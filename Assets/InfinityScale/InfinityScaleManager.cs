using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SURender.InfinityScale
{
    public class InfinityScaleManager : MonoBehaviour
    {
        public static InfinityScaleManager Instance { get; private set; }

        #region 状态定义
        public enum ScaleState
        {
            InnerCity,   // 内城状态
            Transition,  // 过渡状态
            OuterCity    // 外城状态
        }

        [System.Serializable]
        public class ScaleConfig
        {
            public float innerCityMaxHeight = 100f;     // 内城最大高度
            public float transitionStartHeight = 300f;  // 开始过渡高度
            public float transitionEndHeight = 700f;    // 结束过渡高度
            public float outerCityMaxHeight = 2000f;    // 外城最大高度
            public float zoomSpeed = 1.0f;             // 缩放速度
            public float smoothTime = 0.3f;            // 平滑过渡时间
        }
        #endregion

        #region 序列化字段
        [SerializeField] private ScaleConfig scaleConfig;
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Transform cameraTarget;
        #endregion

        #region 私有字段
        private ScaleState currentState;
        private float currentHeight;
        private float targetHeight;
        private Vector3 velocity = Vector3.zero;
        private bool isTransitioning;

        // 缓存的组件引用
        private CameraSystem cameraSystem;
        private ChunkSystem chunkSystem;
        private ResourceSystem resourceSystem;
        private BuildingSystem buildingSystem;

        // 内外城切换阈值
        private float innerToOuterThreshold;
        private float outerToInnerThreshold;

        // 建筑显示控制
        private Dictionary<BuildingType, float> buildingVisibilityThresholds;
        #endregion

        #region Unity生命周期
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeSystems();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            InitializeThresholds();
            SetInitialState();
        }

        private void Update()
        {
            HandleInput();
            UpdateScale();
            UpdateSystems();
        }
        #endregion

        #region 初始化方法
        private void InitializeSystems()
        {
            // 获取或添加必要的组件
            cameraSystem = GetOrAddComponent<CameraSystem>();
            chunkSystem = GetOrAddComponent<ChunkSystem>();
            resourceSystem = GetOrAddComponent<ResourceSystem>();
            buildingSystem = GetOrAddComponent<BuildingSystem>();

            // 初始化建筑显示阈值字典
            buildingVisibilityThresholds = new Dictionary<BuildingType, float>();
        }

        private T GetOrAddComponent<T>() where T : Component
        {
            T component = GetComponent<T>();
            if (component == null)
            {
                component = gameObject.AddComponent<T>();
            }
            return component;
        }

        private void InitializeThresholds()
        {
            innerToOuterThreshold = scaleConfig.transitionStartHeight;
            outerToInnerThreshold = scaleConfig.transitionEndHeight;
        }

        private void SetInitialState()
        {
            currentState = ScaleState.InnerCity;
            currentHeight = scaleConfig.innerCityMaxHeight;
            targetHeight = currentHeight;
            UpdateCameraPosition(currentHeight);
        }
        #endregion

        #region 缩放控制
        private void HandleInput()
        {
            if (isTransitioning) return;

            float scrollDelta = Input.GetAxis("Mouse ScrollWheel");
            if (scrollDelta != 0)
            {
                float targetDelta = -scrollDelta * scaleConfig.zoomSpeed * 100f;
                targetHeight = Mathf.Clamp(currentHeight + targetDelta,
                    scaleConfig.innerCityMaxHeight,
                    scaleConfig.outerCityMaxHeight);
            }
        }

        private void UpdateScale()
        {
            // 平滑更新高度
            currentHeight = Mathf.SmoothDamp(currentHeight, targetHeight, 
                ref velocity.y, scaleConfig.smoothTime);

            // 更新相机位置
            UpdateCameraPosition(currentHeight);

            // 更新场景状态
            UpdateSceneState();

            // 更新建筑可见性
            UpdateBuildingVisibility();
        }

        private void UpdateCameraPosition(float height)
        {
            if (mainCamera == null || cameraTarget == null) return;

            Vector3 targetPosition = cameraTarget.position;
            targetPosition.y = height;
            mainCamera.transform.position = targetPosition;
        }

        private void UpdateSceneState()
        {
            ScaleState newState = currentState;

            if (currentHeight <= scaleConfig.innerCityMaxHeight)
            {
                newState = ScaleState.InnerCity;
            }
            else if (currentHeight >= scaleConfig.outerCityMaxHeight)
            {
                newState = ScaleState.OuterCity;
            }
            else
            {
                newState = ScaleState.Transition;
            }

            if (newState != currentState)
            {
                OnStateChanged(newState);
            }
        }

        private void OnStateChanged(ScaleState newState)
        {
            ScaleState oldState = currentState;
            currentState = newState;

            // 触发状态改变事件
            ScaleEventSystem.TriggerStateChanged(currentState);

            // 处理状态切换逻辑
            HandleStateTransition(oldState, newState);
        }

        private void HandleStateTransition(ScaleState oldState, ScaleState newState)
        {
            isTransitioning = true;
            StartCoroutine(TransitionCoroutine(oldState, newState));
        }

        private IEnumerator TransitionCoroutine(ScaleState oldState, ScaleState newState)
        {
            float transitionProgress = 0f;
            float transitionDuration = 1.0f; // 可配置的过渡时间

            while (transitionProgress < 1f)
            {
                transitionProgress += Time.deltaTime / transitionDuration;
                float t = scaleCurve.Evaluate(transitionProgress);

                // 更新过渡效果
                UpdateTransitionEffect(oldState, newState, t);

                yield return null;
            }

            isTransitioning = false;
        }

        private void UpdateTransitionEffect(ScaleState oldState, ScaleState newState, float t)
        {
            // 实现具体的过渡效果，例如：
            // - 调整建筑透明度
            // - 更新LOD级别
            // - 切换渲染模式等
        }
        #endregion

        #region 建筑控制
        private void UpdateBuildingVisibility()
        {
            float normalizedHeight = Mathf.InverseLerp(
                scaleConfig.innerCityMaxHeight,
                scaleConfig.outerCityMaxHeight,
                currentHeight);

            foreach (var kvp in buildingVisibilityThresholds)
            {
                BuildingType buildingType = kvp.Key;
                float threshold = kvp.Value;
                bool shouldBeVisible = currentHeight <= threshold;

                // 计算过渡alpha值
                float alpha = shouldBeVisible ? 
                    1f - Mathf.Clamp01((currentHeight - threshold) / 100f) : 0f;

                // 更新建筑可见性
                buildingSystem.UpdateBuildingTypeVisibility(buildingType, alpha);
            }
        }

        public void RegisterBuildingType(BuildingType type, float visibilityThreshold)
        {
            buildingVisibilityThresholds[type] = visibilityThreshold;
        }
        #endregion

        #region 系统更新
        private void UpdateSystems()
        {
            // 更新各子系统
            cameraSystem?.UpdateSystem(currentHeight);
            chunkSystem?.UpdateSystem(mainCamera.transform.position);
            resourceSystem?.UpdateSystem();
            buildingSystem?.UpdateSystem(currentState);
        }
        #endregion

        #region 公共接口
        public ScaleState GetCurrentState() => currentState;
        public float GetCurrentHeight() => currentHeight;
        public bool IsTransitioning() => isTransitioning;

        public void SetTargetHeight(float height)
        {
            targetHeight = Mathf.Clamp(height,
                scaleConfig.innerCityMaxHeight,
                scaleConfig.outerCityMaxHeight);
        }

        public void ForceCameraHeight(float height)
        {
            currentHeight = height;
            targetHeight = height;
            UpdateCameraPosition(height);
            UpdateSceneState();
        }
        #endregion
    }
}