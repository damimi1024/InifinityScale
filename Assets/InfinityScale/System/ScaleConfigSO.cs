using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SURender.InfinityScale
{
// 8. 配置管理器
    [CreateAssetMenu(fileName = "ScaleConfig", menuName = "InfinityScale/Config")]
    public class ScaleConfigSO : ScriptableObject
    {
    public InfinityScaleManager.ScaleConfig scaleConfig;  // 修正为InfinityScaleManager
    public CameraSystem.CameraConfig cameraConfig;
    public LODSystem.LODLevel[] lodLevels;
    
    // 性能配置
    public int maxConcurrentLoads = 5;
    public int chunkSize = 100;
    public int viewDistance = 1000;
    }
}