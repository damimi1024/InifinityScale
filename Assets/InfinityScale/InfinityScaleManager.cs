using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SURender.InfinityScale
{
// 1. 核心管理器 - 单例模式
    public class InfinityScaleManager : MonoBehaviour
    {
        public static InfinityScaleManager Instance { get; private set; }

        // 当前缩放状态
        public enum ScaleState
        {
            InnerCity, // 内城状态
            Transition, // 过渡状态
            OuterCity // 外城状态
        }

        // 缩放配置
        [System.Serializable]
        public class ScaleConfig
        {
            public float innerCityMaxHeight = 100f; // 内城最大高度
            public float transitionStartHeight = 300f; // 开始过渡高度
            public float transitionEndHeight = 700f; // 结束过渡高度
            public float outerCityMaxHeight = 2000f; // 外城最大高度
        }

        [SerializeField] private ScaleConfig scaleConfig;

        // 当前状态
        private ScaleState currentState;
        private float currentHeight;

        // 子系统引用
        private CameraSystem cameraSystem;
        private ChunkSystem chunkSystem;
        private ResourceSystem resourceSystem;
        private BuildingSystem buildingSystem;
    }
}