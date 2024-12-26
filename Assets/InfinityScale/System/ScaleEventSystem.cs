using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SURender.InfinityScale
{
// 7. 事件系统
    public class ScaleEventSystem
    {
         #region 事件定义
        // 缩放事件
        public static event Action<float> onScaleChanged;              // 缩放值改变
        public static event Action<InfinityScaleManager.ScaleState> onStateChanged;  // 状态改变
        
        // 建筑事件
        public static event Action<BuildingBase> onBuildingCreated;    // 建筑创建
        public static event Action<BuildingBase> onBuildingDestroyed;  // 建筑销毁
        public static event Action<BuildingBase, int> onBuildingLODChanged;  // LOD改变
        
        // 区块事件
        public static event Action<Vector2Int> onChunkLoaded;          // 区块加载
        public static event Action<Vector2Int> onChunkUnloaded;        // 区块卸载
        
        // 资源事件
        public static event Action<string> onResourceLoaded;           // 资源加载
        public static event Action<string> onResourceUnloaded;         // 资源卸载
        #endregion

        #region 事件触发方法
        public static void TriggerScaleChanged(float scale)
        {
            onScaleChanged?.Invoke(scale);
        }

        public static void TriggerStateChanged(InfinityScaleManager.ScaleState state)
        {
            onStateChanged?.Invoke(state);
        }

        public static void TriggerBuildingCreated(BuildingBase building)
        {
            onBuildingCreated?.Invoke(building);
        }

        public static void TriggerBuildingDestroyed(BuildingBase building)
        {
            onBuildingDestroyed?.Invoke(building);
        }

        public static void TriggerBuildingLODChanged(BuildingBase building, int newLOD)
        {
            onBuildingLODChanged?.Invoke(building, newLOD);
        }

        public static void TriggerChunkLoaded(Vector2Int chunkPos)
        {
            onChunkLoaded?.Invoke(chunkPos);
        }

        public static void TriggerChunkUnloaded(Vector2Int chunkPos)
        {
            onChunkUnloaded?.Invoke(chunkPos);
        }

        public static void TriggerResourceLoaded(string resourcePath)
        {
            onResourceLoaded?.Invoke(resourcePath);
        }

        public static void TriggerResourceUnloaded(string resourcePath)
        {
            onResourceUnloaded?.Invoke(resourcePath);
        }
        #endregion

        #region 清理方法
        public static void ClearAllEvents()
        {
            onScaleChanged = null;
            onStateChanged = null;
            onBuildingCreated = null;
            onBuildingDestroyed = null;
            onBuildingLODChanged = null;
            onChunkLoaded = null;
            onChunkUnloaded = null;
            onResourceLoaded = null;
            onResourceUnloaded = null;
        }
        #endregion
    }
}