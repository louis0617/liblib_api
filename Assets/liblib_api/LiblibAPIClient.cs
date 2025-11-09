using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using LiblibAPI;

/// <summary>
/// Liblib API 客户端
/// 用于调用 Liblib AI 文生图 API
/// </summary>
public class LiblibAPIClient : MonoBehaviour
{
    [Header("API 配置")]
    [Tooltip("API 配置资源")]
    public LiblibAPIConfig apiConfig;
    
    [Header("生成设置")]
    [Tooltip("固定提示词（英文，不超过2000字符）")]
    [TextArea(3, 10)]
    public string fixedPrompt = "a beautiful sunset over the ocean, high quality, detailed";
    
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
    /// 生成图片（使用固定提示词，可从按钮调用）
    /// </summary>
    public void Generate()
    {
        if (string.IsNullOrEmpty(fixedPrompt))
        {
            OnError?.Invoke("固定提示词为空，请在Inspector中设置");
            return;
        }
        
        GenerateImage(fixedPrompt);
    }
    
    /// <summary>
    /// 生成图片（文生图）
    /// </summary>
    /// <param name="prompt">提示词（英文，不超过2000字符）</param>
    /// <param name="templateUuid">模板UUID，如果为空则使用配置中的默认值</param>
    public void GenerateImage(string prompt, string templateUuid = null)
    {
        if (string.IsNullOrEmpty(prompt))
        {
            OnError?.Invoke("提示词不能为空");
            return;
        }
        
        if (apiConfig == null || !apiConfig.IsValid())
        {
            OnError?.Invoke("API配置无效，请检查AccessKey和SecretKey");
            return;
        }
        
        // 停止之前的任务
        if (currentTaskCoroutine != null)
        {
            StopCoroutine(currentTaskCoroutine);
        }
        
        // 使用配置中的templateUuid如果未提供
        string templateId = string.IsNullOrEmpty(templateUuid) ? apiConfig.templateUuid : templateUuid;
        
        // 开始新的生成任务
        currentTaskCoroutine = StartCoroutine(GenerateImageCoroutine(prompt, templateId));
    }
    
    /// <summary>
    /// 生成图片协程（文生图）
    /// </summary>
    private IEnumerator GenerateImageCoroutine(string prompt, string templateUuid)
    {
        // 1. 提交文生图任务
        string generateUuid = null;
        yield return StartCoroutine(SubmitText2ImageTask(prompt, templateUuid, (uuid) => {
            generateUuid = uuid;
        }));
        
        if (string.IsNullOrEmpty(generateUuid))
        {
            // 提交任务失败，错误已在SubmitText2ImageTask中处理
            yield break;
        }
        
        if (enableDebugLog)
        {
            Debug.Log($"[LiblibAPI] 任务已提交，generateUuid: {generateUuid}");
        }
        
        // 2. 轮询查询结果
        yield return StartCoroutine(QueryResultCoroutine(generateUuid));
    }
    
    /// <summary>
    /// 提交文生图任务
    /// </summary>
    private IEnumerator SubmitText2ImageTask(string prompt, string templateUuid, Action<string> onSuccess)
    {
        // 计算宽高比（使用字符串值：portrait, landscape, square）
        string aspectRatio = GetAspectRatioString(apiConfig.defaultWidth, apiConfig.defaultHeight);
        
        // 构建请求体（ultra接口只需要aspectRatio和imgCount，不需要imageSize）
        Text2ImageRequest request = new Text2ImageRequest
        {
            templateUuid = templateUuid,
            generateParams = new GenerateParams
            {
                prompt = prompt,
                aspectRatio = aspectRatio,
                imgCount = 1  // 默认生成1张图片
            }
        };
        
        string jsonBody = JsonUtility.ToJson(request);
        
        if (enableDebugLog)
        {
            Debug.Log($"[LiblibAPI] 提交文生图任务 - Prompt: {prompt}, TemplateUUID: {templateUuid}");
            Debug.Log($"[LiblibAPI] 请求体: {jsonBody}");
        }
        
        // 构建带签名的URL
        string urlPath = "/api/generate/webui/text2img/ultra";
        string signedUrl = LiblibAPIAuthHelper.BuildSignedUrl(
            apiConfig.apiBaseUrl,
            urlPath,
            apiConfig.accessKey,
            apiConfig.secretKey
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
                Debug.Log($"[LiblibAPI] 发送请求到: {signedUrl}");
            }
            
            // 发送请求
            yield return request_web.SendWebRequest();
            
            // 处理响应
            string responseText = request_web.downloadHandler.text;
            
            if (enableDebugLog)
            {
                Debug.Log($"[LiblibAPI] 提交任务响应: {responseText}");
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
                    Debug.LogError($"[LiblibAPI] {errorMessage}");
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
        int retryCount = 0;
        
        while (retryCount < apiConfig.maxRetryCount)
        {
            yield return new WaitForSeconds(apiConfig.queryInterval);
            
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
                Debug.Log($"[LiblibAPI] 查询结果 (第{retryCount + 1}次) - generateUuid: {generateUuid}");
                Debug.Log($"[LiblibAPI] 查询请求体: {jsonBody}");
            }
            
            // 构建带签名的URL
            string signedUrl = LiblibAPIAuthHelper.BuildSignedUrl(
                apiConfig.apiBaseUrl,
                urlPath,
                apiConfig.accessKey,
                apiConfig.secretKey
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
                                Debug.LogWarning($"[LiblibAPI] 查询失败: {errorMsg}");
                            }
                            retryCount++;
                            continue;
                        }
                        
                        if (response.data == null)
                        {
                            if (enableDebugLog)
                            {
                                Debug.LogWarning($"[LiblibAPI] 查询响应中data为空");
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
                            Debug.Log($"[LiblibAPI] 查询结果:");
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
                            Debug.LogError($"[LiblibAPI] 解析查询响应失败: {e.Message}");
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
                        Debug.LogWarning($"[LiblibAPI] {errorMessage}");
                    }
                    retryCount++;
                    continue;
                }
            }
        }
        
        // 达到最大重试次数
        OnError?.Invoke($"查询超时：已达到最大重试次数 ({apiConfig.maxRetryCount})");
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
            Debug.Log($"[LiblibAPI] 开始下载图片: {imageUrl}");
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
                    Debug.Log($"[LiblibAPI] 图片下载成功: {imageUrl}");
                }
            }
            else
            {
                string errorMsg = $"下载图片失败: {request.error}";
                if (enableDebugLog)
                {
                    Debug.LogError($"[LiblibAPI] {errorMsg}");
                }
                onError?.Invoke(errorMsg);
            }
        }
    }
    
    /// <summary>
    /// 根据宽高获取宽高比字符串（portrait, landscape, square）
    /// </summary>
    private string GetAspectRatioString(int width, int height)
    {
        if (width == height)
        {
            return "square";
        }
        else if (width > height)
        {
            return "landscape";
        }
        else
        {
            return "portrait";
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
        
        if (apiConfig == null || !apiConfig.IsValid())
        {
            OnError?.Invoke("API配置无效，请检查AccessKey和SecretKey");
            return;
        }
        
        StartCoroutine(QueryStatusCoroutine(generateUuid));
    }
    
    /// <summary>
    /// 查询状态协程（单次查询，不轮询）
    /// </summary>
    private IEnumerator QueryStatusCoroutine(string generateUuid)
    {
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
            Debug.Log($"[LiblibAPI] 查询状态 - generateUuid: {generateUuid}");
            Debug.Log($"[LiblibAPI] 查询请求体: {jsonBody}");
        }
        
        // 构建带签名的URL
        string signedUrl = LiblibAPIAuthHelper.BuildSignedUrl(
            apiConfig.apiBaseUrl,
            urlPath,
            apiConfig.accessKey,
            apiConfig.secretKey
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
                Debug.Log($"[LiblibAPI] 查询状态响应: {responseText}");
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
                            
                            Debug.Log($"[LiblibAPI] 查询结果:");
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
                            Debug.LogWarning("[LiblibAPI] 查询响应中data为空");
                            OnError?.Invoke("查询响应中data为空");
                        }
                    }
                    else
                    {
                        string errorMsg = !string.IsNullOrEmpty(response.msg) ? response.msg : $"API返回错误码: {response.code}";
                        Debug.LogError($"[LiblibAPI] 查询失败: {errorMsg}");
                        OnError?.Invoke($"查询失败: {errorMsg}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[LiblibAPI] 解析查询响应失败: {e.Message}");
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
                
                Debug.LogError($"[LiblibAPI] {errorMessage}");
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

