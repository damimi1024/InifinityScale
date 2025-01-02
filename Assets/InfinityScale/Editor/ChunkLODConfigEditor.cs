using UnityEngine;
using UnityEditor;

namespace SURender.InfinityScale
{
    [CustomEditor(typeof(ChunkLODConfig))]
    public class ChunkLODConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            ChunkLODConfig config = (ChunkLODConfig)target;

            EditorGUILayout.HelpBox(
                "LOD配置说明：\n" +
                "1. 距离从近到远配置，自动排序\n" +
                "2. 远处使用更大的区块和更多的可视距离\n" +
                "3. 区块大小倍数决定单个区块的大小\n" +
                "4. 可视距离决定可见区块的范围\n" +
                "注意：总区块数 = (viewDistance * 2 + 1)²",
                MessageType.Info);

            EditorGUILayout.Space();
            
            // 绘制基础属性
            EditorGUILayout.PropertyField(serializedObject.FindProperty("baseChunkSize"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("transitionDuration"));
            
            EditorGUILayout.Space();
            
            // 绘制LOD级别数组
            SerializedProperty lodLevelsProperty = serializedObject.FindProperty("lodLevels");
            EditorGUILayout.PropertyField(lodLevelsProperty);
            
            if (lodLevelsProperty.isExpanded && config.lodLevels != null)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < config.lodLevels.Length; i++)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField($"LOD {i} 覆盖范围信息:", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox(
                        config.lodLevels[i].GetCoverageInfo(config.baseChunkSize),
                        MessageType.None);
                }
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            
            // 添加示例配置按钮
            if (GUILayout.Button("应用示例配置"))
            {
                ApplyExampleConfig(config);
            }
        }

        private void ApplyExampleConfig(ChunkLODConfig config)
        {
            config.baseChunkSize = 10f;
            config.transitionDuration = 1f;
            
            config.lodLevels = new ChunkLODConfig.LODLevel[]
            {
                new ChunkLODConfig.LODLevel
                {
                    distance = 0f,          // 近距离
                    chunkSizeMultiplier = 1f,// 基础大小
                    viewDistance = 10,       // 视距
                    useInstancing = false,   // 近处不使用实例化
                    detailLevel = 1f         // 最高细节
                },
                new ChunkLODConfig.LODLevel
                {
                    distance = 200f,         // 中距离
                    chunkSizeMultiplier = 2f,// 2倍大小
                    viewDistance = 15,       // 视距
                    useInstancing = true,    // 使用实例化
                    detailLevel = 0.8f       // 较高细节
                },
                new ChunkLODConfig.LODLevel
                {
                    distance = 500f,         // 远距离
                    chunkSizeMultiplier = 4f,// 4倍大小
                    viewDistance = 20,       // 视距
                    useInstancing = true,    // 使用实例化
                    detailLevel = 0.6f       // 中等细节
                },
                new ChunkLODConfig.LODLevel
                {
                    distance = 1000f,        // 高空
                    chunkSizeMultiplier = 8f,// 8倍大小
                    viewDistance = 25,       // 视距
                    useInstancing = true,    // 使用实例化
                    detailLevel = 0.4f       // 较低细节
                },
                new ChunkLODConfig.LODLevel
                {
                    distance = 2000f,        // 超高空
                    chunkSizeMultiplier = 16f,// 16倍大小
                    viewDistance = 30,       // 视距
                    useInstancing = true,    // 使用实例化
                    detailLevel = 0.2f       // 最低细节
                }
            };
            
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
    }
} 