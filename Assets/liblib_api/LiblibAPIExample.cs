using UnityEngine;
using UnityEngine.UI;
using LiblibAPI;

/// <summary>
/// Liblib API 使用示例
/// 演示如何调用文生图API
/// </summary>
public class LiblibAPIExample : MonoBehaviour
{
    [Header("UI 引用（可选）")]
    [Tooltip("输入提示词的输入框")]
    public InputField promptInputField;
    
    [Tooltip("显示生成状态的文本")]
    public Text statusText;
    
    [Tooltip("显示生成的图片")]
    public RawImage imageDisplay;
    
    [Header("API 客户端")]
    [Tooltip("Liblib API 客户端组件（如果不在同一GameObject上）")]
    public LiblibAPIClient apiClient;
    
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
            Debug.LogError("[LiblibAPIExample] 未找到 LiblibAPIClient 组件！请在场景中添加该组件。");
            return;
        }
        
        // 订阅事件
        apiClient.OnImageGenerated += OnImageGenerated;
        apiClient.OnError += OnError;
        apiClient.OnStatusUpdate += OnStatusUpdate;
        
        // 如果有输入框，添加回车键监听
        if (promptInputField != null)
        {
            promptInputField.onEndEdit.AddListener(OnPromptInputEndEdit);
        }
    }
    
    /// <summary>
    /// 输入框结束编辑时触发（回车键）
    /// </summary>
    private void OnPromptInputEndEdit(string text)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            GenerateImage();
        }
    }
    
    /// <summary>
    /// 生成图片（公共方法，可以从UI按钮调用）
    /// </summary>
    public void GenerateImage()
    {
        string prompt = "";
        
        // 从输入框获取提示词
        if (promptInputField != null)
        {
            prompt = promptInputField.text;
        }
        
        // 如果没有输入框或输入框为空，使用默认提示词
        if (string.IsNullOrEmpty(prompt))
        {
            prompt = "a beautiful sunset over the ocean, high quality, detailed";
        }
        
        UpdateStatus("正在提交生成任务...");
        
        // 调用API生成图片
        apiClient.GenerateImage(prompt);
    }
    
    /// <summary>
    /// 使用自定义提示词生成图片
    /// </summary>
    public void GenerateImageWithPrompt(string prompt)
    {
        if (string.IsNullOrEmpty(prompt))
        {
            Debug.LogWarning("[LiblibAPIExample] 提示词为空");
            return;
        }
        
        UpdateStatus("正在提交生成任务...");
        apiClient.GenerateImage(prompt);
    }
    
    /// <summary>
    /// 图片生成成功回调
    /// </summary>
    private void OnImageGenerated(QueryResultResponse response)
    {
        UpdateStatus("图片生成成功！正在下载...");
        
         // 从response.data中获取imageUrl（优先从images数组获取，如果没有则使用imageUrl字段）
        string imageUrl = null;
        
        if (response.data != null)
        {
            // 优先从images数组获取（images是对象数组，需要访问imageUrl属性）
            if (response.data.images != null && response.data.images.Length > 0 && 
                !string.IsNullOrEmpty(response.data.images[0].imageUrl))
            {
                imageUrl = response.data.images[0].imageUrl;
            }
            // 如果没有images数组，尝试从imageUrl字段获取
            else if (!string.IsNullOrEmpty(response.data.imageUrl))
            {
                imageUrl = response.data.imageUrl;
            }
        }
        
        if (string.IsNullOrEmpty(imageUrl))
        {
            Debug.LogError("[LiblibAPIExample] 响应中未找到图片URL");
            UpdateStatus("错误：响应中未找到图片URL");
            return;
        }
        
        Debug.Log($"[LiblibAPIExample] 图片生成成功！URL: {imageUrl}");
        
        // 下载图片
        apiClient.DownloadImage(imageUrl, OnImageDownloaded, OnImageDownloadError);
    }
    
    /// <summary>
    /// 图片下载成功回调
    /// </summary>
    private void OnImageDownloaded(Texture2D texture)
    {
        UpdateStatus("图片下载成功！");
        
        // 显示图片
        if (imageDisplay != null)
        {
            imageDisplay.texture = texture;
        }
        
        Debug.Log($"[LiblibAPIExample] 图片下载成功，尺寸: {texture.width}x{texture.height}");
    }
    
    /// <summary>
    /// 图片下载失败回调
    /// </summary>
    private void OnImageDownloadError(string error)
    {
        UpdateStatus($"下载失败: {error}");
        Debug.LogError($"[LiblibAPIExample] {error}");
    }
    
    /// <summary>
    /// 错误回调
    /// </summary>
    private void OnError(string error)
    {
        UpdateStatus($"错误: {error}");
        Debug.LogError($"[LiblibAPIExample] {error}");
    }
    
    /// <summary>
    /// 状态更新回调
    /// </summary>
    private void OnStatusUpdate(string status)
    {
        UpdateStatus(status);
        Debug.Log($"[LiblibAPIExample] 状态更新: {status}");
    }
    
    /// <summary>
    /// 更新状态文本
    /// </summary>
    private void UpdateStatus(string status)
    {
        if (statusText != null)
        {
            statusText.text = status;
        }
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

