using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SURender.InfinityScale
{
// 6. LOD系统
    public class LODSystem : MonoBehaviour
    {
        [System.Serializable]
        public class LODLevel
        {
            public float distance;
            public float detailLevel;
            public bool useImpostor;
        }

        [SerializeField] private LODLevel[] lodLevels;

        // LOD切换事件
        public event System.Action<int> onLODChanged;
    }
}
