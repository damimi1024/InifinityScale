using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SURender.InfinityScale{
    
    // 建筑基础类型
    public enum BuildingType
    {
        // 静态建筑
        StaticBuilding,        // 不可交互的静态建筑
        InteractiveBuilding,   // 可交互的静态建筑
        DecorativeBuilding,    // 装饰性建筑
        
        // 动态建筑
        ProductionBuilding,    // 生产建筑
        MilitaryBuilding,      // 军事建筑
        ResourceBuilding,      // 资源建筑
        
        // 特殊建筑
        InnerCityOnly,         // 仅内城建筑
        OuterCityOnly,         // 仅外城建筑
        TransitionBuilding,    // 过渡区域建筑
        
        // 功能建筑
        StorageBuilding,       // 仓储建筑
        DefenseBuilding,       // 防御建筑
        GateBuilding,          // 城门建筑
    }

    // 建筑类型系统
public class BuildingTypeSystem
{
    // 建筑类型配置
    [System.Serializable]
    public class BuildingTypeConfig
    {
        public BuildingType buildingType;
        public bool canTransition;        // 是否可以在内外城转换
        public bool needLOD;              // 是否需要LOD
        public bool isInteractive;        // 是否可交互
        public bool needInstanceRender;   // 是否需要实例化渲染
        public int maxInstanceCount;      // 最大实例化数量
        public float visibleDistance;     // 可见距离
        public float interactiveDistance; // 交互距离
    }

    // 建筑渲染模式
    public enum BuildingRenderMode
    {
        Normal,         // 普通渲染
        Instanced,     // 实例化渲染
        Impostor,      // 公告板渲染
        Simplified,    // 简化模型
        Marker         // 标记点
    }

    // 建筑状态
    [System.Flags]
    public enum BuildingState
    {
        None = 0,
        Active = 1 << 0,           // 激活状态
        Visible = 1 << 1,          // 可见状态
        Interactive = 1 << 2,       // 可交互状态
        Constructing = 1 << 3,      // 建造中
        Destroying = 1 << 4,        // 销毁中
        Transitioning = 1 << 5,     // 过渡中
        Damaged = 1 << 6,          // 受损状态
        Upgrading = 1 << 7,        // 升级中
        Working = 1 << 8,          // 工作中
        Paused = 1 << 9,           // 暂停状态
    }

    // 建筑属性
    [System.Serializable]
    public class BuildingAttributes
    {
        public string buildingName;
        public int level;
        public float health;
        public float maxHealth;
        public float constructionTime;
        public float destructionTime;
        public float transitionTime;
        public Vector3 boundSize;        // 建筑边界大小
        public List<string> tags;        // 建筑标签
        public Dictionary<string, float> customAttributes; // 自定义属性
    }

    // LOD配置
    [System.Serializable]
    public class BuildingLODConfig
    {
        public float[] distances;        // LOD切换距离
        public float[] detailLevels;     // 细节级别
        public BuildingRenderMode[] renderModes; // 对应的渲染模式
        public bool useImpostor;         // 是否使用公告板
        public float impostorDistance;   // 公告板切换距离
    }

    // 建筑类型管理器
    private static Dictionary<BuildingType, BuildingTypeConfig> typeConfigs;
    private static Dictionary<BuildingType, BuildingLODConfig> lodConfigs;

    // 初始化配置
    public static void Initialize()
    {
        typeConfigs = new Dictionary<BuildingType, BuildingTypeConfig>();
        lodConfigs = new Dictionary<BuildingType, BuildingLODConfig>();
        LoadConfigurations();
    }

    // 加载配置
    private static void LoadConfigurations()
    {
        // 这里可以从ScriptableObject或其他配置文件加载
        foreach (BuildingType type in System.Enum.GetValues(typeof(BuildingType)))
        {
            typeConfigs[type] = CreateDefaultConfig(type);
            lodConfigs[type] = CreateDefaultLODConfig(type);
        }
    }

    // 创建默认配置
    private static BuildingTypeConfig CreateDefaultConfig(BuildingType type)
    {
        BuildingTypeConfig config = new BuildingTypeConfig();
        config.buildingType = type;
        
        // 根据建筑类型设置默认配置
        switch (type)
        {
            case BuildingType.StaticBuilding:
                config.canTransition = false;
                config.needLOD = true;
                config.isInteractive = false;
                config.needInstanceRender = true;
                config.maxInstanceCount = 1000;
                config.visibleDistance = 2000f;
                config.interactiveDistance = 0f;
                break;
            case BuildingType.InnerCityOnly:
                config.canTransition = false;
                config.needLOD = true;
                config.isInteractive = true;
                config.needInstanceRender = false;
                config.maxInstanceCount = 1;
                config.visibleDistance = 1000f;
                config.interactiveDistance = 50f;
                break;
            // 添加其他类型的默认配置...
        }
        
        return config;
    }

    // 创建默认LOD配置
    private static BuildingLODConfig CreateDefaultLODConfig(BuildingType type)
    {
        BuildingLODConfig config = new BuildingLODConfig();
        
        // 根据建筑类型设置默认LOD配置
        switch (type)
        {
            case BuildingType.StaticBuilding:
                config.distances = new float[] { 100f, 500f, 1000f };
                config.detailLevels = new float[] { 1f, 0.6f, 0.3f };
                config.renderModes = new BuildingRenderMode[] 
                { 
                    BuildingRenderMode.Normal,
                    BuildingRenderMode.Simplified,
                    BuildingRenderMode.Impostor 
                };
                config.useImpostor = true;
                config.impostorDistance = 800f;
                break;
            // 添加其他类型的默认LOD配置...
        }
        
        return config;
    }

    // 获取建筑类型配置
    public static BuildingTypeConfig GetBuildingConfig(BuildingType type)
    {
        return typeConfigs.TryGetValue(type, out var config) ? config : null;
    }

    // 获取建筑LOD配置
    public static BuildingLODConfig GetLODConfig(BuildingType type)
    {
        return lodConfigs.TryGetValue(type, out var config) ? config : null;
    }

    // 判断建筑类型是否需要过渡
    public static bool NeedsTransition(BuildingType type)
    {
        return GetBuildingConfig(type)?.canTransition ?? false;
    }

    // 获取建筑渲染模式
    public static BuildingRenderMode GetRenderMode(BuildingType type, float distance)
    {
        var lodConfig = GetLODConfig(type);
        if (lodConfig == null) return BuildingRenderMode.Normal;

        for (int i = 0; i < lodConfig.distances.Length; i++)
        {
            if (distance <= lodConfig.distances[i])
            {
                return lodConfig.renderModes[i];
            }
        }

        return BuildingRenderMode.Marker;
    }
}
}