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
            public bool showLODInfo = true;          // 显示LOD信息
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
            var lodConfig = chunkSystem.lodConfig;
            
            if (lodConfig == null) return;
            
            // 获取当前LOD级别的区块大小
            float currentChunkSize = lodConfig.GetChunkSizeAtLOD(chunkSystem.currentLODLevel);

            // 绘制所有已加载的区块（使用蓝色）
            foreach (var chunkPos in  chunkSystem.GetLoadedChunks())
            {
                var chunk = chunkSystem.GetChunk(chunkPos);
                if (chunk != null)
                {
                    Vector3 worldPos = new Vector3(
                        chunkPos.x * currentChunkSize,
                        0,
                        chunkPos.y * currentChunkSize
                    );

                    // 使用蓝色绘制已加载区块
                    Gizmos.color = new Color(0, 0, 1, 0.3f); // 蓝色，半透明
                    Vector3 size = new Vector3(currentChunkSize, config.boundsHeight * 2, currentChunkSize);
                    Gizmos.DrawCube(worldPos + new Vector3(currentChunkSize/2, config.boundsHeight, currentChunkSize/2), size);
                }
            }

            // 绘制当前可见范围内的所有区块
            foreach (var chunkPos in visibleChunks)
            {
                Vector3 worldPos = new Vector3(
                    chunkPos.x * currentChunkSize,
                    0,
                    chunkPos.y * currentChunkSize
                );

                bool isLoaded = chunkSystem.IsChunkLoaded(chunkPos);
                
                // 绘制区块边界
                if (config.showChunkBounds)
                {
                    // 绘制区块底面
                    Gizmos.color = isLoaded ? config.activeChunkColor : config.inactiveChunkColor;
                    Vector3 size = new Vector3(currentChunkSize, config.boundsHeight, currentChunkSize);
                    Gizmos.DrawCube(worldPos + new Vector3(currentChunkSize/2, 0, currentChunkSize/2), size);
                    
                    // 绘制区块边界线
                    Gizmos.color = config.boundaryLineColor;
                    float height = config.boundaryLineHeight;
                    
                    // 绘制竖直边界线（四个角的立柱）
                    DrawChunkBoundaryLines(worldPos, currentChunkSize, height);
                }

                // 绘制区块编号和LOD信息
                if (config.showChunkNumbers)
                {
                    DrawChunkLabels(worldPos, chunkPos, currentChunkSize, isLoaded);
                }

                // 绘制当前中心区块的特殊标记
                if (chunkPos == currentChunk)
                {
                    DrawCenterChunkMarker(worldPos, currentChunkSize);
                }
            }
        }

        private void DrawChunkBoundaryLines(Vector3 worldPos, float currentChunkSize, float height)
        {
            // 绘制竖直边界线（四个角的立柱）
            // 左前角
            Gizmos.DrawLine(worldPos, worldPos + Vector3.up * height);
            // 右前角
            Gizmos.DrawLine(worldPos + new Vector3(currentChunkSize, 0, 0), 
                            worldPos + new Vector3(currentChunkSize, height, 0));
            // 左后角
            Gizmos.DrawLine(worldPos + new Vector3(0, 0, currentChunkSize), 
                            worldPos + new Vector3(0, height, currentChunkSize));
            // 右后角
            Gizmos.DrawLine(worldPos + new Vector3(currentChunkSize, 0, currentChunkSize), 
                            worldPos + new Vector3(currentChunkSize, height, currentChunkSize));
            
            // 绘制地面边界线
            DrawGroundBoundaryLines(worldPos, currentChunkSize);
            
            // 绘制顶部边界线
            DrawTopBoundaryLines(worldPos, currentChunkSize, height);
        }

        private void DrawGroundBoundaryLines(Vector3 worldPos, float currentChunkSize)
        {
            // 底边
            Gizmos.DrawLine(worldPos, worldPos + new Vector3(currentChunkSize, 0, 0));
            // 左边
            Gizmos.DrawLine(worldPos, worldPos + new Vector3(0, 0, currentChunkSize));
            // 右边
            Gizmos.DrawLine(worldPos + new Vector3(currentChunkSize, 0, 0), 
                            worldPos + new Vector3(currentChunkSize, 0, currentChunkSize));
            // 顶边
            Gizmos.DrawLine(worldPos + new Vector3(0, 0, currentChunkSize), 
                            worldPos + new Vector3(currentChunkSize, 0, currentChunkSize));
        }

        private void DrawTopBoundaryLines(Vector3 worldPos, float currentChunkSize, float height)
        {
            // 底边
            Gizmos.DrawLine(worldPos + Vector3.up * height, 
                            worldPos + new Vector3(currentChunkSize, height, 0));
            // 左边
            Gizmos.DrawLine(worldPos + Vector3.up * height, 
                            worldPos + new Vector3(0, height, currentChunkSize));
            // 右边
            Gizmos.DrawLine(worldPos + new Vector3(currentChunkSize, height, 0), 
                            worldPos + new Vector3(currentChunkSize, height, currentChunkSize));
            // 顶边
            Gizmos.DrawLine(worldPos + new Vector3(0, height, currentChunkSize), 
                            worldPos + new Vector3(currentChunkSize, height, currentChunkSize));
        }

        private void DrawChunkLabels(Vector3 worldPos, Vector2Int chunkPos, float currentChunkSize, bool isLoaded)
        {
#if UNITY_EDITOR
            string label = $"Chunk_{chunkPos.x}_{chunkPos.y}\n" +
                            $"Loaded: {isLoaded}\n" +
                            (config.showLODInfo ? $"LOD: {chunkSystem.currentLODLevel}\n" +
                                                $"Size: {currentChunkSize}m" : "");
            
            GUIStyle style = new GUIStyle();
            style.normal.textColor = config.textColor;
            style.alignment = TextAnchor.MiddleCenter;
            
            Vector3 labelPos = worldPos + new Vector3(currentChunkSize/2, 1, currentChunkSize/2);
            UnityEditor.Handles.Label(labelPos, label, style);
#endif
        }

        private void DrawCenterChunkMarker(Vector3 worldPos, float currentChunkSize)
        {
            Gizmos.color = Color.yellow;
            Vector3 centerPos = worldPos + new Vector3(currentChunkSize/2, 1, currentChunkSize/2);
            // 绘制十字标记
            float crossSize = currentChunkSize * 0.1f;
            Gizmos.DrawLine(centerPos - new Vector3(crossSize, 0, 0), 
                            centerPos + new Vector3(crossSize, 0, 0));
            Gizmos.DrawLine(centerPos - new Vector3(0, 0, crossSize), 
                            centerPos + new Vector3(0, 0, crossSize));
            // 绘制圆圈
            DrawWireCircle(centerPos, crossSize);
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

            // 计算右侧显示区域
            float rightMargin = 10f;
            float width = 300f;
            float rightPosition = Screen.width - width - rightMargin;
            
            GUILayout.BeginArea(new Rect(rightPosition, 10, width, 200));
            
            // 设置GUI样式
            GUIStyle redStyle = new GUIStyle(GUI.skin.label);
            redStyle.normal.textColor = Color.red;
            redStyle.fontSize = 20;  // 设置字体大小
            redStyle.fontStyle = FontStyle.Bold;  // 设置字体为粗体
            redStyle.padding = new RectOffset(5, 5, 5, 5);  // 设置内边距
            redStyle.margin = new RectOffset(0, 0, 10, 10); // 设置外边距，增加行间距
            
            GUILayout.Label($"Loaded Chunks: {chunkSystem.GetLoadedChunkCount()}", redStyle);
            GUILayout.Label($"Current Chunk: {chunkSystem.GetCurrentCenterChunk()}", redStyle);
            GUILayout.Label($"Current LOD: {chunkSystem.currentLODLevel}", redStyle);
            GUILayout.Label($"Chunk Size: {chunkSystem.lodConfig.GetChunkSizeAtLOD(chunkSystem.currentLODLevel)}m", redStyle);
            
            // 添加相机高度信息
            if (Camera.main != null)
            {
                GUILayout.Label($"Camera Height: {Camera.main.transform.position.y:F1}m", redStyle);
            }
            
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
                "黄色圆圈: 当前中心区块\n" +
                "LOD信息: 显示当前区块的LOD级别和大小", 
                MessageType.Info);
        }
    }
#endif
} 