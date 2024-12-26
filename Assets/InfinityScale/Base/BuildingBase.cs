using UnityEngine;
using System.Collections.Generic;

namespace SURender.InfinityScale
{


    #region 建筑基类
    public class BuildingBase : MonoBehaviour
    {
        #region 序列化字段
        [SerializeField] protected LODGroup lodGroup;
        [SerializeField] protected MeshRenderer[] renderers;
        [SerializeField] protected bool useInstancing = false;
        #endregion

        #region 属性
        public string BuildingId { get; private set; }
        public BuildingType BuildingType { get; private set; }
        public bool IsInnerCity { get; private set; }
        public bool IsVisible { get; private set; }
        public int CurrentLODLevel { get; private set; }
        public BuildingData BuildingData { get; private set; }
        #endregion

        #region 私有字段
        protected MaterialPropertyBlock propertyBlock;
        protected float currentVisibility = 1f;
        protected bool isInitialized = false;
        #endregion

        #region 初始化
        public virtual void Initialize(BuildingData data)
        {
            BuildingData = data;
            BuildingId = data.buildingId;
            BuildingType = data.buildingType;
            IsInnerCity = data.isInnerCity;
            
            // 初始化组件引用
            if (lodGroup == null) lodGroup = GetComponent<LODGroup>();
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<MeshRenderer>();
            
            propertyBlock = new MaterialPropertyBlock();
            
            // 设置初始状态
            transform.position = data.position;
            transform.rotation = data.rotation;
            transform.localScale = data.scale;
            
            UpdateLOD(data.lodLevel);
            isInitialized = true;
        }
        #endregion

        #region 更新方法
        public virtual void UpdateVisibility(float alpha)
        {
            currentVisibility = alpha;
            bool shouldBeVisible = alpha > 0;
            
            if (gameObject.activeSelf != shouldBeVisible)
            {
                gameObject.SetActive(shouldBeVisible);
                Debug.Log($"Building {BuildingId} visibility changed to {shouldBeVisible}, Type: {BuildingType}, IsInnerCity: {IsInnerCity}");
            }
            
            // 如果可见，则更新材质透明度
            if (shouldBeVisible && renderers != null)
            {
                foreach (var renderer in renderers)
                {
                    if (renderer != null)
                    {
                        propertyBlock.SetFloat("_Alpha", alpha);
                        renderer.SetPropertyBlock(propertyBlock);
                    }
                }
            }
            
            IsVisible = shouldBeVisible;
        }

        public virtual void UpdateLOD(int level)
        {
            if (level == CurrentLODLevel) return;
            
            CurrentLODLevel = level;
            if (lodGroup != null)
            {
                lodGroup.ForceLOD(level);
            }
            
            OnLODChanged(level);
        }

        public virtual void UpdateState(InfinityScaleManager.ScaleState state)
        {
            // 根据缩放状态更新建筑
            switch (state)
            {
                case InfinityScaleManager.ScaleState.InnerCity:
                    if (IsInnerCity)
                    {
                        Show();
                        Debug.Log($"Showing inner city building {BuildingId}");
                    }
                    else
                    {
                        Hide();
                        Debug.Log($"Hiding outer city building {BuildingId}");
                    }
                    break;
                    
                case InfinityScaleManager.ScaleState.OuterCity:
                    if (!IsInnerCity)
                    {
                        Show();
                        Debug.Log($"Showing outer city building {BuildingId}");
                    }
                    else
                    {
                        Hide();
                        Debug.Log($"Hiding inner city building {BuildingId}");
                    }
                    break;
                    
                case InfinityScaleManager.ScaleState.Transition:
                    // 在过渡状态下，根据当前高度决定可见性
                    float currentHeight = InfinityScaleManager.Instance.GetCurrentHeight();
                    float alpha = IsInnerCity ? 
                        Mathf.InverseLerp(500f, 300f, currentHeight) : // 内城建筑淡出
                        Mathf.InverseLerp(300f, 500f, currentHeight);  // 外城建筑淡入
                    UpdateVisibility(alpha);
                    break;
            }
        }
        #endregion

        #region 保护方法
        protected virtual void Show()
        {
            UpdateVisibility(1f);
        }

        protected virtual void Hide()
        {
            UpdateVisibility(0f);
        }

        protected virtual void OnLODChanged(int newLevel)
        {
            // 子类可以重写此方法以响应LOD变化
        }
        #endregion

        #region Unity回调
        protected virtual void OnDestroy()
        {
            // 清理资源
            isInitialized = false;
        }
        #endregion
    }
    #endregion
}