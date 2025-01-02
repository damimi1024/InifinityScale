using UnityEngine;

namespace SURender.InfinityScale
{
    public class CameraSystem : MonoBehaviour
    {
        #region 序列化字段
        [System.Serializable]
        public class CameraConfig
        {
            public float moveSpeed = 10f;            // 相机移动速度
            public float minHeight = 10f;            // 最小高度
            public float maxHeight = 2000f;          // 最大高度
            public float damping = 0.2f;             // 平滑过渡时间
            public float fixedPitch = 45f;           // 低高度时的固定俯仰角
            public float zoomSpeed = 100f;           // 缩放速度
            public float panSpeed = 0.5f;            // 平移速度系数
            
            // 动态俯仰角配置
            public AnimationCurve pitchCurve = new AnimationCurve(  // 高度到俯仰角的映射曲线
                new Keyframe(0f, 1f, 0f, 0f),       // 低高度时保持 fixedPitch
                new Keyframe(0.3f, 0.8f, -1f, -1f), // 开始缓慢过渡
                new Keyframe(0.7f, 0.3f, -1f, -1f), // 加速过渡
                new Keyframe(1f, 0f, 0f, 0f)        // 高高度时趋近90度
            );
            public bool enableDynamicPitch = true;   // 是否启用动态俯仰角
            public float pitchTransitionSpeed = 5f;  // 俯仰角过渡速度
        }

        [SerializeField] private CameraConfig config;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private bool debugMode = false;
        #endregion

        #region 私有字段
        private Vector3 targetPosition;
        private Vector3 currentVelocity;
        private bool isDragging = false;
        private Vector3 lastMousePosition;
        private float currentPitch;
        #endregion

        #region Unity生命周期
        private void Start()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            if (cameraTarget == null)
                cameraTarget = new GameObject("CameraTarget").transform;

            InitializeCamera();
        }

        private void Update()
        {
            HandleInput();
            UpdateCameraTransform();
            
            if (config.enableDynamicPitch)
            {
                UpdateCameraPitch();
            }
        }
        #endregion

        #region 初始化
        private void InitializeCamera()
        {
            targetPosition = cameraTarget.position;
            currentPitch = config.fixedPitch;
            
            // 设置初始俯仰角
            UpdateCameraPitch();
        }
        #endregion

        #region 输入处理
        private void HandleInput()
        {
            // 处理平移
            HandlePanning();
            
            // 处理缩放
            HandleZooming();
            
            // 处理键盘输入
            HandleKeyboardInput();
        }

        private void HandlePanning()
        {
            // 使用鼠标中键或左键进行平移
            if (Input.GetMouseButtonDown(2) || Input.GetMouseButtonDown(0))
            {
                isDragging = true;
                lastMousePosition = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(2) || Input.GetMouseButtonUp(0))
            {
                isDragging = false;
            }

            if (isDragging)
            {
                Vector3 delta = Input.mousePosition - lastMousePosition;
                
                // 计算相机平面上的移动
                Vector3 right = mainCamera.transform.right;
                Vector3 forward = Vector3.Cross(right, Vector3.up);
                
                // 根据当前高度调整移动速度，高度越高移动越快
                float heightFactor = Mathf.Lerp(0.5f, 2f, (mainCamera.transform.position.y - config.minHeight) / (config.maxHeight - config.minHeight));
                float moveDistance = delta.magnitude * config.panSpeed * heightFactor;
                
                Vector3 moveDirection = (-right * delta.x - forward * delta.y).normalized;
                targetPosition += moveDirection * moveDistance;
                
                lastMousePosition = Input.mousePosition;
            }
        }

        private void HandleZooming()
        {
            float scrollDelta = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scrollDelta) > 0.01f)
            {
                // 获取当前高度
                float currentHeight = mainCamera.transform.position.y;
                
                // 根据当前高度调整缩放速度，高度越高缩放越快
                float heightFactor = Mathf.Lerp(0.5f, 2f, (currentHeight - config.minHeight) / (config.maxHeight - config.minHeight));
                float zoomAmount = scrollDelta * config.zoomSpeed * heightFactor * Time.deltaTime;
                
                // 计算新的目标高度
                float newHeight = Mathf.Clamp(currentHeight - zoomAmount, config.minHeight, config.maxHeight);
                
                // 直接更新相机高度，不使用平滑过渡
                Vector3 newPosition = mainCamera.transform.position;
                newPosition.y = newHeight;
                mainCamera.transform.position = newPosition;
                targetPosition.y = newHeight;
                
                // 立即更新俯仰角
                UpdateCameraPitch();
            }
        }

        private void HandleKeyboardInput()
        {
            // WASD键盘控制
            Vector3 moveInput = new Vector3(
                Input.GetAxis("Horizontal"),
                0,
                Input.GetAxis("Vertical")
            );

            if (moveInput.magnitude > 0.1f)
            {
                // 根据相机朝向调整移动方向
                Vector3 forward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                
                Vector3 moveDirection = (forward * moveInput.z + right * moveInput.x).normalized;
                targetPosition += moveDirection * config.moveSpeed * Time.deltaTime;
            }
        }
        #endregion

        #region 相机更新
        private void UpdateCameraTransform()
        {
            // 平滑更新位置
            cameraTarget.position = Vector3.SmoothDamp(
                cameraTarget.position,
                targetPosition,
                ref currentVelocity,
                config.damping
            );

            // 只更新XZ平面的位置，保持Y轴高度
            Vector3 cameraPosition = cameraTarget.position;
            cameraPosition.y = mainCamera.transform.position.y;
            mainCamera.transform.position = cameraPosition;
        }

        private void UpdateCameraPitch()
        {
            if (mainCamera == null) return;

            float height = mainCamera.transform.position.y;
            float normalizedHeight = Mathf.Clamp01((height - config.minHeight) / (config.maxHeight - config.minHeight));
            
            // 使用曲线计算插值因子
            float t = config.pitchCurve.Evaluate(normalizedHeight);
            
            // 在固定俯仰角和90度之间插值
            float targetPitch = Mathf.Lerp(90f, config.fixedPitch, t);
            
            // 直接设置目标俯仰角
            currentPitch = targetPitch;
            
            // 创建一个新的旋转，只设置X轴（俯仰角）
            Quaternion targetRotation = Quaternion.Euler(currentPitch, mainCamera.transform.eulerAngles.y, 0);
            
            // 应用旋转
            mainCamera.transform.rotation = targetRotation;

            if (debugMode)
            {
                Debug.Log($"Height: {height:F1}, Normalized: {normalizedHeight:F2}, T: {t:F2}, Pitch: {currentPitch:F1}");
            }
        }
        #endregion

        #region 公共接口
        public void UpdateSystem(float height)
        {
            // 直接更新相机高度
            Vector3 newPosition = mainCamera.transform.position;
            height = Mathf.Clamp(height, config.minHeight, config.maxHeight);
            newPosition.y = height;
            mainCamera.transform.position = newPosition;
            targetPosition.y = height;
            
            // 立即更新俯仰角
            if (config.enableDynamicPitch)
            {
                UpdateCameraPitch();
            }
        }

        public void SetPosition(Vector3 position)
        {
            targetPosition = position;
            targetPosition.y = mainCamera.transform.position.y; // 保持当前高度
        }

        public void FocusOn(Vector3 position)
        {
            targetPosition = position;
            targetPosition.y = mainCamera.transform.position.y; // 保持当前高度
        }

        public Vector3 GetLookDirection()
        {
            return mainCamera.transform.forward;
        }

        public Ray GetMouseRay()
        {
            return mainCamera.ScreenPointToRay(Input.mousePosition);
        }
        #endregion

        #region 调试
        private void OnDrawGizmos()
        {
            if (!debugMode || mainCamera == null) return;

            Vector3 position = mainCamera.transform.position;
            Vector3 forward = mainCamera.transform.forward;
            
            // 绘制相机朝向
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(position, forward * 10f);
            
            // 绘制垂直参考线
            Gizmos.color = Color.green;
            Gizmos.DrawRay(position, Vector3.down * 10f);
        }

#if UNITY_EDITOR
        // 在Inspector中添加曲线预览
        [UnityEditor.CustomEditor(typeof(CameraSystem))]
        public class CameraSystemEditor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();

                CameraSystem system = (CameraSystem)target;
                if (system.config.enableDynamicPitch)
                {
                    UnityEditor.EditorGUILayout.Space();
                    UnityEditor.EditorGUILayout.HelpBox(
                        "Pitch Curve Guide:\n" +
                        "X轴: 0 = minHeight, 1 = maxHeight\n" +
                        "Y轴: 0 = 90度(垂直), 1 = fixedPitch\n" +
                        "调整曲线来控制相机角度的过渡效果",
                        UnityEditor.MessageType.Info);
                }
            }
        }
#endif
        #endregion
    }
}