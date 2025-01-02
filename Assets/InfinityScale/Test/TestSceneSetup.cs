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
    [SerializeField] private float gridSpacing = 2f; // 网格间距

    private void Start()
    {
        // 创建网格状建筑
        CreateGridBuildings();
        
        // 注册建筑类型
        RegisterBuildingTypes();
    }

    private void CreateGridBuildings()
    {
        float range = 500f; // 生成范围为 -500 到 500
        
        // 计算在每个方向上需要生成的建筑数量
        int buildingsPerSide = Mathf.FloorToInt(range * 2 / gridSpacing);
        
        for (float x = -range; x <= range; x += gridSpacing)
        {
            for (float z = -range; z <= range; z += gridSpacing)
            {
                Vector3 position = new Vector3(x, 0, z);
                CreateTestBuilding(false, position);
            }
        }
        
        Debug.Log($"Generated buildings in grid pattern from -500 to 500 with spacing {gridSpacing}");
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
            scale = Vector3.one * Random.Range(0.8f, 1.2f) // 添加随机缩放
        };

        // 获取ChunkSystem
        var chunkSystem = FindObjectOfType<ChunkSystem>();
        if (chunkSystem != null)
        {
            // 计算建筑所在的区块位置
            Vector2Int chunkPos = chunkSystem.WorldToChunkPosition(position);
            
            // 无论区块是否加载，都添加到待处理列表
            BuildingSystem.Instance.AddPendingBuilding(chunkPos, buildingData);
            
            // 如果区块已加载，触发区块更新以处理新添加的建筑
            if (chunkSystem.IsChunkLoaded(chunkPos))
            {
                var chunk = chunkSystem.GetChunk(chunkPos);
                if (chunk != null)
                {
                    // 检查是否使用实例化渲染
                    bool useInstancing = BuildingSystem.Instance.CheckBuildingUseInstancing(buildingData);
                    if (useInstancing)
                    {
                        chunk.AddBuildingData(buildingData);
                        BuildingSystem.Instance.AddInstancedBuildingData(buildingData);
                    }
                    else
                    {
                        BuildingSystem.Instance.CreateBuilding(buildingData, position, chunk.Transform);
                    }
                }
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

    private void RegisterBuildingTypes()
    {
        var scaleManager = InfinityScaleManager.Instance;
        
        // 注册内城建筑
        scaleManager.RegisterBuildingType(BuildingType.InnerCityOnly, 300f);
        
        // 注册外城建筑
        scaleManager.RegisterBuildingType(BuildingType.OuterCityOnly, 700f);
    }
}