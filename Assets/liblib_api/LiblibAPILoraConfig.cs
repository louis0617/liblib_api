using UnityEngine;

/// <summary>
/// Liblib API LoRA 配置类
/// 用于存储LoRA API的密钥和配置信息（包含LoRA特定的默认值）
/// </summary>
[CreateAssetMenu(fileName = "LiblibAPILoraConfig", menuName = "Liblib/LoRA API Config")]
public class LiblibAPILoraConfig : ScriptableObject
{
    [Header("API 配置")]
    [Tooltip("从 liblib.ai API 开发平台获取的 AccessKey")]
    public string accessKey = "";
    
    [Tooltip("从 liblib.ai API 开发平台获取的 SecretKey")]
    public string secretKey = "";
    
    [Tooltip("API 基础地址")]
    public string apiBaseUrl = "https://openapi.liblibai.cloud";
    
    [Header("默认参数")]
    [Tooltip("模板UUID")]
    public string templateUuid = "e10adc3949ba59abbe56e057f20f883e";
    
    [Tooltip("默认底模 modelVersionUUID（选填，如果需要指定特定的Checkpoint模型）\n从模型页面URL中获取versionUuid，例如：https://www.liblib.art/modelinfo/xxx?versionUuid=412b427ddb674b4dbab9e5abd5ae6057")]
    public string defaultCheckPointId = "";
    
    [Tooltip("默认提示词")]
    [TextArea(3, 5)]
    public string defaultPrompt = "Asian portrait,A young woman wearing a green baseball cap,covering one eye with her hand";
    
    [Tooltip("默认负面提示词")]
    [TextArea(3, 5)]
    public string defaultNegativePrompt = "ng_deepnegative_v1_75t,(badhandv4:1.2),EasyNegative,(worst quality:2),";
    
    [Header("默认生成参数")]
    [Tooltip("默认采样方法")]
    public int defaultSampler = 15;
    
    [Tooltip("默认采样步数")]
    [Range(1, 100)]
    public int defaultSteps = 20;
    
    [Tooltip("默认提示词引导系数")]
    [Range(1f, 30f)]
    public float defaultCfgScale = 7f;
    
    [Tooltip("默认图片宽度")]
    public int defaultWidth = 768;
    
    [Tooltip("默认图片高度")]
    public int defaultHeight = 1024;
    
    [Tooltip("默认图片数量")]
    [Range(1, 4)]
    public int defaultImgCount = 1;
    
    [Tooltip("默认随机种子生成器 0=cpu, 1=Gpu")]
    [Range(0, 1)]
    public int defaultRandnSource = 0;
    
    [Tooltip("默认随机种子值，-1表示随机")]
    public long defaultSeed = -1;
    
    [Tooltip("默认面部修复，0=关闭，1=开启")]
    [Range(0, 1)]
    public int defaultRestoreFaces = 0;
    
    [Header("默认LoRA设置")]
    [Tooltip("默认LoRA模型列表（最多5个）")]
    public DefaultLoraModel[] defaultLoraModels = new DefaultLoraModel[0];
    
    [Header("轮询设置")]
    [Tooltip("查询结果的最大重试次数")]
    public int maxRetryCount = 60;
    
    [Tooltip("每次查询的间隔时间（秒）")]
    public float queryInterval = 2.0f;
    
    /// <summary>
    /// 默认LoRA模型信息（用于配置资源）
    /// </summary>
    [System.Serializable]
    public class DefaultLoraModel
    {
        [Tooltip("LoRA的模型版本versionuuid")]
        public string modelId = "";
        
        [Tooltip("LoRA权重")]
        [Range(0f, 2f)]
        public float weight = 0.3f;
    }
    
    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey);
    }
    
    /// <summary>
    /// 将默认LoRA模型转换为运行时使用的LoraModel数组
    /// </summary>
    public LiblibAPILora.LoraModel[] GetLoraModels()
    {
        if (defaultLoraModels == null || defaultLoraModels.Length == 0)
        {
            return new LiblibAPILora.LoraModel[0];
        }
        
        LiblibAPILora.LoraModel[] models = new LiblibAPILora.LoraModel[defaultLoraModels.Length];
        for (int i = 0; i < defaultLoraModels.Length; i++)
        {
            models[i] = new LiblibAPILora.LoraModel
            {
                modelId = defaultLoraModels[i].modelId,
                weight = defaultLoraModels[i].weight
            };
        }
        return models;
    }
}

