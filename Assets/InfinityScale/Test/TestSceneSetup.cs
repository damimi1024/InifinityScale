using UnityEngine;
using SURender.InfinityScale;

public class TestSceneSetup : MonoBehaviour
{
    [Header("测试建筑预制体")]
    [SerializeField] private GameObject innerCityBuildingPrefab;
    [SerializeField] private GameObject outerCityBuildingPrefab;
    
    [Header("测试配置")]
    [SerializeField] private int innerCityBuildingCount = 10;
    [SerializeField] private int outerCityBuildingCount = 50;
    [SerializeField] private float innerCityRadius = 100f;
    [SerializeField] private float outerCityRadius = 500f;

    private void Start()
    {
        // 创建测试建筑
        CreateTestBuildings();
        
        // 注册建筑类型
        RegisterBuildingTypes();
    }

    private void CreateTestBuildings()
    {
        // 创建内城建筑
        for (int i = 0; i < innerCityBuildingCount; i++)
        {
            float angle = (360f / innerCityBuildingCount) * i;
            float radius = Random.Range(0f, innerCityRadius);
            Vector3 position = GetPositionOnCircle(angle, radius);

            CreateTestBuilding(true, position);
        }

        // 创建外城建筑
        for (int i = 0; i < outerCityBuildingCount; i++)
        {
            float angle = Random.Range(0f, 360f);
            float radius = Random.Range(innerCityRadius, outerCityRadius);
            Vector3 position = GetPositionOnCircle(angle, radius);

            CreateTestBuilding(false, position);
        }
    }

    private void CreateTestBuilding(bool isInnerCity, Vector3 position)
    {
        // 选择对应的预制体
        GameObject prefab = isInnerCity ? innerCityBuildingPrefab : outerCityBuildingPrefab;
        if (prefab == null)
        {
            Debug.LogError($"Missing building prefab for {(isInnerCity ? "inner" : "outer")} city");
            return;
        }

        BuildingData buildingData = new BuildingData
        {
            buildingId = System.Guid.NewGuid().ToString(),
            prefabPath = GetPrefabPath(prefab),
            buildingType = isInnerCity ? BuildingType.InnerCityOnly : BuildingType.OuterCityOnly,
            isInnerCity = isInnerCity,
            position = position,
            rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0),
            scale = Vector3.one
        };

        // 获取ChunkSystem
        var chunkSystem = FindObjectOfType<ChunkSystem>();
        if (chunkSystem != null)
        {
            // 计算建筑所在的区块位置
            Vector2Int chunkPos = chunkSystem.WorldToChunkPosition(position);
            
            // 如果区块不存在，先创建区块
            if (!chunkSystem.IsChunkLoaded(chunkPos))
            {
                // 将建筑数据添加到BuildingSystem的待处理列表
                BuildingSystem.Instance.AddPendingBuilding(chunkPos, buildingData);
            }
            else
            {
                // 如果区块已存在，直接添加到区块中
                var chunk = chunkSystem.GetChunk(chunkPos);
                BuildingSystem.Instance.CreateBuilding(buildingData, position, chunk.Transform);
            }
        }
    }

    // 获取预制体在Resources文件夹中的路径
    private string GetPrefabPath(GameObject prefab)
    {
        // 注意：预制体必须放在Resources文件夹下
        string path = prefab.name;
        if (path.EndsWith("(Clone)"))
        {
            path = path.Substring(0, path.Length - 7);
        }
        return path;
    }

    private Vector3 GetPositionOnCircle(float angle, float radius)
    {
        float x = Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
        float z = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
        return new Vector3(x, 0, z);
    }

    private void RegisterBuildingTypes()
    {
        var scaleManager = InfinityScaleManager.Instance;
        
        // 注册内城建筑
        scaleManager.RegisterBuildingType(BuildingType.InnerCityOnly, 300f);
        
        // 注册外城建筑
        scaleManager.RegisterBuildingType(BuildingType.OuterCityOnly, 700f);
    }
}