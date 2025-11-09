using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
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
    [Tooltip("底模 modelVersionUUID（必填）")]
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
    
    [Header("高分辨率修复（可选）")]
    [Tooltip("启用高分辨率修复")]
    public bool enableHiResFix = false;
    
    [Tooltip("高分辨率修复的重绘步数")]
    [Range(1, 100)]
    public int hiresSteps = 20;
    
    [Tooltip("高分辨率修复的重绘幅度")]
    [Range(0f, 1f)]
    public float hiresDenoisingStrength = 0.75f;
    
    [Tooltip("放大算法模型枚举")]
    public int upscaler = 10;
    
    [Tooltip("放大后的宽度")]
    public int resizedWidth = 1024;
    
    [Tooltip("放大后的高度")]
    public int resizedHeight = 1536;
    
    [Header("调试选项")]
    [Tooltip("启用调试日志")]
    public bool enableDebugLog = true;
    
    // 事件定义
    public event Action<QueryResultResponse> OnImageGenerated;
    public event Action<string> OnError;
    public event Action<string> OnStatusUpdate; // 状态更新事件（processing状态）
    
    // 当前正在进行的任务
    private Coroutine currentTaskCoroutine;
    
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
            if (string.IsNullOrEmpty(checkPointId))
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
    }
    
    /// <summary>
    /// 生成图片（使用Inspector中设置的参数）
    /// </summary>
    public void Generate()
    {
        GenerateImageWithLora(
            checkPointId,
            prompt,
            negativePrompt,
            loraModels,
            templateUuid: null
        );
    }
    
    /// <summary>
    /// 生成图片（带LoRA）
    /// </summary>
    /// <param name="checkPointId">底模 modelVersionUUID（必填）</param>
    /// <param name="prompt">提示词（选填）</param>
    /// <param name="negativePrompt">负面提示词（选填）</param>
    /// <param name="loraModels">LoRA模型列表（最多5个）</param>
    /// <param name="templateUuid">模板UUID，如果为空则使用配置中的默认值</param>
    public void GenerateImageWithLora(
        string checkPointId,
        string prompt = null,
        string negativePrompt = null,
        LoraModel[] loraModels = null,
        string templateUuid = null)
    {
        if (string.IsNullOrEmpty(checkPointId))
        {
            OnError?.Invoke("checkPointId不能为空，请设置底模 modelVersionUUID");
            return;
        }
        
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
        string finalNegativePrompt = negativePrompt ?? this.negativePrompt;
        LoraModel[] finalLoraModels = loraModels ?? this.loraModels;
        
        // 开始新的生成任务
        currentTaskCoroutine = StartCoroutine(GenerateImageLoraCoroutine(
            checkPointId,
            finalPrompt,
            finalNegativePrompt,
            finalLoraModels,
            templateId
        ));
    }
    
    /// <summary>
    /// 生成图片协程（带LoRA）
    /// </summary>
    private IEnumerator GenerateImageLoraCoroutine(
        string checkPointId,
        string prompt,
        string negativePrompt,
        LoraModel[] loraModels,
        string templateUuid)
    {
        // 1. 提交文生图任务
        string generateUuid = null;
        yield return StartCoroutine(SubmitText2ImageLoraTask(
            checkPointId,
            prompt,
            negativePrompt,
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
        string checkPointId,
        string prompt,
        string negativePrompt,
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
        
        // 构建高分辨率修复信息
        HiResFixInfo hiResFixInfo = null;
        if (enableHiResFix)
        {
            hiResFixInfo = new HiResFixInfo
            {
                hiresSteps = this.hiresSteps,
                hiresDenoisingStrength = this.hiresDenoisingStrength,
                upscaler = this.upscaler,
                resizedWidth = this.resizedWidth,
                resizedHeight = this.resizedHeight
            };
        }
        
        // 构建请求体
        Text2ImageLoraRequest request = new Text2ImageLoraRequest
        {
            templateUuid = templateUuid,
            generateParams = new GenerateParamsLora
            {
                checkPointId = checkPointId,
                prompt = prompt,
                negativePrompt = negativePrompt,
                sampler = this.sampler,
                steps = this.steps,
                cfgScale = this.cfgScale,
                width = this.width,
                height = this.height,
                imgCount = this.imgCount,
                randnSource = this.randnSource,
                seed = this.seed,
                restoreFaces = this.restoreFaces,
                additionalNetwork = additionalNetwork,
                hiResFixInfo = hiResFixInfo
            }
        };
        
        string jsonBody = JsonUtility.ToJson(request);
        
        if (enableDebugLog)
        {
            Debug.Log($"[LiblibAPILora] 提交LoRA文生图任务 - CheckPointId: {checkPointId}, TemplateUUID: {templateUuid}");
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
                    
                    try
                    {
                        QueryResultResponse response = JsonUtility.FromJson<QueryResultResponse>(responseText);
                        
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
                        }
                        
                        // 如果任务完成（有图片数据或状态为完成）
                        if (isCompleted)
                        {
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
                    catch (Exception e)
                    {
                        if (enableDebugLog)
                        {
                            Debug.LogError($"[LiblibAPILora] 解析查询响应失败: {e.Message}");
                        }
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
        LiblibAPIConfig activeConfig = GetActiveConfig();
        OnError?.Invoke($"查询超时：已达到最大重试次数 ({activeConfig.maxRetryCount})");
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