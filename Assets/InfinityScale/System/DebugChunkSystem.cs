using UnityEngine;
using System.Collections.Generic;
using UnityEditor;

namespace SURender.InfinityScale
{
    public class DebugChunkSystem : MonoBehaviour
    {
        [System.Serializable]
        public class DebugConfig
        {
            public bool showChunkBounds = true;       // 显示区块边界
            public bool showChunkNumbers = true;      // 显示区块编号
            public Color activeChunkColor = new Color(0, 1, 0, 0.3f);    // 激活区块颜色
            public Color inactiveChunkColor = new Color(1, 0, 0, 0.1f);  // 未激活区块颜色
            public Color boundaryLineColor = new Color(1, 1, 1, 1f);     // 边界线颜色
            public Color textColor = Color.white;     // 文字颜色
            [Range(0f, 1f)]
            public float boundsHeight = 0.1f;        // 区块边界高度
            [Range(0.1f, 5f)]
            public float boundaryLineHeight = 2f;    // 边界线高度
        }

        [SerializeField] private DebugConfig config = new DebugConfig();
        private ChunkSystem chunkSystem;

        private void OnEnable()
        {
            chunkSystem = GetComponent<ChunkSystem>();
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            if (!config.showChunkBounds && !config.showChunkNumbers) return;
            if (chunkSystem == null) return;

            var currentChunk = chunkSystem.GetCurrentCenterChunk();
            var visibleChunks = chunkSystem.GetVisibleChunks();
            var chunkSize = chunkSystem.config?.chunkSize ?? 100f;

            // 绘制当前可见范围内的所有区块
            foreach (var chunkPos in visibleChunks)
            {
                Vector3 worldPos = new Vector3(
                    chunkPos.x * chunkSize,
                    0,
                    chunkPos.y * chunkSize
                );

                bool isLoaded = chunkSystem.IsChunkLoaded(chunkPos);
                
                // 绘制区块边界
                if (config.showChunkBounds)
                {
                    // 绘制区块底面
                    Gizmos.color = isLoaded ? config.activeChunkColor : config.inactiveChunkColor;
                    Vector3 size = new Vector3(chunkSize, config.boundsHeight, chunkSize);
                    Gizmos.DrawCube(worldPos + new Vector3(chunkSize/2, 0, chunkSize/2), size);
                    
                    // 绘制区块边界线
                    Gizmos.color = config.boundaryLineColor;
                    float height = config.boundaryLineHeight;
                    
                    // 绘制竖直边界线（四个角的立柱）
                    // 左前角
                    Gizmos.DrawLine(worldPos, worldPos + Vector3.up * height);
                    // 右前角
                    Gizmos.DrawLine(worldPos + new Vector3(chunkSize, 0, 0), 
                                   worldPos + new Vector3(chunkSize, height, 0));
                    // 左后角
                    Gizmos.DrawLine(worldPos + new Vector3(0, 0, chunkSize), 
                                   worldPos + new Vector3(0, height, chunkSize));
                    // 右后角
                    Gizmos.DrawLine(worldPos + new Vector3(chunkSize, 0, chunkSize), 
                                   worldPos + new Vector3(chunkSize, height, chunkSize));
                    
                    // 绘制地面边界线
                    // 底边
                    Gizmos.DrawLine(worldPos, worldPos + new Vector3(chunkSize, 0, 0));
                    // 左边
                    Gizmos.DrawLine(worldPos, worldPos + new Vector3(0, 0, chunkSize));
                    // 右边
                    Gizmos.DrawLine(worldPos + new Vector3(chunkSize, 0, 0), 
                                   worldPos + new Vector3(chunkSize, 0, chunkSize));
                    // 顶边
                    Gizmos.DrawLine(worldPos + new Vector3(0, 0, chunkSize), 
                                   worldPos + new Vector3(chunkSize, 0, chunkSize));
                    
                    // 绘制顶部边界线
                    // 底边
                    Gizmos.DrawLine(worldPos + Vector3.up * height, 
                                   worldPos + new Vector3(chunkSize, height, 0));
                    // 左边
                    Gizmos.DrawLine(worldPos + Vector3.up * height, 
                                   worldPos + new Vector3(0, height, chunkSize));
                    // 右边
                    Gizmos.DrawLine(worldPos + new Vector3(chunkSize, height, 0), 
                                   worldPos + new Vector3(chunkSize, height, chunkSize));
                    // 顶边
                    Gizmos.DrawLine(worldPos + new Vector3(0, height, chunkSize), 
                                   worldPos + new Vector3(chunkSize, height, chunkSize));
                }

                // 绘制区块编号
                if (config.showChunkNumbers)
                {
#if UNITY_EDITOR
                    string label = $"Chunk_{chunkPos.x}_{chunkPos.y}\n" +
                                 $"Loaded: {isLoaded}";
                    
                    GUIStyle style = new GUIStyle();
                    style.normal.textColor = config.textColor;
                    style.alignment = TextAnchor.MiddleCenter;
                    
                    Vector3 labelPos = worldPos + new Vector3(chunkSize/2, 1, chunkSize/2);
                    Handles.Label(labelPos, label, style);
#endif
                }

                // 绘制当前中心区块的特殊标记
                if (chunkPos == currentChunk)
                {
                    Gizmos.color = Color.yellow;
                    Vector3 centerPos = worldPos + new Vector3(chunkSize/2, 1, chunkSize/2);
                    // 绘制十字标记
                    float crossSize = chunkSize * 0.1f;
                    Gizmos.DrawLine(centerPos - new Vector3(crossSize, 0, 0), 
                                   centerPos + new Vector3(crossSize, 0, 0));
                    Gizmos.DrawLine(centerPos - new Vector3(0, 0, crossSize), 
                                   centerPos + new Vector3(0, 0, crossSize));
                    // 绘制圆圈
                    DrawWireCircle(centerPos, crossSize);
                }
            }
        }

        private void DrawWireCircle(Vector3 center, float radius)
        {
            int segments = 32;
            float angle = 0f;
            float angleStep = 2f * Mathf.PI / segments;
            Vector3 firstPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Vector3 prevPoint = firstPoint;

            for (int i = 1; i <= segments; i++)
            {
                angle = i * angleStep;
                Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prevPoint, nextPoint);
                prevPoint = nextPoint;
            }
        }

        // 在Inspector中添加额外的调试信息
        private void OnGUI()
        {
            if (chunkSystem == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 200, 100));
            GUILayout.Label($"Loaded Chunks: {chunkSystem.GetLoadedChunkCount()}");
            GUILayout.Label($"Current Chunk: {chunkSystem.GetCurrentCenterChunk()}");
            GUILayout.EndArea();
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(DebugChunkSystem))]
    public class DebugChunkSystemEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            DebugChunkSystem debugSystem = (DebugChunkSystem)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "绿色: 已加载区块\n" +
                "红色: 未加载区块\n" +
                "黄色圆圈: 当前中心区块", 
                MessageType.Info);
        }
    }
#endif
} 