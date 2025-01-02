using UnityEngine;
using System;

namespace SURender.InfinityScale
{
    [CreateAssetMenu(fileName = "ChunkLODConfig", menuName = "InfinityScale/Chunk LOD Config")]
    public class ChunkLODConfig : ScriptableObject
    {
        [Serializable]
        public class LODLevel
        {
            public float distance;           // 触发此LOD级别的距离
            public float chunkSizeMultiplier;// Chunk大小倍数
            public int viewDistance;         // 此LOD级别的可视距离
            public bool useInstancing;       // 是否使用实例化渲染
            [Range(0, 1)]
            public float detailLevel;        // 细节级别（0-1）
            
            // 编辑器中显示实际覆盖范围
            public string GetCoverageInfo(float baseChunkSize)
            {
                float actualChunkSize = baseChunkSize * chunkSizeMultiplier;
                float coverageRadius = actualChunkSize * viewDistance;
                int totalChunks = (viewDistance * 2 + 1) * (viewDistance * 2 + 1);
                return $"覆盖半径: {coverageRadius}m, 区块数: {totalChunks}, 单个区块大小: {actualChunkSize}m";
            }
        }

        public float baseChunkSize = 100f;   // 基础Chunk大小
        public LODLevel[] lodLevels;         // LOD级别配置
        public float transitionDuration = 1f; // LOD切换过渡时间

        private void OnValidate()
        {
            // 确保LOD级别按距离从小到大排序
            if (lodLevels != null && lodLevels.Length > 1)
            {
                Array.Sort(lodLevels, (a, b) => a.distance.CompareTo(b.distance));
            }
        }
        
        public int GetLODLevelAtDistance(float distance)
        {
            // 注意：现在是从低到高遍历，因为我们希望在近距离时使用较小的区块
            for (int i = 0; i < lodLevels.Length; i++)
            {
                if (distance < lodLevels[i].distance)
                {
                    return i > 0 ? i - 1 : 0;
                }
            }
            return lodLevels.Length - 1; // 如果超过所有距离，使用最高级别
        }
        
        public float GetChunkSizeAtLOD(int lodLevel)
        {
            if (lodLevel >= 0 && lodLevel < lodLevels.Length)
            {
                return baseChunkSize * lodLevels[lodLevel].chunkSizeMultiplier;
            }
            return baseChunkSize;
        }
        
        public int GetViewDistanceAtLOD(int lodLevel)
        {
            if (lodLevel >= 0 && lodLevel < lodLevels.Length)
            {
                return lodLevels[lodLevel].viewDistance;
            }
            return 1;
        }
        
        public bool ShouldUseInstancingAtLOD(int lodLevel)
        {
            if (lodLevel >= 0 && lodLevel < lodLevels.Length)
            {
                return lodLevels[lodLevel].useInstancing;
            }
            return false;
        }
        
        public float GetDetailLevelAtLOD(int lodLevel)
        {
            if (lodLevel >= 0 && lodLevel < lodLevels.Length)
            {
                return lodLevels[lodLevel].detailLevel;
            }
            return 1f;
        }

        // 获取指定LOD级别的实际覆盖范围
        public float GetCoverageRadius(int lodLevel)
        {
            if (lodLevel >= 0 && lodLevel < lodLevels.Length)
            {
                float chunkSize = GetChunkSizeAtLOD(lodLevel);
                int viewDist = GetViewDistanceAtLOD(lodLevel);
                return chunkSize * viewDist;
            }
            return baseChunkSize;
        }
    }
} 