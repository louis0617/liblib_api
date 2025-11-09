using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using LiblibAPI;

/// <summary>
/// Liblib API LoRA 客户端
/// 用于调用 Liblib AI 文生图 API（支持LoRA模型）
/// </summary>
public class LiblibAPILora : MonoBehaviour
{
    [Header("API 配置")]
    [Tooltip("LoRA API 配置资源（优先使用，包含LoRA特定的默认值）")]
    public LiblibAPILoraConfig loraConfig;
    
    [Tooltip("API 配置资源（向后兼容，如果loraConfig为空则使用此配置）")]
    public LiblibAPIConfig apiConfig;
    
    [Header("生成设置")]
    [Tooltip("底模 modelVersionUUID（选填，如果需要指定特定的Checkpoint模型）\n从模型页面URL中获取versionUuid，例如：https://www.liblib.art/modelinfo/xxx?versionUuid=412b427ddb674b4dbab9e5abd5ae6057")]
    public string checkPointId = "";
    
    [Tooltip("提示词（英文，不超过2000字符）")]
    [TextArea(3, 10)]
    public string prompt = "Asian portrait,A young woman wearing a green baseball cap,covering one eye with her hand";
    
    [Tooltip("负面提示词（选填）")]
    [TextArea(3, 10)]
    public string negativePrompt = "ng_deepnegative_v1_75t,(badhandv4:1.2),EasyNegative,(worst quality:2),";
    
    [Header("LoRA 设置")]
    [Tooltip("LoRA模型列表（最多5个）")]
    public LoraModel[] loraModels = new LoraModel[0];
    
    [Header("生成参数")]
    [Tooltip("采样方法")]
    public int sampler = 15;
    
    [Tooltip("采样步数")]
    [Range(1, 100)]
    public int steps = 20;
    
    [Tooltip("提示词引导系数")]
    [Range(1f, 30f)]
    public float cfgScale = 7f;
    
    [Tooltip("图片宽度")]
    public int width = 768;
    
    [Tooltip("图片高度")]
    public int height = 1024;
    
    [Tooltip("图片数量")]
    [Range(1, 4)]
    public int imgCount = 1;
    
    [Tooltip("随机种子生成器 0=cpu, 1=Gpu")]
    [Range(0, 1)]
    public int randnSource = 0;
    
    [Tooltip("随机种子值，-1表示随机")]
    public long seed = -1;
    
    [Tooltip("面部修复，0=关闭，1=开启")]
    [Range(0, 1)]
    public int restoreFaces = 0;
    
    [Header("调试选项")]
    [Tooltip("启用调试日志")]
    public bool enableDebugLog = true;
    
    [Header("按钮设置")]
    [Tooltip("按钮配置列表，每个按钮可以配置使用不同数量的LoRA模型")]
    public ButtonConfig[] buttonConfigs = new ButtonConfig[0];
    
    [Header("图片保存设置")]
    [Tooltip("是否自动保存生成的图片到本地文件夹")]
    public bool autoSaveImages = true;
    
    [Tooltip("保存图片的文件夹路径（相对于项目根目录，例如：GeneratedImages）")]
    public string saveFolderPath = "GeneratedImages";
    
    [Tooltip("文件名格式已固定为：{buttonName}_{timestamp}.png\n例如：lora_merge_20241201_143022.png 或 1_lora_20241201_143022.png")]
    [HideInInspector]
    public string fileNameFormat = ""; // 已废弃，保留用于兼容性
    
    // 事件定义
    public event Action<QueryResultResponse> OnImageGenerated;
    public event Action<string> OnError;
    public event Action<string> OnStatusUpdate; // 状态更新事件（processing状态）
    
    // 当前正在进行的任务
    private Coroutine currentTaskCoroutine;
    
    // 当前使用的按钮名称（用于文件名生成）
    private string currentButtonName = "";
    
    /// <summary>
    /// LoRA模型信息
    /// </summary>
    [Serializable]
    public class LoraModel
    {
        [Tooltip("LoRA的模型版本versionuuid")]
        public string modelId = "";
        
        [Tooltip("LoRA权重")]
        [Range(0f, 2f)]
        public float weight = 0.3f;
    }
    
    /// <summary>
    /// 按钮配置信息
    /// </summary>
    [Serializable]
    public class ButtonConfig
    {
        [Tooltip("要绑定的按钮（从场景中拖入）")]
        public Button button;
        
        [Tooltip("按钮名称（用于文件名生成，例如：lora_merge、1_lora、2_lora）")]
        public string buttonName = "";
        
        [Tooltip("使用的LoRA数量（0=不使用LoRA，1=使用第1个LoRA，2=使用前2个LoRA，以此类推，最多5个）")]
        [Range(0, 5)]
        public int loraCount = 0;
        
        [Tooltip("是否使用自定义提示词（如果启用，将使用下面的customPrompt）")]
        public bool useCustomPrompt = false;
        
        [Tooltip("自定义提示词（仅在useCustomPrompt为true时使用）")]
        [TextArea(3, 10)]
        public string customPrompt = "";
    }
    
    /// <summary>
    /// 获取当前使用的配置（优先使用loraConfig，如果没有则使用apiConfig）
    /// </summary>
    private LiblibAPIConfig GetActiveConfig()
    {
        if (loraConfig != null && loraConfig.IsValid())
        {
            // 创建一个临时的LiblibAPIConfig来兼容现有代码
            // 实际上我们可以直接使用loraConfig，但为了保持代码一致性，我们返回一个包装
            return CreateConfigWrapper(loraConfig);
        }
        return apiConfig;
    }
    
    /// <summary>
    /// 创建一个配置包装器，将LiblibAPILoraConfig包装为LiblibAPIConfig接口
    /// 由于两者结构相似，我们可以直接访问字段
    /// </summary>
    private LiblibAPIConfig CreateConfigWrapper(LiblibAPILoraConfig loraConfig)
    {
        // 创建一个临时的配置对象来兼容现有代码
        // 实际上我们可以直接使用loraConfig的字段，但为了类型安全，我们创建一个包装
        LiblibAPIConfig wrapper = ScriptableObject.CreateInstance<LiblibAPIConfig>();
        wrapper.accessKey = loraConfig.accessKey;
        wrapper.secretKey = loraConfig.secretKey;
        wrapper.apiBaseUrl = loraConfig.apiBaseUrl;
        wrapper.templateUuid = loraConfig.templateUuid;
        wrapper.maxRetryCount = loraConfig.maxRetryCount;
        wrapper.queryInterval = loraConfig.queryInterval;
        return wrapper;
    }
    
    /// <summary>
    /// 从配置中初始化默认值（在Start或Awake中调用）
    /// </summary>
    private void InitializeFromConfig()
    {
        if (loraConfig != null && loraConfig.IsValid())
        {
            // 如果Inspector中的值为空或默认值，则从配置中读取
            if (string.IsNullOrEmpty(checkPointId) && !string.IsNullOrEmpty(loraConfig.defaultCheckPointId))
            {
                checkPointId = loraConfig.defaultCheckPointId;
            }
            if (string.IsNullOrEmpty(prompt))
            {
                prompt = loraConfig.defaultPrompt;
            }
            if (string.IsNullOrEmpty(negativePrompt))
            {
                negativePrompt = loraConfig.defaultNegativePrompt;
            }
            if (loraModels == null || loraModels.Length == 0)
            {
                loraModels = ConvertLoraModels(loraConfig.defaultLoraModels);
            }
            // 使用配置中的默认参数（如果Inspector中的值是默认值）
            if (sampler == 15 && loraConfig.defaultSampler != 15)
            {
                sampler = loraConfig.defaultSampler;
            }
            if (steps == 20 && loraConfig.defaultSteps != 20)
            {
                steps = loraConfig.defaultSteps;
            }
            if (Mathf.Approximately(cfgScale, 7f) && !Mathf.Approximately(loraConfig.defaultCfgScale, 7f))
            {
                cfgScale = loraConfig.defaultCfgScale;
            }
            if (width == 768 && loraConfig.defaultWidth != 768)
            {
                width = loraConfig.defaultWidth;
            }
            if (height == 1024 && loraConfig.defaultHeight != 1024)
            {
                height = loraConfig.defaultHeight;
            }
        }
    }
    
    /// <summary>
    /// 将配置中的DefaultLoraModel转换为运行时使用的LoraModel
    /// </summary>
    private LoraModel[] ConvertLoraModels(LiblibAPILoraConfig.DefaultLoraModel[] defaultModels)
    {
        if (defaultModels == null || defaultModels.Length == 0)
        {
            return new LoraModel[0];
        }
        LoraModel[] models = new LoraModel[defaultModels.Length];
        for (int i = 0; i < defaultModels.Length; i++)
        {
            models[i] = new LoraModel
            {
                modelId = defaultModels[i].modelId,
                weight = defaultModels[i].weight
            };
        }
        return models;
    }
    
    private void Start()
    {
        // 从配置中初始化默认值
        InitializeFromConfig();
        
        // 绑定按钮事件
        SetupButtons();
        
        // 确保保存文件夹存在
        if (autoSaveImages)
        {
            EnsureSaveFolderExists();
        }
    }
    
    /// <summary>
    /// 确保保存文件夹存在
    /// </summary>
    private void EnsureSaveFolderExists()
    {
        try
        {
            string fullPath = GetSaveFolderPath();
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                if (enableDebugLog)
                {
                    Debug.Log($"[LiblibAPILora] 已创建保存文件夹: {fullPath}");
                }
            }
            else if (enableDebugLog)
            {
                Debug.Log($"[LiblibAPILora] 保存文件夹已存在: {fullPath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LiblibAPILora] 创建保存文件夹失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 设置按钮点击事件
    /// </summary>
    private void SetupButtons()
    {
        if (buttonConfigs == null || buttonConfigs.Length == 0)
        {
            return;
        }
        
        for (int i = 0; i < buttonConfigs.Length; i++)
        {
            ButtonConfig config = buttonConfigs[i];
            if (config == null || config.button == null)
            {
                continue;
            }
            
            // 创建闭包来捕获当前配置
            ButtonConfig currentConfig = config;
            int buttonIndex = i;
            
            // 移除之前的监听器（如果有）
            config.button.onClick.RemoveAllListeners();
            
            // 添加新的监听器
            config.button.onClick.AddListener(() => OnButtonClicked(currentConfig, buttonIndex));
            
            if (enableDebugLog)
            {
                Debug.Log($"[LiblibAPILora] 已绑定按钮 {buttonIndex}，LoRA数量: {currentConfig.loraCount}");
            }
        }
    }
    
    /// <summary>
    /// 按钮点击事件处理
    /// </summary>
    private void OnButtonClicked(ButtonConfig config, int buttonIndex)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[LiblibAPILora] 按钮 {buttonIndex} 被点击，使用 {config.loraCount} 个LoRA模型");
        }
        
        // 记录当前使用的按钮名称（用于文件名生成）
        // 优先使用配置的buttonName，如果没有则使用按钮GameObject的名称，再没有则使用loraCount
        if (!string.IsNullOrEmpty(config.buttonName))
        {
            currentButtonName = config.buttonName;
        }
        else if (config.button != null && config.button.gameObject != null)
        {
            currentButtonName = config.button.gameObject.name;
        }
        else
        {
            currentButtonName = $"{config.loraCount}_lora";
        }
        
        // 根据配置的LoRA数量，从loraModels数组中提取相应数量的模型
        LoraModel[] modelsToUse = null;
        if (config.loraCount > 0 && loraModels != null && loraModels.Length > 0)
        {
            int actualCount = Mathf.Min(config.loraCount, loraModels.Length);
            modelsToUse = new LoraModel[actualCount];
            for (int i = 0; i < actualCount; i++)
            {
                modelsToUse[i] = loraModels[i];
            }
        }
        
        // 确定使用的提示词
        string promptToUse = config.useCustomPrompt && !string.IsNullOrEmpty(config.customPrompt) 
            ? config.customPrompt 
            : this.prompt;
        
        // 调用生成方法
        GenerateImageWithLora(
            prompt: promptToUse,
            loraModels: modelsToUse,
            templateUuid: null
        );
    }
    
    /// <summary>
    /// 生成图片（使用Inspector中设置的参数）
    /// </summary>
    public void Generate()
    {
        // 记录当前使用的按钮名称（用于文件名生成）
        // 如果没有通过按钮调用，使用默认名称
        if (string.IsNullOrEmpty(currentButtonName))
        {
            int loraCount = loraModels != null ? loraModels.Length : 0;
            currentButtonName = $"{loraCount}_lora";
        }
        
        GenerateImageWithLora(
            prompt,
            loraModels,
            templateUuid: null
        );
    }
    
    /// <summary>
    /// 生成图片（带LoRA）
    /// </summary>
    /// <param name="prompt">提示词（选填）</param>
    /// <param name="loraModels">LoRA模型列表（最多5个）</param>
    /// <param name="templateUuid">模板UUID，如果为空则使用配置中的默认值</param>
    public void GenerateImageWithLora(
        string prompt = null,
        LoraModel[] loraModels = null,
        string templateUuid = null)
    {
        // 获取当前使用的配置
        LiblibAPIConfig activeConfig = GetActiveConfig();
        if (activeConfig == null || !activeConfig.IsValid())
        {
            OnError?.Invoke("API配置无效，请检查AccessKey和SecretKey。请设置loraConfig或apiConfig。");
            return;
        }
        
        // 验证LoRA模型数量（最多5个）
        if (loraModels != null && loraModels.Length > 5)
        {
            OnError?.Invoke("LoRA模型数量不能超过5个");
            return;
        }
        
        // 停止之前的任务
        if (currentTaskCoroutine != null)
        {
            StopCoroutine(currentTaskCoroutine);
        }
        
        // 使用配置中的templateUuid如果未提供
        string templateId = string.IsNullOrEmpty(templateUuid) ? activeConfig.templateUuid : templateUuid;
        
        // 使用Inspector中的参数作为默认值
        string finalPrompt = prompt ?? this.prompt;
        LoraModel[] finalLoraModels = loraModels ?? this.loraModels;
        
        // 开始新的生成任务
        currentTaskCoroutine = StartCoroutine(GenerateImageLoraCoroutine(
            finalPrompt,
            finalLoraModels,
            templateId
        ));
    }
    
    /// <summary>
    /// 生成图片协程（带LoRA）
    /// </summary>
    private IEnumerator GenerateImageLoraCoroutine(
        string prompt,
        LoraModel[] loraModels,
        string templateUuid)
    {
        // 1. 提交文生图任务
        string generateUuid = null;
        yield return StartCoroutine(SubmitText2ImageLoraTask(
            prompt,
            loraModels,
            templateUuid,
            (uuid) => {
                generateUuid = uuid;
            }
        ));
        
        if (string.IsNullOrEmpty(generateUuid))
        {
            // 提交任务失败，错误已在SubmitText2ImageLoraTask中处理
            yield break;
        }
        
        if (enableDebugLog)
        {
            Debug.Log($"[LiblibAPILora] 任务已提交，generateUuid: {generateUuid}");
        }
        
        // 2. 轮询查询结果
        yield return StartCoroutine(QueryResultCoroutine(generateUuid));
    }
    
    /// <summary>
    /// 提交LoRA文生图任务
    /// </summary>
    private IEnumerator SubmitText2ImageLoraTask(
        string prompt,
        LoraModel[] loraModels,
        string templateUuid,
        Action<string> onSuccess)
    {
        // 构建additionalNetwork数组
        AdditionalNetwork[] additionalNetwork = null;
        if (loraModels != null && loraModels.Length > 0)
        {
            // 过滤掉空的modelId
            int validCount = 0;
            for (int i = 0; i < loraModels.Length; i++)
            {
                if (!string.IsNullOrEmpty(loraModels[i].modelId))
                {
                    validCount++;
                }
            }
            
            if (validCount > 0)
            {
                additionalNetwork = new AdditionalNetwork[validCount];
                int index = 0;
                for (int i = 0; i < loraModels.Length; i++)
                {
                    if (!string.IsNullOrEmpty(loraModels[i].modelId))
                    {
                        additionalNetwork[index] = new AdditionalNetwork
                        {
                            modelId = loraModels[i].modelId,
                            weight = loraModels[i].weight
                        };
                        index++;
                    }
                }
            }
        }
        
        // 构建请求体（按照官方文档F.1文生图 - 自定义完整参数示例）
        Text2ImageLoraRequest request = new Text2ImageLoraRequest
        {
            templateUuid = templateUuid,
            generateParams = new GenerateParamsLora
            {
                checkPointId = !string.IsNullOrEmpty(this.checkPointId) ? this.checkPointId : null,
                prompt = prompt,
                steps = this.steps,
                width = this.width,
                height = this.height,
                imgCount = this.imgCount,
                seed = this.seed,
                restoreFaces = this.restoreFaces,
                additionalNetwork = additionalNetwork
            }
        };
        
        string jsonBody = JsonUtility.ToJson(request);
        
        if (enableDebugLog)
        {
            Debug.Log($"[LiblibAPILora] 提交LoRA文生图任务 - TemplateUUID: {templateUuid}");
            if (!string.IsNullOrEmpty(this.checkPointId))
            {
                Debug.Log($"[LiblibAPILora] CheckPointId: {this.checkPointId}");
            }
            Debug.Log($"[LiblibAPILora] Prompt: {prompt}");
            Debug.Log($"[LiblibAPILora] LoRA数量: {(additionalNetwork != null ? additionalNetwork.Length : 0)}");
            Debug.Log($"[LiblibAPILora] 请求体: {jsonBody}");
        }
        
        // 获取当前使用的配置
        LiblibAPIConfig activeConfig = GetActiveConfig();
        
        // 构建带签名的URL（使用text2img接口，不是ultra接口）
        string urlPath = "/api/generate/webui/text2img";
        string signedUrl = LiblibAPIAuthHelper.BuildSignedUrl(
            activeConfig.apiBaseUrl,
            urlPath,
            activeConfig.accessKey,
            activeConfig.secretKey
        );
        
        // 创建POST请求
        using (UnityWebRequest request_web = new UnityWebRequest(signedUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request_web.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request_web.downloadHandler = new DownloadHandlerBuffer();
            request_web.SetRequestHeader("Content-Type", "application/json");
            
            if (enableDebugLog)
            {
                Debug.Log($"[LiblibAPILora] 发送请求到: {signedUrl}");
            }
            
            // 发送请求
            yield return request_web.SendWebRequest();
            
            // 处理响应
            string responseText = request_web.downloadHandler.text;
            
            if (enableDebugLog)
            {
                Debug.Log($"[LiblibAPILora] 提交任务响应: {responseText}");
            }
            
            if (request_web.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    SubmitTaskResponse response = JsonUtility.FromJson<SubmitTaskResponse>(responseText);
                    
                    // 检查响应码（通常0或200表示成功）
                    if (response.code == 0 || response.code == 200)
                    {
                        if (response.data != null && !string.IsNullOrEmpty(response.data.generateUuid))
                        {
                            onSuccess?.Invoke(response.data.generateUuid);
                        }
                        else
                        {
                            OnError?.Invoke("响应中未找到generateUuid");
                        }
                    }
                    else
                    {
                        // API返回了错误码
                        string errorMsg = !string.IsNullOrEmpty(response.msg) ? response.msg : $"API返回错误码: {response.code}";
                        OnError?.Invoke($"提交任务失败: {errorMsg}");
                    }
                }
                catch (Exception e)
                {
                    OnError?.Invoke($"解析响应失败: {e.Message}");
                }
            }
            else
            {
                string errorMessage = $"提交任务失败: {request_web.error}";
                
                // 尝试解析错误响应
                try
                {
                    if (!string.IsNullOrEmpty(responseText))
                    {
                        APIErrorResponse errorResponse = JsonUtility.FromJson<APIErrorResponse>(responseText);
                        if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.msg))
                        {
                            errorMessage = $"提交任务失败: {errorResponse.msg}";
                        }
                        else
                        {
                            errorMessage = $"提交任务失败: {responseText}";
                        }
                    }
                }
                catch
                {
                    // 忽略解析错误，使用默认错误消息
                }
                
                if (enableDebugLog)
                {
                    Debug.LogError($"[LiblibAPILora] {errorMessage}");
                }
                
                OnError?.Invoke(errorMessage);
            }
        }
    }
    
    /// <summary>
    /// 查询结果协程（轮询）
    /// </summary>
    private IEnumerator QueryResultCoroutine(string generateUuid)
    {
        // 获取当前使用的配置
        LiblibAPIConfig activeConfig = GetActiveConfig();
        
        int retryCount = 0;
        
        while (retryCount < activeConfig.maxRetryCount)
        {
            yield return new WaitForSeconds(activeConfig.queryInterval);
            
            // 构建查询请求
            // 查询接口：POST /api/generate/webui/status
            string urlPath = "/api/generate/webui/status";
            
            // 构建请求体
            QueryStatusRequest queryRequest = new QueryStatusRequest
            {
                generateUuid = generateUuid
            };
            
            string jsonBody = JsonUtility.ToJson(queryRequest);
            
            if (enableDebugLog && retryCount % 5 == 0) // 每5次查询输出一次日志
            {
                Debug.Log($"[LiblibAPILora] 查询结果 (第{retryCount + 1}次) - generateUuid: {generateUuid}");
                Debug.Log($"[LiblibAPILora] 查询请求体: {jsonBody}");
            }
            
            // 构建带签名的URL
            string signedUrl = LiblibAPIAuthHelper.BuildSignedUrl(
                activeConfig.apiBaseUrl,
                urlPath,
                activeConfig.accessKey,
                activeConfig.secretKey
            );
            
            // 创建POST请求
            using (UnityWebRequest request_web = new UnityWebRequest(signedUrl, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                request_web.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request_web.downloadHandler = new DownloadHandlerBuffer();
                request_web.SetRequestHeader("Content-Type", "application/json");
                
                yield return request_web.SendWebRequest();
                
                if (request_web.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request_web.downloadHandler.text;
                    
                    QueryResultResponse response = null;
                    bool parseSuccess = false;
                    
                    try
                    {
                        response = JsonUtility.FromJson<QueryResultResponse>(responseText);
                        parseSuccess = true;
                    }
                    catch (Exception e)
                    {
                        if (enableDebugLog)
                        {
                            Debug.LogError($"[LiblibAPILora] 解析查询响应失败: {e.Message}");
                        }
                        retryCount++;
                        continue;
                    }
                    
                    if (!parseSuccess || response == null)
                    {
                        retryCount++;
                        continue;
                    }
                    
                    // 检查响应码
                    if (response.code != 0 && response.code != 200)
                    {
                        string errorMsg = !string.IsNullOrEmpty(response.msg) ? response.msg : $"API返回错误码: {response.code}";
                        if (enableDebugLog)
                        {
                            Debug.LogWarning($"[LiblibAPILora] 查询失败: {errorMsg}");
                        }
                        retryCount++;
                        continue;
                    }
                    
                    if (response.data == null)
                    {
                        if (enableDebugLog)
                        {
                            Debug.LogWarning($"[LiblibAPILora] 查询响应中data为空");
                        }
                        retryCount++;
                        continue;
                    }
                    
                    // 使用新的数据模型字段
                    int generateStatus = response.data.generateStatus;
                    float percentCompleted = response.data.percentCompleted;
                    string generateMsg = response.data.generateMsg;
                    
                    // 检查是否有图片数据（images是对象数组，需要访问imageUrl属性）
                    bool hasImages = response.data.images != null && response.data.images.Length > 0 && 
                                    !string.IsNullOrEmpty(response.data.images[0].imageUrl);
                    bool hasImageUrl = !string.IsNullOrEmpty(response.data.imageUrl);
                    
                    // 判断任务是否完成：generateStatus == 5 或 percentCompleted >= 1.0 或 有图片数据
                    bool isCompleted = generateStatus == 5 || percentCompleted >= 1.0f || hasImages || hasImageUrl;
                    
                    if (enableDebugLog)
                    {
                        Debug.Log($"[LiblibAPILora] 查询结果:");
                        Debug.Log($"  GenerateStatus: {generateStatus} (5=完成)");
                        Debug.Log($"  PercentCompleted: {percentCompleted * 100:F1}%");
                        Debug.Log($"  GenerateMsg: {generateMsg}");
                        Debug.Log($"  HasImages: {hasImages} (Count: {(response.data.images != null ? response.data.images.Length : 0)})");
                        Debug.Log($"  HasImageUrl: {hasImageUrl}");
                        Debug.Log($"  IsCompleted: {isCompleted}");
                        
                        // 输出完整的响应数据以便调试
                        if (response.data.images != null && response.data.images.Length > 0)
                        {
                            for (int i = 0; i < response.data.images.Length; i++)
                            {
                                Debug.Log($"  图片[{i}]: imageUrl={response.data.images[i].imageUrl}, seed={response.data.images[i].seed}");
                            }
                        }
                        if (!string.IsNullOrEmpty(response.data.imageUrl))
                        {
                            Debug.Log($"  图片URL (直接字段): {response.data.imageUrl}");
                        }
                        
                        // 输出完整响应JSON以便调试
                        Debug.Log($"[LiblibAPILora] 完整响应JSON: {responseText}");
                    }
                    
                    // 如果任务完成（有图片数据或状态为完成）
                    if (isCompleted)
                    {
                        // 生成成功，自动下载并保存图片（移到 try-catch 外面）
                        if (autoSaveImages)
                        {
                            yield return StartCoroutine(DownloadAndSaveImages(response));
                        }
                        
                        // 生成成功
                        OnImageGenerated?.Invoke(response);
                        yield break;
                    }
                    else if (generateStatus < 0 || (generateMsg != null && generateMsg.Contains("失败")))
                    {
                        // 生成失败
                        OnError?.Invoke($"图片生成失败: {generateMsg ?? "未知错误"}");
                        yield break;
                    }
                    else
                    {
                        // 仍在处理中
                        string statusMsg = $"处理中... ({percentCompleted * 100:F1}%)";
                        if (!string.IsNullOrEmpty(generateMsg))
                        {
                            statusMsg = $"{generateMsg} ({percentCompleted * 100:F1}%)";
                        }
                        OnStatusUpdate?.Invoke(statusMsg);
                        retryCount++;
                        continue;
                    }
                }
                else
                {
                    string errorMessage = $"查询请求失败: {request_web.error}";
                    
                    // 尝试解析错误响应
                    try
                    {
                        string responseText = request_web.downloadHandler.text;
                        if (!string.IsNullOrEmpty(responseText))
                        {
                            APIErrorResponse errorResponse = JsonUtility.FromJson<APIErrorResponse>(responseText);
                            if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.msg))
                            {
                                errorMessage = $"查询失败: {errorResponse.msg}";
                            }
                            else
                            {
                                errorMessage = $"查询失败: {responseText}";
                            }
                        }
                    }
                    catch
                    {
                        // 忽略解析错误，使用默认错误消息
                    }
                    
                    if (enableDebugLog)
                    {
                        Debug.LogWarning($"[LiblibAPILora] {errorMessage}");
                    }
                    retryCount++;
                    continue;
                }
            }
        }
        
        // 达到最大重试次数
        OnError?.Invoke($"查询超时：已达到最大重试次数 ({activeConfig.maxRetryCount})");
    }
    
    /// <summary>
    /// 下载并保存所有图片
    /// </summary>
    private IEnumerator DownloadAndSaveImages(QueryResultResponse response)
    {
        if (response.data == null)
        {
            if (enableDebugLog)
            {
                Debug.LogWarning("[LiblibAPILora] 响应数据为空，无法保存图片");
            }
            yield break;
        }
        
        // 收集所有图片URL
        System.Collections.Generic.List<string> imageUrls = new System.Collections.Generic.List<string>();
        System.Collections.Generic.List<long> seeds = new System.Collections.Generic.List<long>();
        
        // 优先从images数组获取
        if (response.data.images != null && response.data.images.Length > 0)
        {
            for (int i = 0; i < response.data.images.Length; i++)
            {
                if (!string.IsNullOrEmpty(response.data.images[i].imageUrl))
                {
                    imageUrls.Add(response.data.images[i].imageUrl);
                    seeds.Add(response.data.images[i].seed);
                }
            }
        }
        // 如果没有images数组，尝试从imageUrl字段获取
        else if (!string.IsNullOrEmpty(response.data.imageUrl))
        {
            imageUrls.Add(response.data.imageUrl);
            seeds.Add(0); // 如果没有seed信息，使用0
        }
        
        if (imageUrls.Count == 0)
        {
            if (enableDebugLog)
            {
                Debug.LogWarning("[LiblibAPILora] 未找到图片URL，无法保存");
            }
            yield break;
        }
        
        // 确保保存文件夹存在
        string fullPath = GetSaveFolderPath();
        if (!Directory.Exists(fullPath))
        {
            try
            {
                Directory.CreateDirectory(fullPath);
                if (enableDebugLog)
                {
                    Debug.Log($"[LiblibAPILora] 创建保存文件夹: {fullPath}");
                }
            }
            catch (Exception e)
            {
                OnError?.Invoke($"创建保存文件夹失败: {e.Message}");
                yield break;
            }
        }
        
        // 下载并保存每张图片
        for (int i = 0; i < imageUrls.Count; i++)
        {
            string imageUrl = imageUrls[i];
            long seed = seeds[i];
            
            yield return StartCoroutine(DownloadAndSaveSingleImage(
                imageUrl, 
                seed, 
                i, 
                imageUrls.Count > 1
            ));
        }
    }
    
    /// <summary>
    /// 下载并保存单张图片
    /// </summary>
    private IEnumerator DownloadAndSaveSingleImage(string imageUrl, long seed, int imageIndex, bool isMultiple)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[LiblibAPILora] 开始下载并保存图片 {imageIndex + 1}: {imageUrl}");
        }
        
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                
                // 生成文件名
                string fileName = GenerateFileName(seed, imageIndex, isMultiple);
                string fullPath = Path.Combine(GetSaveFolderPath(), fileName);
                
                // 保存图片为PNG格式
                try
                {
                    byte[] pngData = texture.EncodeToPNG();
                    File.WriteAllBytes(fullPath, pngData);
                    
                    if (enableDebugLog)
                    {
                        Debug.Log($"[LiblibAPILora] 图片已保存: {fullPath}");
                    }
                    
                    OnStatusUpdate?.Invoke($"图片已保存: {fileName}");
                }
                catch (Exception e)
                {
                    string errorMsg = $"保存图片失败: {e.Message}";
                    if (enableDebugLog)
                    {
                        Debug.LogError($"[LiblibAPILora] {errorMsg}");
                    }
                    OnError?.Invoke(errorMsg);
                }
            }
            else
            {
                string errorMsg = $"下载图片失败: {request.error}";
                if (enableDebugLog)
                {
                    Debug.LogError($"[LiblibAPILora] {errorMsg}");
                }
                OnError?.Invoke(errorMsg);
            }
        }
    }
    
    /// <summary>
    /// 生成文件名
    /// </summary>
    private string GenerateFileName(long seed, int imageIndex, bool isMultiple)
    {
        // 生成时间戳（格式：yyyyMMdd_HHmmss）
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        
        // 获取按钮名称，如果没有则使用默认值
        string buttonName = currentButtonName;
        if (string.IsNullOrEmpty(buttonName))
        {
            buttonName = "image";
        }
        
        // 文件名格式：{buttonName}_{timestamp}
        string fileName = $"{buttonName}_{timestamp}";
        
        // 如果是多张图片，添加索引
        if (isMultiple)
        {
            fileName = $"{fileName}_{imageIndex + 1}";
        }
        
        // 确保文件名以.png结尾
        if (!fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".png";
        }
        
        // 清理文件名（移除可能的非法字符）
        fileName = SanitizeFileName(fileName);
        
        return fileName;
    }
    
    /// <summary>
    /// 清理文件名中的非法字符
    /// </summary>
    private string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return "Image";
        }
        
        // 移除Windows文件名中不允许的字符
        string invalidChars = new string(Path.GetInvalidFileNameChars());
        string pattern = "[" + Regex.Escape(invalidChars) + "]";
        fileName = Regex.Replace(fileName, pattern, "_");
        
        // 移除多余的下划线和空格
        fileName = Regex.Replace(fileName, @"_{2,}", "_");
        fileName = fileName.Trim('_', ' ');
        
        // 如果文件名为空，使用默认名称
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = "Image";
        }
        
        return fileName;
    }
    
    /// <summary>
    /// 获取保存文件夹的完整路径
    /// </summary>
    private string GetSaveFolderPath()
    {
        // 如果路径是绝对路径，直接返回
        if (Path.IsPathRooted(saveFolderPath))
        {
            return saveFolderPath;
        }
        
        // 否则，相对于项目根目录
        string projectRoot = Application.dataPath;
        // Application.dataPath 指向 Assets 文件夹，需要返回上一级
        projectRoot = Directory.GetParent(projectRoot).FullName;
        
        return Path.Combine(projectRoot, saveFolderPath);
    }
    
    /// <summary>
    /// 下载图片
    /// </summary>
    /// <param name="imageUrl">图片URL</param>
    /// <param name="onSuccess">成功回调，返回Texture2D</param>
    /// <param name="onError">失败回调</param>
    public void DownloadImage(string imageUrl, Action<Texture2D> onSuccess, Action<string> onError = null)
    {
        StartCoroutine(DownloadImageCoroutine(imageUrl, onSuccess, onError));
    }
    
    /// <summary>
    /// 下载图片协程
    /// </summary>
    private IEnumerator DownloadImageCoroutine(string imageUrl, Action<Texture2D> onSuccess, Action<string> onError)
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            onError?.Invoke("图片URL为空");
            yield break;
        }
        
        if (enableDebugLog)
        {
            Debug.Log($"[LiblibAPILora] 开始下载图片: {imageUrl}");
        }
        
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                onSuccess?.Invoke(texture);
                
                if (enableDebugLog)
                {
                    Debug.Log($"[LiblibAPILora] 图片下载成功: {imageUrl}");
                }
            }
            else
            {
                string errorMsg = $"下载图片失败: {request.error}";
                if (enableDebugLog)
                {
                    Debug.LogError($"[LiblibAPILora] {errorMsg}");
                }
                onError?.Invoke(errorMsg);
            }
        }
    }
    
    /// <summary>
    /// 查询指定generateUuid的状态（用于手动查询或调试）
    /// </summary>
    /// <param name="generateUuid">生成任务的UUID</param>
    public void QueryStatus(string generateUuid)
    {
        if (string.IsNullOrEmpty(generateUuid))
        {
            OnError?.Invoke("generateUuid不能为空");
            return;
        }
        
        LiblibAPIConfig activeConfig = GetActiveConfig();
        if (activeConfig == null || !activeConfig.IsValid())
        {
            OnError?.Invoke("API配置无效，请检查AccessKey和SecretKey。请设置loraConfig或apiConfig。");
            return;
        }
        
        StartCoroutine(QueryStatusCoroutine(generateUuid));
    }
    
    /// <summary>
    /// 查询状态协程（单次查询，不轮询）
    /// </summary>
    private IEnumerator QueryStatusCoroutine(string generateUuid)
    {
        // 获取当前使用的配置
        LiblibAPIConfig activeConfig = GetActiveConfig();
        
        // 构建查询请求
        string urlPath = "/api/generate/webui/status";
        
        // 构建请求体
        QueryStatusRequest queryRequest = new QueryStatusRequest
        {
            generateUuid = generateUuid
        };
        
        string jsonBody = JsonUtility.ToJson(queryRequest);
        
        if (enableDebugLog)
        {
            Debug.Log($"[LiblibAPILora] 查询状态 - generateUuid: {generateUuid}");
            Debug.Log($"[LiblibAPILora] 查询请求体: {jsonBody}");
        }
        
        // 构建带签名的URL
        string signedUrl = LiblibAPIAuthHelper.BuildSignedUrl(
            activeConfig.apiBaseUrl,
            urlPath,
            activeConfig.accessKey,
            activeConfig.secretKey
        );
        
        // 创建POST请求
        using (UnityWebRequest request_web = new UnityWebRequest(signedUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request_web.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request_web.downloadHandler = new DownloadHandlerBuffer();
            request_web.SetRequestHeader("Content-Type", "application/json");
            
            yield return request_web.SendWebRequest();
            
            // 处理响应
            string responseText = request_web.downloadHandler.text;
            
            if (enableDebugLog)
            {
                Debug.Log($"[LiblibAPILora] 查询状态响应: {responseText}");
            }
            
            if (request_web.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    QueryResultResponse response = JsonUtility.FromJson<QueryResultResponse>(responseText);
                    
                    // 检查响应码
                    if (response.code == 0 || response.code == 200)
                    {
                        if (response.data != null)
                        {
                            // 使用新的数据模型字段
                            int generateStatus = response.data.generateStatus;
                            float percentCompleted = response.data.percentCompleted;
                            string generateMsg = response.data.generateMsg;
                            
                            // 检查是否有图片数据（images是对象数组，需要访问imageUrl属性）
                            bool hasImages = response.data.images != null && response.data.images.Length > 0 && 
                                            !string.IsNullOrEmpty(response.data.images[0].imageUrl);
                            bool hasImageUrl = !string.IsNullOrEmpty(response.data.imageUrl);
                            
                            // 判断任务是否完成
                            bool isCompleted = generateStatus == 5 || percentCompleted >= 1.0f || hasImages || hasImageUrl;
                            
                            Debug.Log($"[LiblibAPILora] 查询结果:");
                            Debug.Log($"  GenerateStatus: {generateStatus} (5=完成)");
                            Debug.Log($"  PercentCompleted: {percentCompleted * 100:F1}%");
                            Debug.Log($"  GenerateMsg: {generateMsg}");
                            Debug.Log($"  HasImages: {hasImages} (Count: {(response.data.images != null ? response.data.images.Length : 0)})");
                            Debug.Log($"  HasImageUrl: {hasImageUrl}");
                            Debug.Log($"  IsCompleted: {isCompleted}");
                            
                            if (hasImages)
                            {
                                Debug.Log($"  图片URL (from images): {response.data.images[0].imageUrl}");
                                for (int i = 0; i < response.data.images.Length; i++)
                                {
                                    Debug.Log($"  图片[{i}]: {response.data.images[i].imageUrl} (Seed: {response.data.images[i].seed})");
                                }
                            }
                            else if (hasImageUrl)
                            {
                                Debug.Log($"  图片URL: {response.data.imageUrl}");
                            }
                            
                            // 如果有图片，触发成功事件
                            if (isCompleted)
                            {
                                // 自动下载并保存图片
                                if (autoSaveImages)
                                {
                                    StartCoroutine(DownloadAndSaveImages(response));
                                }
                                
                                OnImageGenerated?.Invoke(response);
                            }
                            else if (generateStatus < 0 || (generateMsg != null && generateMsg.Contains("失败")))
                            {
                                OnError?.Invoke($"图片生成失败: {generateMsg ?? "未知错误"}");
                            }
                            else
                            {
                                string statusMsg = $"处理中... ({percentCompleted * 100:F1}%)";
                                if (!string.IsNullOrEmpty(generateMsg))
                                {
                                    statusMsg = $"{generateMsg} ({percentCompleted * 100:F1}%)";
                                }
                                OnStatusUpdate?.Invoke(statusMsg);
                            }
                        }
                        else
                        {
                            Debug.LogWarning("[LiblibAPILora] 查询响应中data为空");
                            OnError?.Invoke("查询响应中data为空");
                        }
                    }
                    else
                    {
                        string errorMsg = !string.IsNullOrEmpty(response.msg) ? response.msg : $"API返回错误码: {response.code}";
                        Debug.LogError($"[LiblibAPILora] 查询失败: {errorMsg}");
                        OnError?.Invoke($"查询失败: {errorMsg}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[LiblibAPILora] 解析查询响应失败: {e.Message}");
                    OnError?.Invoke($"解析响应失败: {e.Message}");
                }
            }
            else
            {
                string errorMessage = $"查询请求失败: {request_web.error}";
                
                // 尝试解析错误响应
                try
                {
                    if (!string.IsNullOrEmpty(responseText))
                    {
                        APIErrorResponse errorResponse = JsonUtility.FromJson<APIErrorResponse>(responseText);
                        if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.msg))
                        {
                            errorMessage = $"查询失败: {errorResponse.msg}";
                        }
                        else
                        {
                            errorMessage = $"查询失败: {responseText}";
                        }
                    }
                }
                catch
                {
                    // 忽略解析错误，使用默认错误消息
                }
                
                Debug.LogError($"[LiblibAPILora] {errorMessage}");
                OnError?.Invoke(errorMessage);
            }
        }
    }
    
    /// <summary>
    /// 取消当前任务
    /// </summary>
    public void CancelCurrentTask()
    {
        if (currentTaskCoroutine != null)
        {
            StopCoroutine(currentTaskCoroutine);
            currentTaskCoroutine = null;
            OnError?.Invoke("任务已取消");
        }
    }
    
    private void OnDestroy()
    {
        CancelCurrentTask();
    }
}