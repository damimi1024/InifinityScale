using UnityEngine;
using System.Collections.Generic;
using System.Collections;

namespace SURender.InfinityScale
{
    public class ResourceSystem : MonoBehaviour
    {
        #region 单例
        public static ResourceSystem Instance { get; private set; }
        #endregion

        #region 序列化字段
        [System.Serializable]
        public class ResourceConfig
        {
            public int maxConcurrentLoads = 4;        // 最大同时加载数
            public int poolInitialSize = 10;          // 对象池初始大小
            public float cacheTimeout = 30f;          // 缓存超时时间
        }

        [SerializeField] private ResourceConfig config;
        #endregion

        #region 私有字段
        // 资源缓存
        private Dictionary<string, Object> resourceCache;
        private Dictionary<string, float> lastAccessTime;
        
        // 对象池
        private Dictionary<string, Queue<GameObject>> objectPools;
        private Dictionary<string, GameObject> poolPrefabs;
        
        // 加载队列
        private Queue<ResourceRequest> loadQueue;
        private int currentLoadCount;
        
        // 资源引用计数
        private Dictionary<string, int> referenceCount;
        #endregion

        #region 资源请求
        private class ResourceRequest
        {
            public string resourcePath;
            public System.Type resourceType;
            public System.Action<Object> onComplete;
            public float priority;

            public ResourceRequest(string path, System.Type type, System.Action<Object> callback, float priority = 0)
            {
                this.resourcePath = path;
                this.resourceType = type;
                this.onComplete = callback;
                this.priority = priority;
            }
        }
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
            resourceCache = new Dictionary<string, Object>();
            lastAccessTime = new Dictionary<string, float>();
            objectPools = new Dictionary<string, Queue<GameObject>>();
            poolPrefabs = new Dictionary<string, GameObject>();
            loadQueue = new Queue<ResourceRequest>();
            referenceCount = new Dictionary<string, int>();
            
            StartCoroutine(ProcessLoadQueue());
            StartCoroutine(CleanupCache());
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
        #endregion

        #region 资源加载
        public void LoadAssetAsync<T>(string path, System.Action<T> callback, float priority = 0) where T : Object
        {
            // 检查缓存
            if (resourceCache.TryGetValue(path, out Object cachedResource))
            {
                lastAccessTime[path] = Time.time;
                callback?.Invoke(cachedResource as T);
                return;
            }

            // 添加到加载队列
            loadQueue.Enqueue(new ResourceRequest(path, typeof(T), 
                (obj) => callback?.Invoke(obj as T), priority));
        }

        private IEnumerator ProcessLoadQueue()
        {
            while (true)
            {
                while (loadQueue.Count > 0 && currentLoadCount < config.maxConcurrentLoads)
                {
                    ResourceRequest request = loadQueue.Dequeue();
                    currentLoadCount++;

                    ResourceRequest loadRequest = request;
                    StartCoroutine(LoadResourceCoroutine(loadRequest));
                }

                yield return new WaitForSeconds(0.1f);
            }
        }

        private IEnumerator LoadResourceCoroutine(ResourceRequest request)
        {
            ResourceRequest loadRequest = new ResourceRequest(request.resourcePath, 
                request.resourceType, request.onComplete, request.priority);

            // 异步加载资源
            var asyncOperation = Resources.LoadAsync(loadRequest.resourcePath, loadRequest.resourceType);
            yield return asyncOperation;

            if (asyncOperation.asset != null)
            {
                // 缓存资源
                resourceCache[loadRequest.resourcePath] = asyncOperation.asset;
                lastAccessTime[loadRequest.resourcePath] = Time.time;
                referenceCount[loadRequest.resourcePath] = 0;

                // 回调
                loadRequest.onComplete?.Invoke(asyncOperation.asset);
            }

            currentLoadCount--;
        }
        #endregion

        #region 对象池管理
        public void InitializePool(string prefabPath, int initialSize)
        {
            LoadAssetAsync<GameObject>(prefabPath, (prefab) =>
            {
                if (prefab != null)
                {
                    poolPrefabs[prefabPath] = prefab;
                    Queue<GameObject> pool = new Queue<GameObject>();
                    
                    for (int i = 0; i < initialSize; i++)
                    {
                        GameObject obj = CreatePoolObject(prefab);
                        pool.Enqueue(obj);
                    }
                    
                    objectPools[prefabPath] = pool;
                }
            });
        }

        private GameObject CreatePoolObject(GameObject prefab)
        {
            GameObject obj = Instantiate(prefab);
            obj.SetActive(false);
            return obj;
        }

        public void GetFromPool(string prefabPath, System.Action<GameObject> callback)
        {
            if (objectPools.TryGetValue(prefabPath, out Queue<GameObject> pool))
            {
                GameObject obj = null;
                if (pool.Count > 0)
                {
                    obj = pool.Dequeue();
                }
                else if (poolPrefabs.TryGetValue(prefabPath, out GameObject prefab))
                {
                    obj = CreatePoolObject(prefab);
                }

                if (obj != null)
                {
                    obj.SetActive(true);
                    callback?.Invoke(obj);
                }
            }
            else
            {
                InitializePool(prefabPath, config.poolInitialSize);
            }
        }

        public void ReturnToPool(string prefabPath, GameObject obj)
        {
            if (obj != null)
            {
                obj.SetActive(false);
                if (objectPools.TryGetValue(prefabPath, out Queue<GameObject> pool))
                {
                    pool.Enqueue(obj);
                }
            }
        }
        #endregion

        #region 缓存管理
        private IEnumerator CleanupCache()
        {
            while (true)
            {
                yield return new WaitForSeconds(60f); // 每分钟检查一次

                List<string> keysToRemove = new List<string>();
                float currentTime = Time.time;

                foreach (var kvp in lastAccessTime)
                {
                    if (currentTime - kvp.Value > config.cacheTimeout && 
                        referenceCount[kvp.Key] <= 0)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (string key in keysToRemove)
                {
                    // 根据资源类型选择合适的卸载方式
                    Object asset = resourceCache[key];
                    if (asset != null)
                    {
                        if (asset is GameObject)
                        {
                            // GameObject预制体不需要手动卸载，会在场景卸载时自动处理
                            resourceCache.Remove(key);
                        }
                        else if (asset is Material || asset is Texture || asset is Mesh)
                        {
                            // 对于材质、贴图、网格等资源，使用UnloadAsset
                            Resources.UnloadAsset(asset);
                            resourceCache.Remove(key);
                        }
                    }
                    
                    lastAccessTime.Remove(key);
                    referenceCount.Remove(key);
                }

                // 强制垃圾回收
                if (keysToRemove.Count > 0)
                {
                    Resources.UnloadUnusedAssets();
                }
            }
        }
        #endregion

        #region 引用计数
        public void AddReference(string path)
        {
            if (referenceCount.ContainsKey(path))
            {
                referenceCount[path]++;
            }
        }

        public void RemoveReference(string path)
        {
            if (referenceCount.ContainsKey(path))
            {
                referenceCount[path]--;
            }
        }
        #endregion

        #region 系统更新
        public void UpdateSystem()
        {
            // 可以添加额外的更新逻辑
        }
        #endregion
    }
}
