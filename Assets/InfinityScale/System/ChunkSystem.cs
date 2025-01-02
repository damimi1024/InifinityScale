using UnityEngine;
using System.Collections.Generic;
using System.Collections;

namespace SURender.InfinityScale
{
    public class ChunkSystem : MonoBehaviour
    {
        #region 单例
        public static ChunkSystem Instance { get; private set; }
        #endregion

        #region 序列化字段
        [System.Serializable]
        public class ChunkConfig
        {
            public float chunkSize = 100f;           // 区块大小
            public int viewDistance = 5;             // 可见区块距离
            public int maxConcurrentLoads = 4;       // 最大同时加载数
            public float loadingInterval = 0.1f;     // 加载间隔
        }

        [SerializeField] public ChunkConfig config;
        #endregion

        #region 私有字段
        private Dictionary<Vector2Int, Chunk> loadedChunks;
        private HashSet<Vector2Int> visibleChunks;
        private Queue<Vector2Int> loadQueue;
        private Queue<Vector2Int> unloadQueue;
        private Vector2Int currentCenterChunk;
        private bool isProcessingQueue;
        private Transform buildingContainer;
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
            loadedChunks = new Dictionary<Vector2Int, Chunk>();
            visibleChunks = new HashSet<Vector2Int>();
            loadQueue = new Queue<Vector2Int>();
            unloadQueue = new Queue<Vector2Int>();

            // 创建建筑容器
            buildingContainer = new GameObject("ChunkContainer").transform;
            buildingContainer.SetParent(transform);
        }

        private void Start()
        {
            StartCoroutine(ProcessLoadQueue());
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
        #endregion

        #region 区块管理
        public void UpdateSystem(Vector3 cameraPosition)
        {
            Vector2Int newCenterChunk = WorldToChunkPosition(cameraPosition);

            if (newCenterChunk != currentCenterChunk)
            {
                currentCenterChunk = newCenterChunk;
                UpdateVisibleChunks();
            }
        }

        private void UpdateVisibleChunks()
        {
            HashSet<Vector2Int> newVisibleChunks = new HashSet<Vector2Int>();

            // 计算可见区块
            for (int x = -config.viewDistance; x <= config.viewDistance; x++)
            {
                for (int z = -config.viewDistance; z <= config.viewDistance; z++)
                {
                    Vector2Int offset = new Vector2Int(x, z);
                    Vector2Int chunkPos = currentCenterChunk + offset;
                    newVisibleChunks.Add(chunkPos);
                }
            }

            // 找出需要加载和卸载的区块
            foreach (Vector2Int chunk in newVisibleChunks)
            {
                if (!loadedChunks.ContainsKey(chunk))
                {
                    loadQueue.Enqueue(chunk);
                }
            }

            // 更新现有区块的可见性
            foreach (var kvp in loadedChunks)
            {
                Vector2Int chunkPos = kvp.Key;
                Chunk chunk = kvp.Value;
                bool shouldBeVisible = newVisibleChunks.Contains(chunkPos);
                
                // 如果区块状态需要改变
                if (chunk.IsVisible != shouldBeVisible)
                {
                    // Debug.Log($"Chunk {chunkPos} visibility changed to {shouldBeVisible}");
                    chunk.SetVisible(shouldBeVisible);
                    
                    // 如果区块变为不可见，加入卸载队列
                    if (!shouldBeVisible)
                    {
                        unloadQueue.Enqueue(chunkPos);
                    }
                }
            }

            visibleChunks = newVisibleChunks;
        }

        private IEnumerator ProcessLoadQueue()
        {
            WaitForSeconds wait = new WaitForSeconds(config.loadingInterval);

            while (true)
            {
                // 处理卸载队列
                while (unloadQueue.Count > 0)
                {
                    Vector2Int chunkPos = unloadQueue.Dequeue();
                    UnloadChunk(chunkPos);
                    yield return wait;
                }

                // 处理加载队列
                while (loadQueue.Count > 0)
                {
                    Vector2Int chunkPos = loadQueue.Dequeue();
                    LoadChunk(chunkPos);
                    yield return wait;
                }

                yield return wait;
            }
        }

        private void LoadChunk(Vector2Int position)
        {
            if (loadedChunks.ContainsKey(position))
                return;

            // 创建区块GameObject
            GameObject chunkObject = new GameObject($"Chunk_{position.x}_{position.y}");
            chunkObject.transform.SetParent(buildingContainer);
            
            // 设置区块位置
            Vector3 worldPos = ChunkToWorldPosition(position);
            chunkObject.transform.position = worldPos;

            // 创建Chunk实例
            Chunk chunk = new Chunk(position, chunkObject.transform);
            loadedChunks.Add(position, chunk);
            StartCoroutine(LoadChunkContent(chunk));
        }

        private void UnloadChunk(Vector2Int position)
        {
            if (!loadedChunks.ContainsKey(position))
                return;

            Chunk chunk = loadedChunks[position];
            
            // 在卸载前，将当前区块中的建筑数据保存到BuildingSystem中
            if (chunk.buildings != null && chunk.buildings.Count > 0)
            {
                foreach (var building in chunk.buildings)
                {
                    if (building != null && building.BuildingData != null)
                    {
                        BuildingSystem.Instance.AddPendingBuilding(position, building.BuildingData);
                        Debug.Log($"Saving building {building.BuildingId} data for chunk {position}");
                    }
                }
            }
            
            chunk.Unload();
            loadedChunks.Remove(position);
            Debug.Log($"Unloaded chunk at {position}, saved {(chunk.buildings != null ? chunk.buildings.Count : 0)} buildings");
        }

        private IEnumerator LoadChunkContent(Chunk chunk)
        {
            bool hasLoadedAnyBuilding = false;
            int pendingBuildings = 0;  // 跟踪待处理的建筑数量
            
            // 先检查是否有之前保存的建筑数据
            var savedBuildings = BuildingSystem.Instance.GetPendingBuildings(chunk.Position);
            if (savedBuildings != null && savedBuildings.Count > 0)
            {
                Debug.Log($"Found {savedBuildings.Count} saved buildings for chunk {chunk.Position}");
                foreach (var buildingData in savedBuildings)
                {
                    if (buildingData != null)
                    {
                        pendingBuildings++;  // 增加待处理数量
                        BuildingSystem.Instance.CreateBuilding(
                            buildingData,
                            buildingData.position,
                            chunk.Transform,
                            (building) =>
                            {
                                if (building != null)
                                {
                                    chunk.AddBuilding(building);
                                    hasLoadedAnyBuilding = true;
                                    Debug.Log($"Restored building {building.BuildingId} to chunk {chunk.Position}");
                                }
                                pendingBuildings--;  // 完成一个建筑的处理
                            });
                        yield return null;
                    }
                }
                
                // 等待所有建筑创建完成
                while (pendingBuildings > 0)
                {
                    yield return null;
                }
            }
            
            // 如果没有加载任何保存的建筑，则尝试加载新的建筑
            if (!hasLoadedAnyBuilding)
            {
                Debug.Log($"No saved buildings loaded for chunk {chunk.Position}, loading new buildings");
                yield return StartCoroutine(LoadChunkBuildings(chunk));
            }
            
            yield return StartCoroutine(LoadChunkTerrain(chunk));
            chunk.SetLoaded(true);
            
            // 设置可见性
            if (visibleChunks.Contains(chunk.Position))
            {
                chunk.SetVisible(true);
                Debug.Log($"Setting chunk {chunk.Position} visible after loading");
            }
        }

        private IEnumerator LoadChunkBuildings(Chunk chunk)
        {
            Vector3 chunkWorldPos = ChunkToWorldPosition(chunk.Position);
            
            // 从BuildingSystem获取该区块范围内的建筑数据
            var buildingsInChunk = BuildingSystem.Instance.GetBuildingsInRange(
                chunkWorldPos,
                config.chunkSize
            );
            
            Debug.Log($"Found {buildingsInChunk.Count} buildings in range for chunk {chunk.Position}");
            
            foreach (var buildingData in buildingsInChunk)
            {
                if (buildingData != null)
                {
                    // 检查建筑预制体是否使用实例化渲染
                    bool useInstancing = BuildingSystem.Instance.CheckBuildingUseInstancing(buildingData);
                    
                    // 先添加到实例化数据中
                    if (useInstancing)
                    {
                        chunk.AddBuildingData(buildingData);
                        Debug.Log($"Added instanced building data for {buildingData.buildingId} to chunk {chunk.Position}");
                    }
                    else
                    {
                        // 如果不使用实例化渲染，创建实际的GameObject
                        BuildingSystem.Instance.CreateBuilding(
                            buildingData,
                            buildingData.position,
                            chunk.Transform,
                            (building) =>
                            {
                                if (building != null)
                                {
                                    // 再次检查UseInstancing属性，因为可能在异步加载后发生变化
                                    if (building.UseInstancing)
                                    {
                                        // 如果变成了实例化渲染，销毁GameObject并添加到实例化数据中
                                        chunk.AddBuildingData(buildingData);
                                        GameObject.Destroy(building.gameObject);
                                        Debug.Log($"Converted building {buildingData.buildingId} to instanced rendering");
                                    }
                                    else
                                    {
                                        chunk.AddBuilding(building);
                                        Debug.Log($"Added new building {building.BuildingId} to chunk {chunk.Position}");
                                    }
                                }
                            });
                    }
                    yield return null;
                }
            }
        }

        private IEnumerator LoadChunkTerrain(Chunk chunk)
        {
            Vector3 chunkWorldPos = ChunkToWorldPosition(chunk.Position);
            
            // 加载地形资源
            string terrainPrefabPath = $"Terrains/Chunk_{chunk.Position.x}_{chunk.Position.y}";
            ResourceSystem.Instance.LoadAssetAsync<GameObject>(terrainPrefabPath, (terrainPrefab) =>
            {
                if (terrainPrefab != null)
                {
                    chunk.terrainObject = GameObject.Instantiate(
                        terrainPrefab,
                        chunkWorldPos,
                        Quaternion.identity,
                        chunk.Transform  // 设置地形的父物体
                    );
                }
            });
            
            yield return null;
        }
        #endregion

        #region 辅助方法
        public Vector2Int WorldToChunkPosition(Vector3 worldPosition)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPosition.x / config.chunkSize),
                Mathf.FloorToInt(worldPosition.z / config.chunkSize)
            );
        }

        private Vector3 ChunkToWorldPosition(Vector2Int chunkPosition)
        {
            return new Vector3(
                chunkPosition.x * config.chunkSize,
                0,
                chunkPosition.y * config.chunkSize
            );
        }
        #endregion

        #region 公共接口
        public Chunk GetChunk(Vector2Int position)
        {
            return loadedChunks.TryGetValue(position, out Chunk chunk) ? chunk : null;
        }

        public bool IsChunkLoaded(Vector2Int position)
        {
            return loadedChunks.ContainsKey(position);
        }

        public Vector2Int GetCurrentCenterChunk()
        {
            return currentCenterChunk;
        }

        public HashSet<Vector2Int> GetVisibleChunks()
        {
            if(visibleChunks!=null)
                return new HashSet<Vector2Int>(visibleChunks);
            return null;
        }

        public int GetLoadedChunkCount()
        {
            return loadedChunks.Count;
        }
        #endregion
    }

    public class Chunk
    {
        public Vector2Int Position { get; private set; }
        public bool IsLoaded { get; private set; }
        public bool IsVisible { get; private set; }
        public Transform Transform { get; private set; }
        
        public List<BuildingBase> buildings { get; private set; }
        public GameObject terrainObject;
        public List<BuildingData> instancedBuildingData { get; private set; }

        public Chunk(Vector2Int position, Transform chunkTransform)
        {
            Position = position;
            Transform = chunkTransform;
            IsLoaded = false;
            IsVisible = false;
            buildings = new List<BuildingBase>();
            instancedBuildingData = new List<BuildingData>();
        }

        public void SetVisible(bool visible)
        {
            if (Transform != null)
            {
                Transform.gameObject.SetActive(visible);
                IsVisible = visible;
                // Debug.Log($"Setting chunk {Position} visibility to {visible}");
            }
        }

        public void AddBuilding(BuildingBase building)
        {
            if (building != null && !buildings.Contains(building))
            {
                buildings.Add(building);
                building.transform.SetParent(Transform);
                building.gameObject.SetActive(IsVisible);
                // Debug.Log($"Added building {building.BuildingId} to chunk {Position}, total buildings: {buildings.Count}");
            }
        }

        public void SetLoaded(bool loaded)
        {
            IsLoaded = loaded;
            
            if (loaded)
            {
                ScaleEventSystem.TriggerChunkLoaded(Position);
            }
        }

        public void Unload()
        {
            IsLoaded = false;
            IsVisible = false;

            // 清理建筑
            if (buildings != null)
            {
                foreach (var building in buildings.ToArray())
                {
                    if (building != null)
                    {
                        BuildingSystem.Instance?.UnregisterBuilding(building);
                        // 将建筑返回对象池而不是销毁
                        ObjectPoolSystem.Instance?.ReturnBuilding(building);
                    }
                }
                buildings.Clear();
            }

            // 清理实例化渲染的建筑数据
            if (instancedBuildingData != null)
            {
                foreach (var buildingData in instancedBuildingData)
                {
                    BuildingSystem.Instance?.UnregisterBuildingData(buildingData);
                }
                instancedBuildingData.Clear();
            }

            // 清理地形资源
            if (terrainObject != null)
            {
                GameObject.Destroy(terrainObject);
                terrainObject = null;
            }

            // 销毁区块GameObject
            if (Transform != null)
            {
                GameObject.Destroy(Transform.gameObject);
                Transform = null;
            }

            ScaleEventSystem.TriggerChunkUnloaded(Position);
        }

        public void AddBuildingData(BuildingData buildingData)
        {
            if (buildingData != null && !instancedBuildingData.Contains(buildingData))
            {
                instancedBuildingData.Add(buildingData);
                BuildingSystem.Instance.AddInstancedBuildingData(buildingData);
            }
        }
    }
}
