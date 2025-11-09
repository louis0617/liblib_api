using UnityEngine;

/// <summary>
/// Liblib API 配置类
/// 用于存储API密钥和配置信息
/// </summary>
[CreateAssetMenu(fileName = "LiblibAPIConfig", menuName = "Liblib/API Config")]
public class LiblibAPIConfig : ScriptableObject
{
    [Header("API 配置")]
    [Tooltip("从 liblib.ai API 开发平台获取的 AccessKey")]
    public string accessKey = "";
    
    [Tooltip("从 liblib.ai API 开发平台获取的 SecretKey")]
    public string secretKey = "";
    
    [Tooltip("API 基础地址")]
    public string apiBaseUrl = "https://openapi.liblibai.cloud";
    
    [Header("默认参数")]
    [Tooltip("模板UUID（星流Star-3 Alpha模板ID）")]
    public string templateUuid = "5d7e67009b344550bc1aa6ccbfa1d7f4";
    
    [Tooltip("默认图片宽度")]
    public int defaultWidth = 512;
    
    [Tooltip("默认图片高度")]
    public int defaultHeight = 512;
    
    [Header("轮询设置")]
    [Tooltip("查询结果的最大重试次数")]
    public int maxRetryCount = 60;
    
    [Tooltip("每次查询的间隔时间（秒）")]
    public float queryInterval = 2.0f;
    
    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey);
    }
}

