using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SURender.InfinityScale
{
    
// 2. 相机系统
    public class CameraSystem : MonoBehaviour
    {
        [System.Serializable]
        public class CameraConfig
        {
            public float zoomSpeed = 1f;
            public float moveSpeed = 10f;
            public float rotateSpeed = 100f;
            public float smoothTime = 0.3f;
        }
    
        [SerializeField] private CameraConfig config;
        private Camera mainCamera;
        private Vector3 targetPosition;
        private float targetHeight;
    }
}