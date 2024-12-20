using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SURender.InfinityScale
{
    
// 5. 建筑系统
    public class BuildingSystem : MonoBehaviour
    {
        // 建筑基础类
        public abstract class BuildingBase : MonoBehaviour
        {
            public string buildingId;
            public BuildingType buildingType;
            public bool isInnerCity;
        
            // LOD 控制
            protected LODGroup lodGroup;
            protected virtual void UpdateLOD(float distance) { }
        }
    
        // 建筑类型
        public enum BuildingType
        {
            Static,
            Dynamic,
            Decoration
        }
    
        // 建筑管理
        private Dictionary<string, BuildingBase> activeBuildings;
    }
}
