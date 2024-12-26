using UnityEngine;
using System.Collections.Generic;

namespace SURender.InfinityScale
{
    public class DebugSystem : MonoBehaviour
    {
        #region 序列化字段
        [System.Serializable]
        public class DebugConfig
        {
            public bool showDebugInfo = true;       // 显示调试信息
            public bool showPerformanceInfo = true; // 显示性能信息
            public bool showChunkBounds = true;     // 显示区块边界
            public bool showLODLevels = true;       // 显示LOD级别
            public Color chunkBoundsColor = Color.yellow;
            public Color lodBoundsColor = Color.green;
        }

        [SerializeField] private DebugConfig config;
        #endregion

        #region 私有字段
        private float updateInterval = 0.5f;        // 更新间隔
        private float lastUpdateTime;
        private Dictionary<string, string> debugInfo;
        private List<PerformanceMetric> performanceMetrics;
        #endregion

        #region 性能指标
        private class PerformanceMetric
        {
            public string name;
            public float value;
            public float min;
            public float max;
            public Queue<float> history;

            public PerformanceMetric(string name)
            {
                this.name = name;
                history = new Queue<float>();
            }

            public void AddSample(float sample)
            {
                value = sample;
                min = Mathf.Min(min, sample);
                max = Mathf.Max(max, sample);
                
                history.Enqueue(sample);
                if (history.Count > 100)
                    history.Dequeue();
            }
        }
        #endregion

        #region Unity生命周期
        private void Awake()
        {
            debugInfo = new Dictionary<string, string>();
            performanceMetrics = new List<PerformanceMetric>();
            InitializeMetrics();
        }

        private void Update()
        {
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateMetrics();
                lastUpdateTime = Time.time;
            }
        }

        private void OnGUI()
        {
            if (!config.showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, Screen.height - 20));
            
            // 显示调试信息
            foreach (var info in debugInfo)
            {
                GUILayout.Label($"{info.Key}: {info.Value}");
            }

            // 显示性能信息
            if (config.showPerformanceInfo)
            {
                GUILayout.Space(20);
                foreach (var metric in performanceMetrics)
                {
                    GUILayout.Label($"{metric.name}: {metric.value:F2} (Min: {metric.min:F2}, Max: {metric.max:F2})");
                }
            }

            GUILayout.EndArea();
        }
        #endregion

        #region 调试方法
        public void SetDebugInfo(string key, string value)
        {
            debugInfo[key] = value;
        }

        public void AddPerformanceMetric(string name, float value)
        {
            var metric = performanceMetrics.Find(m => m.name == name);
            if (metric == null)
            {
                metric = new PerformanceMetric(name);
                performanceMetrics.Add(metric);
            }
            metric.AddSample(value);
        }

        private void InitializeMetrics()
        {
            AddPerformanceMetric("FPS", 0);
            AddPerformanceMetric("Memory Usage (MB)", 0);
            AddPerformanceMetric("Active Buildings", 0);
            AddPerformanceMetric("Loaded Chunks", 0);
        }

        private void UpdateMetrics()
        {
            // 更新FPS
            AddPerformanceMetric("FPS", 1.0f / Time.deltaTime);

            // 更新内存使用
            float memoryUsage = System.GC.GetTotalMemory(false) / (1024f * 1024f);
            AddPerformanceMetric("Memory Usage (MB)", memoryUsage);

            // 更新其他指标
            UpdateSystemMetrics();
        }

        private void UpdateSystemMetrics()
        {
            // 获取建筑数量
            var buildingSystem = FindObjectOfType<BuildingSystem>();
            if (buildingSystem != null)
            {
                // 这里需要在BuildingSystem中添加获取活动建筑数量的方法
                // AddPerformanceMetric("Active Buildings", buildingSystem.GetActiveBuildingCount());
            }

            // 获取加载的区块数量
            var chunkSystem = FindObjectOfType<ChunkSystem>();
            if (chunkSystem != null)
            {
                // 这里需要在ChunkSystem中添加获取加载区块数量的方法
                // AddPerformanceMetric("Loaded Chunks", chunkSystem.GetLoadedChunkCount());
            }
        }
        #endregion

        #region Gizmos绘制
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            if (config.showChunkBounds)
            {
                DrawChunkBounds();
            }

            if (config.showLODLevels)
            {
                DrawLODBounds();
            }
        }

        private void DrawChunkBounds()
        {
            var chunkSystem = FindObjectOfType<ChunkSystem>();
            if (chunkSystem != null)
            {
                // 这里需要在ChunkSystem中添加获取可见区块的方法
                // foreach (var chunk in chunkSystem.GetVisibleChunks())
                // {
                //     Gizmos.color = config.chunkBoundsColor;
                //     // 绘制区块边界
                // }
            }
        }

        private void DrawLODBounds()
        {
            var buildingSystem = FindObjectOfType<BuildingSystem>();
            if (buildingSystem != null)
            {
                // 这里需要在BuildingSystem中添加获取活动建筑的方法
                // foreach (var building in buildingSystem.GetActiveBuildings())
                // {
                //     Gizmos.color = config.lodBoundsColor;
                //     // 绘制LOD边界
                // }
            }
        }
        #endregion
    }
}