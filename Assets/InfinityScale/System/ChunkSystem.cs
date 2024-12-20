using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace SURender.InfinityScale
{
// 3. 区块系统
    public class ChunkSystem : MonoBehaviour
    {
        // 区块数据结构
        public class Chunk
        {
            public Vector2Int position;
            public List<BuildingData> staticBuildings;
            public List<BuildingData> dynamicBuildings;
            public bool isLoaded;
            public int lodLevel;
        }

        private Dictionary<Vector2Int, Chunk> chunks;
        private QuadTree<Chunk> chunkTree;

        // 视野范围内的区块管理
        private HashSet<Vector2Int> visibleChunks;
        
    }
}
