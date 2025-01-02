using UnityEngine;
using System.Collections.Generic;
using System;

namespace SURender.InfinityScale
{
    public class ObjectPoolSystem : MonoBehaviour
    {
        #region 单例
        public static ObjectPoolSystem Instance { get; private set; }
        #endregion

        #region 序列化字段
        [System.Serializable]
        public class PoolConfig
        {
            public int defaultPoolSize = 20;        // 默认池大小
            public int maxPoolSize = 100;           // 最大池大小
            public float cleanupInterval = 30f;     // 清理间隔（秒）
            public float unusedTimeout = 60f;       // 未使用超时（秒）
        }

        [SerializeField] private PoolConfig config;
        #endregion

        #region 私有字段
        private Dictionary<string, Queue<BuildingBase>> buildingPools;
        private Dictionary<string, GameObject> prefabCache;
        private Dictionary<string, int> poolSizes;
        private Dictionary<BuildingBase, float> lastUsedTime;
        private Transform poolContainer;
        private float lastCleanupTime;
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

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            // 定期清理未使用的对象
            if (Time.time - lastCleanupTime > config.cleanupInterval)
            {
                CleanupUnusedObjects();
                lastCleanupTime = Time.time;
            }
        }
        #endregion

        #region 初始化
        private void InitializeSystem()
        {
            buildingPools = new Dictionary<string, Queue<BuildingBase>>();
            prefabCache = new Dictionary<string, GameObject>();
            poolSizes = new Dictionary<string, int>();
            lastUsedTime = new Dictionary<BuildingBase, float>();
            
            // 创建对象池容器
            poolContainer = new GameObject("ObjectPool_Container").transform;
            poolContainer.SetParent(transform);
            
            lastCleanupTime = Time.time;
        }
        #endregion

        #region 对象池操作
        public void PrewarmPool(string prefabPath, int size)
        {
            if (!buildingPools.ContainsKey(prefabPath))
            {
                buildingPools[prefabPath] = new Queue<BuildingBase>();
                poolSizes[prefabPath] = 0;
            }

            // 预加载预制体
            ResourceSystem.Instance.LoadAssetAsync<GameObject>(prefabPath, prefab =>
            {
                if (prefab != null)
                {
                    prefabCache[prefabPath] = prefab;
                    
                    // 预热对象池
                    for (int i = 0; i < size && poolSizes[prefabPath] < config.maxPoolSize; i++)
                    {
                        CreateNewInstance(prefabPath, true);
                    }
                }
            });
        }

        public void GetBuilding(string prefabPath, Vector3 position, Transform parent, Action<BuildingBase> onComplete)
        {
            if (!buildingPools.ContainsKey(prefabPath))
            {
                buildingPools[prefabPath] = new Queue<BuildingBase>();
                poolSizes[prefabPath] = 0;
                PrewarmPool(prefabPath, config.defaultPoolSize);
            }

            if (buildingPools[prefabPath].Count > 0)
            {
                // 从池中获取对象
                var building = buildingPools[prefabPath].Dequeue();
                if (building != null)
                {
                    ActivateBuilding(building, position, parent);
                    onComplete?.Invoke(building);
                    return;
                }
            }

            // 如果池中没有可用对象，且未达到最大大小，创建新实例
            if (poolSizes[prefabPath] < config.maxPoolSize)
            {
                CreateNewInstance(prefabPath, false, position, parent, onComplete);
            }
            else
            {
                // 如果达到最大大小，等待资源释放
                Debug.LogWarning($"Object pool for {prefabPath} has reached its maximum size. Waiting for objects to be returned.");
                onComplete?.Invoke(null);
            }
        }

        public void ReturnBuilding(BuildingBase building)
        {
            if (building == null) return;

            string prefabPath = building.PrefabPath;
            if (string.IsNullOrEmpty(prefabPath)) return;

            // 重置对象状态
            building.gameObject.SetActive(false);
            building.transform.SetParent(poolContainer);
            building.transform.localPosition = Vector3.zero;
            building.transform.localRotation = Quaternion.identity;
            building.transform.localScale = Vector3.one;

            // 返回到池中
            if (!buildingPools.ContainsKey(prefabPath))
            {
                buildingPools[prefabPath] = new Queue<BuildingBase>();
            }
            buildingPools[prefabPath].Enqueue(building);

            // 更新最后使用时间
            lastUsedTime[building] = Time.time;
        }

        private void CreateNewInstance(string prefabPath, bool addToPool = true, Vector3? position = null, 
            Transform parent = null, Action<BuildingBase> onComplete = null)
        {
            if (prefabCache.TryGetValue(prefabPath, out GameObject prefab))
            {
                InstantiateBuilding(prefab, prefabPath, addToPool, position, parent, onComplete);
            }
            else
            {
                // 如果预制体未缓存，先加载
                ResourceSystem.Instance.LoadAssetAsync<GameObject>(prefabPath, loadedPrefab =>
                {
                    if (loadedPrefab != null)
                    {
                        prefabCache[prefabPath] = loadedPrefab;
                        InstantiateBuilding(loadedPrefab, prefabPath, addToPool, position, parent, onComplete);
                    }
                    else
                    {
                        Debug.LogError($"Failed to load prefab: {prefabPath}");
                        onComplete?.Invoke(null);
                    }
                });
            }
        }

        private void InstantiateBuilding(GameObject prefab, string prefabPath, bool addToPool, Vector3? position,
            Transform parent, Action<BuildingBase> onComplete)
        {
            GameObject instance = Instantiate(prefab, position ?? Vector3.zero, Quaternion.identity, 
                addToPool ? poolContainer : parent);
            
            var building = instance.GetComponent<BuildingBase>();
            if (building != null)
            {
                building.PrefabPath = prefabPath;
                
                if (addToPool)
                {
                    instance.SetActive(false);
                    buildingPools[prefabPath].Enqueue(building);
                }
                else
                {
                    ActivateBuilding(building, position.Value, parent);
                }
                
                poolSizes[prefabPath]++;
                lastUsedTime[building] = Time.time;
                
                onComplete?.Invoke(building);
            }
            else
            {
                Debug.LogError($"Prefab {prefabPath} does not have BuildingBase component");
                Destroy(instance);
                onComplete?.Invoke(null);
            }
        }

        private void ActivateBuilding(BuildingBase building, Vector3 position, Transform parent)
        {
            building.transform.SetParent(parent);
            building.transform.position = position;
            building.gameObject.SetActive(true);
            lastUsedTime[building] = Time.time;
        }
        #endregion

        #region 清理
        private void CleanupUnusedObjects()
        {
            float currentTime = Time.time;
            foreach (var pool in buildingPools.Values)
            {
                var unusedObjects = new List<BuildingBase>();
                foreach (var building in pool)
                {
                    if (currentTime - lastUsedTime[building] > config.unusedTimeout)
                    {
                        unusedObjects.Add(building);
                    }
                }

                foreach (var building in unusedObjects)
                {
                    pool.Dequeue();
                    poolSizes[building.PrefabPath]--;
                    lastUsedTime.Remove(building);
                    Destroy(building.gameObject);
                }
            }
        }

        public void ClearPool(string prefabPath)
        {
            if (buildingPools.TryGetValue(prefabPath, out var pool))
            {
                while (pool.Count > 0)
                {
                    var building = pool.Dequeue();
                    if (building != null)
                    {
                        lastUsedTime.Remove(building);
                        Destroy(building.gameObject);
                    }
                }
                poolSizes[prefabPath] = 0;
            }
        }

        public void ClearAllPools()
        {
            foreach (var prefabPath in buildingPools.Keys)
            {
                ClearPool(prefabPath);
            }
            buildingPools.Clear();
            poolSizes.Clear();
            lastUsedTime.Clear();
        }
        #endregion

        #region 调试
        public int GetPoolSize(string prefabPath)
        {
            return poolSizes.TryGetValue(prefabPath, out int size) ? size : 0;
        }

        public int GetAvailableCount(string prefabPath)
        {
            return buildingPools.TryGetValue(prefabPath, out var pool) ? pool.Count : 0;
        }
        #endregion
    }
} 