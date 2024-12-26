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
            public float fixedPitch = 45f;           // 固定俯视角度
            public float zoomSpeed = 100f;           // 缩放速度
            public float panSpeed = 0.5f;            // 平移速度系数
        }

        [SerializeField] private CameraConfig config;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Transform cameraTarget;
        #endregion

        #region 私有字段
        private Vector3 targetPosition;
        private Vector3 currentVelocity;
        private bool isDragging = false;
        private Vector3 lastMousePosition;
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
        }
        #endregion

        #region 初始化
        private void InitializeCamera()
        {
            targetPosition = cameraTarget.position;
            
            // 设置固定的俯视角度
            mainCamera.transform.rotation = Quaternion.Euler(config.fixedPitch, 0, 0);
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
                // Debug.Log(zoomAmount);
                // 计算新的目标高度
                float newHeight = Mathf.Clamp(currentHeight - zoomAmount, config.minHeight, config.maxHeight);
                
                // 更新相机高度
                UpdateSystem(newHeight);
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

            // 保持固定的相机角度和高度
            Vector3 cameraPosition = cameraTarget.position;
            cameraPosition.y = mainCamera.transform.position.y; // 保持当前高度
            mainCamera.transform.position = cameraPosition;
        }
        #endregion

        #region 公共接口
        public void UpdateSystem(float height)
        {
            // 更新相机高度
            Vector3 newPosition = mainCamera.transform.position;
            height = Mathf.Clamp(height, config.minHeight, config.maxHeight);
            newPosition.y = height;
            mainCamera.transform.position = newPosition;
            
            // 更新目标位置的高度
            targetPosition.y = height;
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
    }
}