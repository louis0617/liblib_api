using System;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Liblib API 认证辅助类
/// 用于生成签名等认证信息
/// 根据飞书文档调整此类的实现
/// </summary>
public static class LiblibAPIAuthHelper
{
    /// <summary>
    /// 生成 HMAC-SHA1 签名（官方要求使用 SHA1）
    /// </summary>
    /// <param name="data">要签名的数据</param>
    /// <param name="secretKey">密钥</param>
    /// <returns>签名的字节数组</returns>
    private static byte[] GenerateHMACSHA1(string data, string secretKey)
    {
        using (HMACSHA1 hmac = new HMACSHA1(Encoding.UTF8.GetBytes(secretKey)))
        {
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        }
    }
    
    /// <summary>
    /// URL 安全的 Base64 编码（不补全位数）
    /// </summary>
    /// <param name="input">要编码的字节数组</param>
    /// <returns>URL 安全的 Base64 字符串</returns>
    private static string EncodeBase64URLSafe(byte[] input)
    {
        string base64 = Convert.ToBase64String(input);
        // URL 安全：将 + 替换为 -，/ 替换为 _，并移除末尾的 = 填充
        return base64.Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
    
    /// <summary>
    /// 生成时间戳（毫秒时间戳）
    /// </summary>
    public static string GenerateTimestamp()
    {
        // 毫秒时间戳
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
    }
    
    /// <summary>
    /// 生成随机字符串（用于 SignatureNonce）
    /// </summary>
    public static string GenerateNonce()
    {
        return Guid.NewGuid().ToString("N");
    }
    
    /// <summary>
    /// 根据官方文档生成签名
    /// 原文 = URL地址 + "&" + 毫秒时间戳 + "&" + 随机字符串
    /// 密文 = hmacSha1(原文, SecretKey)
    /// 签名 = encodeBase64URLSafeString(密文)
    /// </summary>
    /// <param name="urlPath">URL地址（如：/api/genImg）</param>
    /// <param name="secretKey">SecretKey</param>
    /// <param name="timestamp">毫秒时间戳</param>
    /// <param name="nonce">随机字符串（SignatureNonce）</param>
    /// <returns>签名字符串（URL安全的Base64）</returns>
    public static string GenerateSignature(string urlPath, string secretKey, string timestamp, string nonce)
    {
        // 1. 用"&"拼接参数：URL地址 + "&" + 毫秒时间戳 + "&" + 随机字符串
        string signString = $"{urlPath}&{timestamp}&{nonce}";
        
        // 2. 用SecretKey加密原文，使用hmacsha1算法
        byte[] hashBytes = GenerateHMACSHA1(signString, secretKey);
        
        // 3. 生成url安全的base64签名（不补全位数）
        string signature = EncodeBase64URLSafe(hashBytes);
        
        // 调试输出（可以在Unity Console中查看）
        UnityEngine.Debug.Log($"[LiblibAuth] 签名字符串（原文）: {signString}");
        UnityEngine.Debug.Log($"[LiblibAuth] 生成的签名: {signature}");
        
        return signature;
    }
    
    /// <summary>
    /// 设置认证请求头（简单方式：直接使用AccessKey和SecretKey）
    /// </summary>
    public static void SetAuthHeaders(UnityEngine.Networking.UnityWebRequest request, string accessKey, string secretKey)
    {
        request.SetRequestHeader("AccessKey", accessKey);
        request.SetRequestHeader("SecretKey", secretKey);
    }
    
    /// <summary>
    /// 构建带签名的URL（根据官方文档，签名参数应作为URL查询参数）
    /// </summary>
    /// <param name="baseUrl">基础URL（如：https://openapi.liblibai.cloud）</param>
    /// <param name="urlPath">URL路径（如：/api/genImg）</param>
    /// <param name="accessKey">AccessKey</param>
    /// <param name="secretKey">SecretKey</param>
    /// <returns>带签名参数的完整URL</returns>
    public static string BuildSignedUrl(string baseUrl, string urlPath, string accessKey, string secretKey)
    {
        string timestamp = GenerateTimestamp();
        string nonce = GenerateNonce();
        string signature = GenerateSignature(urlPath, secretKey, timestamp, nonce);
        
        // 构建查询参数字符串：AccessKey=xxx&Signature=xxx&Timestamp=xxx&SignatureNonce=xxx
        string queryString = $"AccessKey={UnityEngine.Networking.UnityWebRequest.EscapeURL(accessKey)}" +
                            $"&Signature={UnityEngine.Networking.UnityWebRequest.EscapeURL(signature)}" +
                            $"&Timestamp={timestamp}" +
                            $"&SignatureNonce={UnityEngine.Networking.UnityWebRequest.EscapeURL(nonce)}";
        
        // 构建完整URL
        string fullUrl = $"{baseUrl}{urlPath}?{queryString}";
        
        // 调试输出（只在需要时输出，避免轮询时产生过多日志）
        // UnityEngine.Debug.Log($"[LiblibAuth] 构建签名URL - AccessKey: {accessKey}, Timestamp: {timestamp}, SignatureNonce: {nonce}");
        // UnityEngine.Debug.Log($"[LiblibAuth] 完整URL: {fullUrl}");
        
        return fullUrl;
    }
    
    /// <summary>
    /// 设置认证请求头（签名方式）- 已废弃，请使用 BuildSignedUrl
    /// 根据官方文档，签名参数应作为URL查询参数，而不是请求头
    /// </summary>
    [System.Obsolete("请使用 BuildSignedUrl 方法，将签名参数添加到URL查询字符串中")]
    public static void SetAuthHeadersWithSignature(UnityEngine.Networking.UnityWebRequest request, string accessKey, string secretKey, string urlPath)
    {
        string timestamp = GenerateTimestamp();
        string nonce = GenerateNonce();
        string signature = GenerateSignature(urlPath, secretKey, timestamp, nonce);
        
        // 设置请求头（根据官方文档）
        request.SetRequestHeader("AccessKey", accessKey);
        request.SetRequestHeader("Timestamp", timestamp);
        request.SetRequestHeader("SignatureNonce", nonce);  // 注意：字段名是 SignatureNonce，不是 Nonce
        request.SetRequestHeader("Signature", signature);
        
        // 调试输出
        UnityEngine.Debug.Log($"[LiblibAuth] 设置认证头 - AccessKey: {accessKey}, Timestamp: {timestamp}, SignatureNonce: {nonce}, Signature: {signature}");
    }
}


