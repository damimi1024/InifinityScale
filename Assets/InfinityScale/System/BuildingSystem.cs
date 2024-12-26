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
        private ResourceSystem resourceSystem;
        
        // 待处理的建筑
        private Dictionary<Vector2Int, List<BuildingData>> pendingBuildings = new Dictionary<Vector2Int, List<BuildingData>>();
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
            resourceSystem = ResourceSystem.Instance;

            foreach (BuildingType type in System.Enum.GetValues(typeof(BuildingType)))
            {
                activeBuildings[type] = new List<BuildingBase>();
                propertyBlocks[type] = new MaterialPropertyBlock();
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
            string fullPath = $"Buildings/{data.prefabPath}";
            ResourceSystem.Instance.LoadAssetAsync<GameObject>(fullPath, (prefab) =>
            {
                if (prefab != null)
                {
                    GameObject buildingObj = Instantiate(prefab, position, Quaternion.identity);
                    if (parent != null)
                    {
                        buildingObj.transform.SetParent(parent);
                    }
                    else
                    {
                        Debug.LogWarning(parent +"  is null");
                    }
                    
                    BuildingBase building = buildingObj.GetComponent<BuildingBase>();
                    if (building != null)
                    {
                        building.Initialize(data);
                        RegisterBuilding(building);
                        onComplete?.Invoke(building);
                    }
                }
                else
                {
                    Debug.LogError($"Failed to load building prefab: {fullPath}");
                    onComplete?.Invoke(null);
                }
            });
        }

        private void RegisterBuilding(BuildingBase building)
        {
            if (!activeBuildings.ContainsKey(building.BuildingType))
            {
                activeBuildings[building.BuildingType] = new List<BuildingBase>();
            }
            
            activeBuildings[building.BuildingType].Add(building);
        }

        public void UnregisterBuilding(BuildingBase building)
        {
            if (building != null && activeBuildings.ContainsKey(building.BuildingType))
            {
                activeBuildings[building.BuildingType].Remove(building);
            }
        }

        public void UpdateBuildingTypeVisibility(BuildingType type, float alpha)
        {
            if (activeBuildings.TryGetValue(type, out List<BuildingBase> buildings))
            {
                foreach (var building in buildings)
                {
                    building.UpdateVisibility(alpha);
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
            if (!activeBuildings.ContainsKey(type))
                return;

            var buildings = activeBuildings[type];
            var propertyBlock = propertyBlocks[type];

            // 更新实例化渲染数据
            Matrix4x4[] matrices = new Matrix4x4[Mathf.Min(buildings.Count, config.maxInstancedBuildings)];
            for (int i = 0; i < matrices.Length; i++)
            {
                matrices[i] = buildings[i].transform.localToWorldMatrix;
            }

            // 执行实例化渲染
            if (buildings.Count > 0 && buildings[0] != null)
            {
                var renderer = buildings[0].GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    Graphics.DrawMeshInstanced(
                        buildings[0].GetComponent<MeshFilter>().sharedMesh,
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
                pendingBuildings[chunkPos] = new List<BuildingData>();
            }
            
            // 检查是否已经存在相同的建筑数据
            bool exists = pendingBuildings[chunkPos].Any(b => 
                b.buildingId == buildingData.buildingId && 
                Vector3.Distance(b.position, buildingData.position) < 0.1f);
                
            if (!exists)
            {
                pendingBuildings[chunkPos].Add(buildingData);
                // Debug.Log($"Added pending building {buildingData.buildingId} to chunk {chunkPos}, total pending: {pendingBuildings[chunkPos].Count}");
            }
        }

        public List<BuildingData> GetPendingBuildings(Vector2Int chunkPos)
        {
            if (pendingBuildings.TryGetValue(chunkPos, out var buildings))
            {
                pendingBuildings.Remove(chunkPos);  // 获取后移除
                return buildings;
            }
            return new List<BuildingData>();
        }

        public List<BuildingData> GetBuildingsInRange(Vector3 center, float range)
        {
            List<BuildingData> result = new List<BuildingData>();
            Vector2Int chunkPos = ChunkSystem.Instance.WorldToChunkPosition(center);
            
            // 首先获取待处理的建筑
            if (pendingBuildings.ContainsKey(chunkPos))
            {
                result.AddRange(pendingBuildings[chunkPos]);
                Debug.Log($"Found {pendingBuildings[chunkPos].Count} pending buildings for chunk {chunkPos}");
            }
            
            // 然后获取范围内的其他建筑
            float rangeSqr = range * range;
            foreach (var buildingList in activeBuildings.Values)
            {
                foreach (var building in buildingList)
                {
                    if (building != null && 
                        Vector3.SqrMagnitude(building.transform.position - center) <= rangeSqr)
                    {
                        if (building.BuildingData != null)
                        {
                            // 检查是否已经在结果列表中
                            bool exists = result.Any(b => 
                                b.buildingId == building.BuildingData.buildingId && 
                                Vector3.Distance(b.position, building.BuildingData.position) < 0.1f);
                                
                            if (!exists)
                            {
                                result.Add(building.BuildingData);
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"Building {building.name} has no BuildingData!");
                        }
                    }
                }
            }
            
            Debug.Log($"Total buildings found in range for chunk {chunkPos}: {result.Count}");
            return result;
        }

        #region 系统更新
        public void UpdateSystem(InfinityScaleManager.ScaleState currentState)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
                return;

            foreach (var type in activeBuildings.Keys)
            {
                var buildings = activeBuildings[type];
                foreach (var building in buildings)
                {
                    if (building != null)
                    {
                        float distance = Vector3.Distance(
                            mainCamera.transform.position,
                            building.transform.position
                        );
                        
                        UpdateBuildingLOD(building, distance);
                        
                        // 根据当前状态更新建筑
                        building.UpdateState(currentState);
                    }
                }

                // 更新实例化渲染
                if (buildings.Count > 0)
                {
                    UpdateInstancedRendering(type, mainCamera);
                }
            }
        }
        #endregion
    }
}
