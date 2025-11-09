using UnityEngine;
using UnityEngine.UI;
using LiblibAPI;

/// <summary>
/// Liblib API LoRA 使用示例
/// 演示如何调用带LoRA模型的文生图API
/// </summary>
public class LiblibAPILoraExample : MonoBehaviour
{
    [Header("UI 引用（可选）")]
    [Tooltip("输入提示词的输入框")]
    public InputField promptInputField;
    
    [Tooltip("输入负面提示词的输入框")]
    public InputField negativePromptInputField;
    
    [Tooltip("输入底模ID的输入框")]
    public InputField checkPointIdInputField;
    
    [Tooltip("显示生成状态的文本")]
    public Text statusText;
    
    [Tooltip("显示生成的图片")]
    public RawImage imageDisplay;
    
    [Header("API 客户端")]
    [Tooltip("Liblib API LoRA 客户端组件（如果不在同一GameObject上）")]
    public LiblibAPILora loraClient;
    
    private void Start()
    {
        // 如果没有指定loraClient，尝试从当前GameObject获取
        if (loraClient == null)
        {
            loraClient = GetComponent<LiblibAPILora>();
        }
        
        // 如果没有找到，尝试从场景中查找
        if (loraClient == null)
        {
            loraClient = FindObjectOfType<LiblibAPILora>();
        }
        
        if (loraClient == null)
        {
            Debug.LogError("[LiblibAPILoraExample] 未找到 LiblibAPILora 组件！请在场景中添加该组件。");
            return;
        }
        
        // 订阅事件
        loraClient.OnImageGenerated += OnImageGenerated;
        loraClient.OnError += OnError;
        loraClient.OnStatusUpdate += OnStatusUpdate;
        
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
    /// 使用Inspector中设置的参数
    /// </summary>
    public void GenerateImage()
    {
        UpdateStatus("正在提交生成任务...");
        
        // 调用API生成图片（使用Inspector中配置的参数）
        loraClient.Generate();
    }
    
    /// <summary>
    /// 使用自定义参数生成图片
    /// </summary>
    /// <param name="checkPointId">底模 modelVersionUUID（必填）</param>
    /// <param name="prompt">提示词</param>
    /// <param name="negativePrompt">负面提示词</param>
    public void GenerateImageWithParams(string checkPointId, string prompt, string negativePrompt = null)
    {
        if (string.IsNullOrEmpty(checkPointId))
        {
            Debug.LogWarning("[LiblibAPILoraExample] checkPointId不能为空");
            UpdateStatus("错误：checkPointId不能为空");
            return;
        }
        
        UpdateStatus("正在提交生成任务...");
        
        // 从输入框获取参数（如果提供了）
        string finalPrompt = prompt;
        string finalNegativePrompt = negativePrompt;
        
        if (promptInputField != null && !string.IsNullOrEmpty(promptInputField.text))
        {
            finalPrompt = promptInputField.text;
        }
        
        if (negativePromptInputField != null && !string.IsNullOrEmpty(negativePromptInputField.text))
        {
            finalNegativePrompt = negativePromptInputField.text;
        }
        
        // 调用API生成图片
        loraClient.GenerateImageWithLora(
            checkPointId: checkPointId,
            prompt: finalPrompt,
            negativePrompt: finalNegativePrompt,
            loraModels: null, // 使用Inspector中配置的LoRA模型
            templateUuid: null // 使用配置中的默认模板
        );
    }
    
    /// <summary>
    /// 使用自定义LoRA模型生成图片
    /// </summary>
    public void GenerateImageWithCustomLora()
    {
        // 从输入框获取参数
        string checkPointId = "";
        string prompt = "";
        string negativePrompt = "";
        
        if (checkPointIdInputField != null)
        {
            checkPointId = checkPointIdInputField.text;
        }
        
        if (promptInputField != null)
        {
            prompt = promptInputField.text;
        }
        
        if (negativePromptInputField != null)
        {
            negativePrompt = negativePromptInputField.text;
        }
        
        if (string.IsNullOrEmpty(checkPointId))
        {
            Debug.LogWarning("[LiblibAPILoraExample] checkPointId不能为空");
            UpdateStatus("错误：请先输入底模ID");
            return;
        }
        
        // 创建自定义LoRA模型列表（示例：最多5个）
        LiblibAPILora.LoraModel[] customLoraModels = new LiblibAPILora.LoraModel[]
        {
            new LiblibAPILora.LoraModel
            {
                modelId = "31360f2f031b4ff6b589412a52713fcf", // LoRA的versionuuid
                weight = 0.3f // LoRA权重
            },
            new LiblibAPILora.LoraModel
            {
                modelId = "365e700254dd40bbb90d5e78c152ec7f", // 另一个LoRA的versionuuid
                weight = 0.6f
            }
        };
        
        UpdateStatus("正在提交生成任务...");
        
        // 调用API生成图片
        loraClient.GenerateImageWithLora(
            checkPointId: checkPointId,
            prompt: prompt,
            negativePrompt: negativePrompt,
            loraModels: customLoraModels,
            templateUuid: null
        );
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
            Debug.LogError("[LiblibAPILoraExample] 响应中未找到图片URL");
            UpdateStatus("错误：响应中未找到图片URL");
            return;
        }
        
        Debug.Log($"[LiblibAPILoraExample] 图片生成成功！URL: {imageUrl}");
        
        // 下载图片
        loraClient.DownloadImage(imageUrl, OnImageDownloaded, OnImageDownloadError);
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
        
        Debug.Log($"[LiblibAPILoraExample] 图片下载成功，尺寸: {texture.width}x{texture.height}");
    }
    
    /// <summary>
    /// 图片下载失败回调
    /// </summary>
    private void OnImageDownloadError(string error)
    {
        UpdateStatus($"下载失败: {error}");
        Debug.LogError($"[LiblibAPILoraExample] {error}");
    }
    
    /// <summary>
    /// 错误回调
    /// </summary>
    private void OnError(string error)
    {
        UpdateStatus($"错误: {error}");
        Debug.LogError($"[LiblibAPILoraExample] {error}");
    }
    
    /// <summary>
    /// 状态更新回调
    /// </summary>
    private void OnStatusUpdate(string status)
    {
        UpdateStatus(status);
        Debug.Log($"[LiblibAPILoraExample] 状态更新: {status}");
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
        if (loraClient != null)
        {
            loraClient.OnImageGenerated -= OnImageGenerated;
            loraClient.OnError -= OnError;
            loraClient.OnStatusUpdate -= OnStatusUpdate;
        }
    }
}

