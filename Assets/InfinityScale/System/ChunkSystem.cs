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
        [SerializeField] public ChunkLODConfig lodConfig;
        
        [System.Serializable]
        public class ChunkConfig
        {
            public int maxConcurrentLoads = 4;       // 最大同时加载数
            public float loadingInterval = 0.1f;     // 加载间隔
            public bool enableDebugLog = false;      // 是否启用调试日志
        }

        [SerializeField] public ChunkConfig config;
        #endregion

        #region 私有字段
        public Dictionary<Vector2Int, Chunk> loadedChunks;
        private HashSet<Vector2Int> visibleChunks;
        private Queue<Vector2Int> loadQueue;
        private Queue<Vector2Int> unloadQueue;
        private Vector2Int currentCenterChunk;
        private bool isProcessingQueue;
        private Transform buildingContainer;
        public int currentLODLevel { get; private set; } = 0;
        private float lastLODUpdateTime;

        // 添加调试日志方法
        private void DebugLog(string message)
        {
            if (config.enableDebugLog)
            {
                Debug.Log($"[ChunkSystem] {message}");
            }
        }
        #endregion

        #region Unity生命周期
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
        #endregion

        #region 初始化
        private void InitializeSystem()
        {
            loadedChunks = new Dictionary<Vector2Int, Chunk>();
            visibleChunks = new HashSet<Vector2Int>();
            loadQueue = new Queue<Vector2Int>();
            unloadQueue = new Queue<Vector2Int>();

            buildingContainer = new GameObject("ChunkContainer").transform;
            buildingContainer.SetParent(transform);
            
            if (lodConfig == null)
            {
                Debug.LogError("ChunkLODConfig not assigned!");
            }
        }
        #endregion

        #region 区块管理
        public void UpdateSystem(Vector3 cameraPosition)
        {
            // 更新LOD级别
            UpdateLODLevel(cameraPosition);

            // 获取当前LOD级别的区块大小
            float currentChunkSize = lodConfig.GetChunkSizeAtLOD(currentLODLevel);
            
            // 使用当前区块大小计算中心区块位置
            Vector2Int newCenterChunk = WorldToChunkPosition(cameraPosition, currentChunkSize);

            if (newCenterChunk != currentCenterChunk || 
                Time.time - lastLODUpdateTime > lodConfig.transitionDuration)
            {
                currentCenterChunk = newCenterChunk;
                UpdateVisibleChunks(cameraPosition);
                lastLODUpdateTime = Time.time;
            }
        }

        private void UpdateLODLevel(Vector3 cameraPosition)
        {
            float cameraHeight = cameraPosition.y;
            int newLODLevel = lodConfig.GetLODLevelAtDistance(cameraHeight);
            
            if (newLODLevel != currentLODLevel)
            {
                currentLODLevel = newLODLevel;
                float newChunkSize = lodConfig.GetChunkSizeAtLOD(currentLODLevel);
                float detailLevel = lodConfig.GetDetailLevelAtLOD(currentLODLevel);
                
                // 更新所有加载的区块的LOD级别
                foreach (var chunk in loadedChunks.Values)
                {
                    chunk.UpdateLOD(currentLODLevel, detailLevel, newChunkSize);
                }
            }
        }

        private void UpdateVisibleChunks(Vector3 cameraPosition)
        {
            HashSet<Vector2Int> newVisibleChunks = new HashSet<Vector2Int>();
            float currentChunkSize = lodConfig.GetChunkSizeAtLOD(currentLODLevel);
            int viewDistance = lodConfig.GetViewDistanceAtLOD(currentLODLevel);
            
            // 获取视锥体平面
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(Camera.main);

            // 计算可见区块
            for (int x = -viewDistance; x <= viewDistance; x++)
            {
                for (int z = -viewDistance; z <= viewDistance; z++)
                {
                    Vector2Int offset = new Vector2Int(x, z);
                    Vector2Int chunkPos = currentCenterChunk + offset;
                    
                    // 创建临时Chunk用于视锥体检测
                    var tempChunk = new Chunk(chunkPos, null, currentChunkSize);
                    
                    // 检查是否在视锥体内并且在视距内
                    if (tempChunk.IsInFrustum(frustumPlanes) && 
                        tempChunk.IsInViewDistance(cameraPosition, viewDistance * currentChunkSize))
                    {
                        newVisibleChunks.Add(chunkPos);
                    }
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
                
                if (chunk.IsVisible != shouldBeVisible)
                {
                    chunk.SetVisible(shouldBeVisible);
                    
                    if (!shouldBeVisible)
                    {
                        unloadQueue.Enqueue(chunkPos);
                    }
                }
                
            }

            visibleChunks = newVisibleChunks;
        }

        public Vector2Int WorldToChunkPosition(Vector3 worldPosition, float chunkSize)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPosition.x / chunkSize),
                Mathf.FloorToInt(worldPosition.z / chunkSize)
            );
        }

        public Vector2Int WorldToChunkPosition(Vector3 worldPosition)
        {
            float currentChunkSize = lodConfig.GetChunkSizeAtLOD(currentLODLevel);
            return WorldToChunkPosition(worldPosition, currentChunkSize);
        }

        public void ReloadChunk(Chunk chunk)
        {
            if (chunk != null)
            {
                StartCoroutine(LoadChunkContent(chunk));
            }
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
                int concurrentLoads = 0;
                while (loadQueue.Count > 0 && concurrentLoads < config.maxConcurrentLoads)
                {
                    Vector2Int chunkPos = loadQueue.Dequeue();
                    LoadChunk(chunkPos);
                    concurrentLoads++;
                    yield return wait;
                }

                yield return wait;
            }
        }

        private void LoadChunk(Vector2Int position)
        {
            if (loadedChunks.ContainsKey(position))
            {
                if (loadedChunks[position].IsLoading)
                {
                    DebugLog($"Chunk {position} is already loading");
                }
                return;
            }

            // 创建区块GameObject
            GameObject chunkObject = new GameObject($"Chunk_{position.x}_{position.y}");
            chunkObject.transform.SetParent(buildingContainer);
            
            // 设置区块位置
            Vector3 worldPos = ChunkToWorldPosition(position);
            chunkObject.transform.position = worldPos;

            // 创建Chunk实例
            Chunk chunk = new Chunk(position, chunkObject.transform, lodConfig.GetChunkSizeAtLOD(currentLODLevel));
            loadedChunks.Add(position, chunk);
            
            // 开始加载过程
            chunk.StartLoading();
            DebugLog($"Started loading chunk at {position}");
        }

        private void UnloadChunk(Vector2Int position)
        {
            //Debug.Log("卸载区块"+ position);
            if (!loadedChunks.ContainsKey(position))
            {
                //Debug.Log("不包含该区块"+ position);
                return;
            }
                

            Chunk chunk = loadedChunks[position];
            
            // 如果正在加载，先取消加载
            if (chunk.IsLoading)
            {
                chunk.CancelLoading();
                DebugLog($"Cancelled loading for chunk {position}");
            }
            
            // 在卸载前，将当前区块中的建筑数据保存到BuildingSystem中
            if (chunk.buildings != null && chunk.buildings.Count > 0)
            {
                foreach (var building in chunk.buildings)
                {
                    if (building != null && building.BuildingData != null)
                    {
                        BuildingSystem.Instance.AddPendingBuilding(position, building.BuildingData);
                        DebugLog($"Saving building {building.BuildingId} data for chunk {position}");
                    }
                }
            }
            
            chunk.Unload();
            loadedChunks.Remove(position);
            DebugLog($"Unloaded chunk at {position}, saved {(chunk.buildings != null ? chunk.buildings.Count : 0)} buildings");
        }

        private IEnumerator LoadChunkContent(Chunk chunk)
        {
            bool hasLoadedAnyBuilding = false;
            int pendingBuildings = 0;  
            int buildingsCreatedThisFrame = 0;
            const int MAX_BUILDINGS_PER_FRAME = 10; // 每帧最多创建10个建筑
            
            // 先检查是否有之前保存的建筑数据
            var savedBuildings = BuildingSystem.Instance.GetPendingBuildings(chunk.Position);
            if (savedBuildings != null && savedBuildings.Count > 0)
            {
                DebugLog($"Found {savedBuildings.Count} saved buildings for chunk {chunk.Position}");
                foreach (var buildingData in savedBuildings)
                {
                    if (buildingData != null)
                    {
                        pendingBuildings++;  
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
                                    DebugLog($"Restored building {building.BuildingId} to chunk {chunk.Position}");
                                }
                                pendingBuildings--;  
                            });
                        
                        // 每创建MAX_BUILDINGS_PER_FRAME个建筑后才暂停一帧
                        buildingsCreatedThisFrame++;
                        if (buildingsCreatedThisFrame >= MAX_BUILDINGS_PER_FRAME)
                        {
                            buildingsCreatedThisFrame = 0;
                            yield return null;
                        }
                    }
                }
            }
            
            // 如果没有加载任何保存的建筑，则尝试加载新的建筑
            if (!hasLoadedAnyBuilding)
            {
                DebugLog($"No saved buildings loaded for chunk {chunk.Position}, loading new buildings");
                yield return StartCoroutine(LoadChunkBuildings(chunk));
            }
            
            yield return StartCoroutine(LoadChunkTerrain(chunk));
            chunk.SetLoaded(true);
            
            // 设置可见性
            if (visibleChunks.Contains(chunk.Position))
            {
                chunk.SetVisible(true);
                DebugLog($"Setting chunk {chunk.Position} visible after loading");
            }
        }

        private IEnumerator LoadChunkBuildings(Chunk chunk)
        {
            Vector3 chunkWorldPos = ChunkToWorldPosition(chunk.Position);
            float currentChunkSize = lodConfig.GetChunkSizeAtLOD(currentLODLevel);
            
            // 从BuildingSystem获取该区块范围内的建筑数据
            var buildingsInChunk = BuildingSystem.Instance.GetBuildingsInRange(
                chunkWorldPos,
                currentChunkSize
            );
            
            DebugLog($"Found {buildingsInChunk.Count} buildings in range for chunk {chunk.Position}");
            
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
                        DebugLog($"Added instanced building data for {buildingData.buildingId} to chunk {chunk.Position}");
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
                                        DebugLog($"Converted building {buildingData.buildingId} to instanced rendering");
                                    }
                                    else
                                    {
                                        chunk.AddBuilding(building);
                                        DebugLog($"Added new building {building.BuildingId} to chunk {chunk.Position}");
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
        private Vector3 ChunkToWorldPosition(Vector2Int chunkPosition)
        {
            float currentChunkSize = lodConfig.GetChunkSizeAtLOD(currentLODLevel);
            return new Vector3(
                chunkPosition.x * currentChunkSize,
                0,
                chunkPosition.y * currentChunkSize
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

        public Dictionary<Vector2Int, Chunk>.KeyCollection GetLoadedChunks()
        {
            return loadedChunks.Keys;
        }
        public int GetLoadedChunkCount()
        {
            return loadedChunks.Count;
        }

        public IEnumerator LoadChunkContentPublic(Chunk chunk)
        {
            yield return StartCoroutine(LoadChunkContent(chunk));
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

        private int currentLODLevel = 0;
        private float currentDetailLevel = 1f;
        private bool isTransitioning = false;
        private Vector3 chunkCenter;
        private float chunkSize;
        private Bounds chunkBounds;
        private bool isLoading = false; // 添加加载状态标记
        private Coroutine loadingCoroutine = null; // 添加协程引用

        public bool IsLoading => isLoading;

        public void CancelLoading()
        {
            if (loadingCoroutine != null)
            {
                ChunkSystem.Instance.StopCoroutine(loadingCoroutine);
                loadingCoroutine = null;
            }
            isLoading = false;
        }

        public void StartLoading()
        {
            if (isLoading) return;
            isLoading = true;
            loadingCoroutine = ChunkSystem.Instance.StartCoroutine(ChunkSystem.Instance.LoadChunkContentPublic(this));
        }

        public void Unload()
        {
            // 确保先取消任何正在进行的加载
            CancelLoading();
            
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

        private void UpdateChunkBounds()
        {
            chunkCenter = new Vector3(
                Position.x * chunkSize + chunkSize * 0.5f,
                0,
                Position.y * chunkSize + chunkSize * 0.5f
            );
            
            chunkBounds = new Bounds(
                chunkCenter,
                new Vector3(chunkSize, 1000f, chunkSize)
            );
        }

        public void UpdateLOD(int newLODLevel, float detailLevel, float newSize)
        {
            if (currentLODLevel == newLODLevel && Mathf.Approximately(chunkSize, newSize))
                return;

            bool needsRebuild = chunkSize != newSize;
            currentLODLevel = newLODLevel;
            currentDetailLevel = detailLevel;
            chunkSize = newSize;

            // 更新区块边界
            UpdateChunkBounds();

            if (needsRebuild)
            {
                // 标记需要重新加载内容
                IsLoaded = false;
                
                // 保存当前建筑数据
                SaveCurrentBuildings();
                
                // 清理当前内容
                ClearContent();
                
                // 触发重新加载
                ChunkSystem.Instance.ReloadChunk(this);
            }
            else
            {
                // 仅更新现有内容的LOD级别
                UpdateContentLOD();
            }
        }

        private void SaveCurrentBuildings()
        {
            if (buildings != null)
            {
                foreach (var building in buildings)
                {
                    if (building != null && building.BuildingData != null)
                    {
                        BuildingSystem.Instance.AddPendingBuilding(Position, building.BuildingData);
                    }
                }
            }
        }

        private void ClearContent()
        {
            // 清理建筑
            if (buildings != null)
            {
                foreach (var building in buildings.ToArray())
                {
                    if (building != null)
                    {
                        BuildingSystem.Instance?.UnregisterBuilding(building);
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
        }

        private void UpdateContentLOD()
        {
            // 更新建筑LOD
            if (buildings != null)
            {
                foreach (var building in buildings)
                {
                    if (building != null)
                    {
                        building.UpdateLOD(currentLODLevel);
                    }
                }
            }
        }

        public bool IsInViewDistance(Vector3 cameraPosition, float maxDistance)
        {
            return Vector3.Distance(cameraPosition, chunkCenter) <= maxDistance;
        }

        public bool IsInFrustum(Plane[] frustumPlanes)
        {
            return GeometryUtility.TestPlanesAABB(frustumPlanes, chunkBounds);
        }

        public float GetDistanceToCamera(Vector3 cameraPosition)
        {
            return Vector3.Distance(cameraPosition, chunkCenter);
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

        public void AddBuildingData(BuildingData buildingData)
        {
            if (buildingData != null && !instancedBuildingData.Contains(buildingData))
            {
                instancedBuildingData.Add(buildingData);
                BuildingSystem.Instance.AddInstancedBuildingData(buildingData);
            }
        }

        public Chunk(Vector2Int position, Transform chunkTransform, float size)
        {
            Position = position;
            Transform = chunkTransform;
            IsLoaded = false;
            IsVisible = false;
            buildings = new List<BuildingBase>();
            instancedBuildingData = new List<BuildingData>();
            chunkSize = size;
            isLoading = false;
            loadingCoroutine = null;
            
            // 计算区块中心点和边界
            UpdateChunkBounds();
        }
    }
}
