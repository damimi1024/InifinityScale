using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SURender.InfinityScale
{
    public class BuildingSystem : MonoBehaviour
    {
        #region 单例
        public static BuildingSystem Instance { get; private set; }
        #endregion

        #region 序列化字段
        [System.Serializable]
        public class BuildingConfig
        {
            public float fadeSpeed = 1f;              // 建筑淡入淡出速度
            public float lodTransitionSpeed = 0.5f;   // LOD切换速度
            public int maxInstancedBuildings = 1000;  // 最大实例化建筑数量
        }

        [SerializeField] private BuildingConfig config;
        #endregion

        #region 私有字段
        private Dictionary<BuildingType, List<BuildingBase>> activeBuildings;
        private Dictionary<BuildingType, MaterialPropertyBlock> propertyBlocks;
        private Dictionary<string, BuildingBase> buildingIdMap;  // 通过ID快速查找建筑
        private Dictionary<Vector2Int, HashSet<BuildingBase>> chunkBuildingMap;  // 通过区块位置快速查找建筑
        private Dictionary<string, bool> prefabInstancingCache = new Dictionary<string, bool>();  // 预制体Instancing缓存
        private ResourceSystem resourceSystem;
        
        // 待处理的建筑
        private Dictionary<Vector2Int, HashSet<BuildingData>> pendingBuildings = new Dictionary<Vector2Int, HashSet<BuildingData>>();
        // 实例化渲染的建筑数据
        private Dictionary<BuildingType, HashSet<BuildingData>> instancedBuildingDataMap = new Dictionary<BuildingType, HashSet<BuildingData>>();
        private Transform fallbackContainer;
        #endregion

        #region 初始化
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeSystem();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeSystem()
        {
            activeBuildings = new Dictionary<BuildingType, List<BuildingBase>>();
            propertyBlocks = new Dictionary<BuildingType, MaterialPropertyBlock>();
            buildingIdMap = new Dictionary<string, BuildingBase>();
            chunkBuildingMap = new Dictionary<Vector2Int, HashSet<BuildingBase>>();
            instancedBuildingDataMap = new Dictionary<BuildingType, HashSet<BuildingData>>();
            resourceSystem = ResourceSystem.Instance;

            foreach (BuildingType type in System.Enum.GetValues(typeof(BuildingType)))
            {
                activeBuildings[type] = new List<BuildingBase>();
                propertyBlocks[type] = new MaterialPropertyBlock();
                instancedBuildingDataMap[type] = new HashSet<BuildingData>();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
        #endregion

        #region 建筑管理
        public void CreateBuilding(BuildingData data, Vector3 position, Transform parent = null, System.Action<BuildingBase> onComplete = null)
        {
            if (data == null)
            {
                Debug.LogError("BuildingData is null!");
                onComplete?.Invoke(null);
                return;
            }

            // O(1) 检查建筑是否已存在
            if (buildingIdMap.ContainsKey(data.buildingId))
            {
                Debug.LogWarning($"Building {data.buildingId} already exists, skipping creation");
                onComplete?.Invoke(null);
                return;
            }

            string fullPath = $"Buildings/{data.prefabPath}";
            
            // 检查是否使用实例化渲染
            if (CheckBuildingUseInstancing(data))
            {
                // 如果使用实例化渲染，直接回调
                onComplete?.Invoke(null);
                return;
            }

            // 使用对象池获取或创建建筑实例
            ObjectPoolSystem.Instance.GetBuilding(fullPath, position, parent, building =>
            {
                if (building != null)
                {
                    building.Initialize(data);
                    RegisterBuilding(building);
                    
                    // 根据当前状态设置初始可见性
                    var currentState = InfinityScaleManager.Instance.GetCurrentState();
                    building.UpdateState(currentState);
                }
                onComplete?.Invoke(building);
            });
        }

        private void RegisterBuilding(BuildingBase building)
        {
            if (building == null) return;

            // 记录建筑ID映射
            buildingIdMap[building.BuildingId] = building;
            
            // 记录区块映射
            Vector2Int chunkPos = ChunkSystem.Instance.WorldToChunkPosition(building.transform.position);
            if (!chunkBuildingMap.ContainsKey(chunkPos))
            {
                chunkBuildingMap[chunkPos] = new HashSet<BuildingBase>();
            }
            chunkBuildingMap[chunkPos].Add(building);
            
            // 记录类型映射
            if (!activeBuildings.ContainsKey(building.BuildingType))
            {
                activeBuildings[building.BuildingType] = new List<BuildingBase>();
            }
            activeBuildings[building.BuildingType].Add(building);
        }

        public void UnregisterBuilding(BuildingBase building)
        {
            if (building == null) return;

            // 移除ID映射
            buildingIdMap.Remove(building.BuildingId);
            
            // 移除区块映射
            Vector2Int chunkPos = ChunkSystem.Instance.WorldToChunkPosition(building.transform.position);
            if (chunkBuildingMap.TryGetValue(chunkPos, out var buildings))
            {
                buildings.Remove(building);
                if (buildings.Count == 0)
                {
                    chunkBuildingMap.Remove(chunkPos);
                }
            }
            
            // 移除类型映射
            if (activeBuildings.TryGetValue(building.BuildingType, out var buildingList))
            {
                buildingList.Remove(building);
            }
        }

        public void UpdateBuildingTypeVisibility(BuildingType type, float alpha)
        {
            if (activeBuildings.TryGetValue(type, out List<BuildingBase> buildings))
            {
                foreach (var building in buildings)
                {
                    if (building != null)
                    {
                        building.UpdateVisibility(alpha);
                        
                        // 如果是实例化渲染的建筑，需要更新propertyBlock
                        if (building.UseInstancing && propertyBlocks.TryGetValue(type, out var propertyBlock))
                        {
                            propertyBlock.SetFloat("_Alpha", alpha);
                        }
                    }
                }
            }
        }
        #endregion

        #region LOD管理
        public void UpdateBuildingLOD(BuildingBase building, float distance)
        {
            int targetLOD = CalculateTargetLOD(distance);
            building.UpdateLOD(targetLOD);
        }

        private int CalculateTargetLOD(float distance)
        {
            // 根据距离计算目标LOD级别
            if (distance < 100f) return 0;
            if (distance < 300f) return 1;
            if (distance < 600f) return 2;
            return 3;
        }
        #endregion

        #region 实例化渲染
        private void UpdateInstancedRendering(BuildingType type, Camera camera)
        {
            if (!activeBuildings.TryGetValue(type, out var buildings) || buildings.Count == 0)
                return;

            // 获取使用实例化渲染的建筑数据
            var instancedBuildingData = instancedBuildingDataMap[type];
            if (instancedBuildingData.Count == 0)
                return;

            // 分离使用实例化和非实例化的建筑
            var instancedBuildings = buildings.Where(b => b != null && b.UseInstancing && b.IsVisible).ToList();
            
            if (instancedBuildings.Count == 0 && instancedBuildingData.Count == 0)
                return;

            var propertyBlock = propertyBlocks[type];

            // 更新实例化渲染数据
            Matrix4x4[] matrices = new Matrix4x4[Mathf.Min(instancedBuildings.Count, config.maxInstancedBuildings)];
            for (int i = 0; i < matrices.Length; i++)
            {
                matrices[i] = instancedBuildings[i].transform.localToWorldMatrix;
            }

            // 执行实例化渲染
            if (instancedBuildings[0] != null)
            {
                var renderer = instancedBuildings[0].GetComponent<MeshRenderer>();
                var meshFilter = instancedBuildings[0].GetComponent<MeshFilter>();
                if (renderer != null && meshFilter != null)
                {
                    Graphics.DrawMeshInstanced(
                        meshFilter.sharedMesh,
                        0,
                        renderer.sharedMaterial,
                        matrices,
                        matrices.Length,
                        propertyBlock
                    );
                }
            }
        }
        #endregion

        public void AddPendingBuilding(Vector2Int chunkPos, BuildingData buildingData)
        {
            if (buildingData == null) return;
            
            if (!pendingBuildings.ContainsKey(chunkPos))
            {
                pendingBuildings[chunkPos] = new HashSet<BuildingData>();
            }
            
            pendingBuildings[chunkPos].Add(buildingData);
        }

        public List<BuildingData> GetPendingBuildings(Vector2Int chunkPos)
        {
            if (pendingBuildings.TryGetValue(chunkPos, out var buildings))
            {
                return new List<BuildingData>(buildings);
            }
            return new List<BuildingData>();
        }

        public List<BuildingData> GetBuildingsInRange(Vector3 center, float range)
        {
            Vector2Int targetChunkPos = ChunkSystem.Instance.WorldToChunkPosition(center);
            HashSet<BuildingData> result = new HashSet<BuildingData>();  // 使用HashSet避免重复
            
            // 获取待处理的建筑
            if (pendingBuildings.TryGetValue(targetChunkPos, out var pendingList))
            {
                foreach (var buildingData in pendingList)
                {
                    result.Add(buildingData);
                }
            }
            
            // 获取当前区块中的建筑
            if (chunkBuildingMap.TryGetValue(targetChunkPos, out var buildings))
            {
                foreach (var building in buildings)
                {
                    if (building != null && building.BuildingData != null)
                    {
                        result.Add(building.BuildingData);
                    }
                }
            }
            
            return new List<BuildingData>(result);
        }

        #region 系统更新
        public void UpdateSystem(InfinityScaleManager.ScaleState currentState)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null) return;

            foreach (var type in activeBuildings.Keys.ToArray())  // 使用ToArray避免遍历时修改集合
            {
                var buildings = activeBuildings[type];
                if (buildings == null) continue;

                foreach (var building in buildings.ToArray())
                {
                    if (building == null)
                    {
                        buildings.Remove(building);
                        continue;
                    }

                    float distance = Vector3.Distance(
                        mainCamera.transform.position,
                        building.transform.position
                    );
                    
                    UpdateBuildingLOD(building, distance);
                    building.UpdateState(currentState);
                }

                // 更新实例化渲染
                if (buildings.Count > 0)
                {
                    UpdateInstancedRendering(type, mainCamera);
                }
            }
        }
        #endregion

        public bool CheckBuildingUseInstancing(BuildingData buildingData)
        {
            if (buildingData == null) return false;

            string fullPath = $"Buildings/{buildingData.prefabPath}";
            
            // 检查缓存
            if (prefabInstancingCache.TryGetValue(fullPath, out bool useInstancing))
            {
                return useInstancing;
            }

            // 如果没有缓存，默认不使用实例化
            prefabInstancingCache[fullPath] = false;
            
            // 异步加载并更新缓存
            ResourceSystem.Instance.LoadAssetAsync<GameObject>(fullPath, (prefab) =>
            {
                if (prefab != null)
                {
                    BuildingBase building = prefab.GetComponent<BuildingBase>();
                    if (building != null)
                    {
                        prefabInstancingCache[fullPath] = building.UseInstancing;
                    }
                }
            });
            
            return false; // 首次检查时返回false，等待异步加载更新缓存
        }

        public void UnregisterBuildingData(BuildingData buildingData)
        {
            if (buildingData == null) return;

            // 从实例化渲染映射中移除
            if (instancedBuildingDataMap.TryGetValue(buildingData.buildingType, out var dataSet))
            {
                dataSet.Remove(buildingData);
            }

            // 从待处理建筑中移除
            Vector2Int chunkPos = ChunkSystem.Instance.WorldToChunkPosition(buildingData.position);
            if (pendingBuildings.TryGetValue(chunkPos, out var pendingSet))
            {
                pendingSet.Remove(buildingData);
                if (pendingSet.Count == 0)
                {
                    pendingBuildings.Remove(chunkPos);
                }
            }
        }

        public void AddInstancedBuildingData(BuildingData buildingData)
        {
            if (buildingData == null) return;

            if (!instancedBuildingDataMap.ContainsKey(buildingData.buildingType))
            {
                instancedBuildingDataMap[buildingData.buildingType] = new HashSet<BuildingData>();
            }
            instancedBuildingDataMap[buildingData.buildingType].Add(buildingData);
        }
    }
}
