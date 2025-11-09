using UnityEngine;
using LiblibAPI;

/// <summary>
/// Liblib API 查询测试脚本
/// 用于手动查询指定generateUuid的状态
/// </summary>
public class LiblibAPIQueryTest : MonoBehaviour
{
    [Header("API 客户端")]
    [Tooltip("Liblib API 客户端组件（如果不在同一GameObject上）")]
    public LiblibAPIClient apiClient;
    
    [Header("测试参数")]
    [Tooltip("要查询的generateUuid")]
    public string generateUuid = "4a5b5f5a488142f3bf75260d5e70a24c";
    
    private void Start()
    {
        // 如果没有指定apiClient，尝试从当前GameObject获取
        if (apiClient == null)
        {
            apiClient = GetComponent<LiblibAPIClient>();
        }
        
        // 如果没有找到，尝试从场景中查找
        if (apiClient == null)
        {
            apiClient = FindObjectOfType<LiblibAPIClient>();
        }
        
        if (apiClient == null)
        {
            Debug.LogError("[LiblibAPIQueryTest] 未找到 LiblibAPIClient 组件！请在场景中添加该组件。");
            return;
        }
        
        // 订阅事件
        apiClient.OnImageGenerated += OnImageGenerated;
        apiClient.OnError += OnError;
        apiClient.OnStatusUpdate += OnStatusUpdate;
    }
    
    /// <summary>
    /// 查询状态（公共方法，可以从UI按钮调用）
    /// </summary>
    public void Query()
    {
        if (string.IsNullOrEmpty(generateUuid))
        {
            Debug.LogError("[LiblibAPIQueryTest] generateUuid为空，请在Inspector中设置");
            return;
        }
        
        Debug.Log($"[LiblibAPIQueryTest] 开始查询 generateUuid: {generateUuid}");
        apiClient.QueryStatus(generateUuid);
    }
    
    /// <summary>
    /// 使用自定义generateUuid查询
    /// </summary>
    public void QueryWithUuid(string uuid)
    {
        if (string.IsNullOrEmpty(uuid))
        {
            Debug.LogError("[LiblibAPIQueryTest] generateUuid为空");
            return;
        }
        
        generateUuid = uuid;
        Query();
    }
    
    /// <summary>
    /// 图片生成成功回调
    /// </summary>
    private void OnImageGenerated(QueryResultResponse response)
    {
        Debug.Log("[LiblibAPIQueryTest] ====== 查询成功！图片已生成 ======");
        
        // 从response.data中获取imageUrl
        string imageUrl = null;
        
        if (response.data != null)
        {
            // 优先从images数组获取（images是对象数组，需要访问imageUrl属性）
            if (response.data.images != null && response.data.images.Length > 0 && 
                !string.IsNullOrEmpty(response.data.images[0].imageUrl))
            {
                imageUrl = response.data.images[0].imageUrl;
                Debug.Log($"[LiblibAPIQueryTest] 找到 {response.data.images.Length} 张图片");
                for (int i = 0; i < response.data.images.Length; i++)
                {
                    Debug.Log($"[LiblibAPIQueryTest] 图片[{i}]: {response.data.images[i].imageUrl} (Seed: {response.data.images[i].seed})");
                }
            }
            // 如果没有images数组，尝试从imageUrl字段获取
            else if (!string.IsNullOrEmpty(response.data.imageUrl))
            {
                imageUrl = response.data.imageUrl;
                Debug.Log($"[LiblibAPIQueryTest] 图片URL: {imageUrl}");
            }
        }
        
        if (!string.IsNullOrEmpty(imageUrl))
        {
            // 下载图片
            apiClient.DownloadImage(imageUrl, OnImageDownloaded, OnImageDownloadError);
        }
        else
        {
            Debug.LogWarning("[LiblibAPIQueryTest] 响应中未找到图片URL");
        }
    }
    
    /// <summary>
    /// 图片下载成功回调
    /// </summary>
    private void OnImageDownloaded(Texture2D texture)
    {
        Debug.Log($"[LiblibAPIQueryTest] 图片下载成功！尺寸: {texture.width}x{texture.height}");
    }
    
    /// <summary>
    /// 图片下载失败回调
    /// </summary>
    private void OnImageDownloadError(string error)
    {
        Debug.LogError($"[LiblibAPIQueryTest] 下载失败: {error}");
    }
    
    /// <summary>
    /// 错误回调
    /// </summary>
    private void OnError(string error)
    {
        Debug.LogError($"[LiblibAPIQueryTest] 错误: {error}");
    }
    
    /// <summary>
    /// 状态更新回调
    /// </summary>
    private void OnStatusUpdate(string status)
    {
        Debug.Log($"[LiblibAPIQueryTest] 状态: {status}");
    }
    
    private void OnDestroy()
    {
        // 取消订阅事件
        if (apiClient != null)
        {
            apiClient.OnImageGenerated -= OnImageGenerated;
            apiClient.OnError -= OnError;
            apiClient.OnStatusUpdate -= OnStatusUpdate;
        }
    }
}

