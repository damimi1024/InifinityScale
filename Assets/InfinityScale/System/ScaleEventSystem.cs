using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SURender.InfinityScale
{
// 7. 事件系统
    public class ScaleEventSystem
    {
        // 使用InfinityScaleManager中定义的ScaleState
        public static event System.Action<InfinityScaleManager.ScaleState> onStateChanged;
        public static event System.Action<float> onScaleChanged;
        
        // 资源事件
        public static event System.Action<Vector2Int> onChunkLoaded;
        public static event System.Action<Vector2Int> onChunkUnloaded;
    }
}