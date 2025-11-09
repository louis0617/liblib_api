# Liblib API Unity 使用指南

本文档说明如何在Unity中使用Liblib AI文生图API。

## 📋 前置要求

1. Unity 2019.4 或更高版本
2. 有效的Liblib AI API密钥（AccessKey 和 SecretKey）

## 🚀 快速开始

### 1. 创建API配置

1. 在Unity编辑器中，右键点击Project窗口
2. 选择 `Create > Liblib > API Config`
3. 创建配置文件（例如：`LiblibAPIConfig`）
4. 在Inspector中填入您的API密钥：
   - **AccessKey**: 从Liblib AI平台获取
   - **SecretKey**: 从Liblib AI平台获取
   - **Template UUID**: 默认使用星流Star-3 Alpha模板（`5d7e67009b344550bc1aa6ccbfa1d7f4`）

### 2. 设置场景

1. 在场景中创建一个空的GameObject（例如：`LiblibAPIManager`）
2. 添加 `LiblibAPIClient` 组件
3. 将创建的 `LiblibAPIConfig` 资源拖拽到组件的 `Api Config` 字段
4. （可选）启用 `Enable Debug Log` 以查看详细日志

### 3. 基本使用

#### 方法1：使用示例脚本

1. 在同一个GameObject上添加 `LiblibAPIExample` 组件
2. （可选）设置UI引用：
   - `Prompt Input Field`: 输入提示词的输入框
   - `Status Text`: 显示状态的文本
   - `Image Display`: 显示生成图片的RawImage
3. 运行场景，调用 `GenerateImage()` 方法

#### 方法2：代码调用

```csharp
using UnityEngine;
using LiblibAPI;

public class MyScript : MonoBehaviour
{
    public LiblibAPIClient apiClient;
    
    void Start()
    {
        // 订阅事件
        apiClient.OnImageGenerated += OnImageGenerated;
        apiClient.OnError += OnError;
        apiClient.OnStatusUpdate += OnStatusUpdate;
        
        // 生成图片
        apiClient.GenerateImage("a beautiful sunset over the ocean, high quality, detailed");
    }
    
    void OnImageGenerated(QueryResultResponse response)
    {
        Debug.Log($"图片生成成功！URL: {response.imageUrl}");
        
        // 下载图片
        apiClient.DownloadImage(response.imageUrl, (texture) => {
            // 使用texture，例如显示在UI上
            // myRawImage.texture = texture;
        });
    }
    
    void OnError(string error)
    {
        Debug.LogError($"生成失败: {error}");
    }
    
    void OnStatusUpdate(string status)
    {
        Debug.Log($"状态: {status}");
    }
}
```

## 📝 API说明

### LiblibAPIClient 主要方法

#### GenerateImage(string prompt, string templateUuid = null)

生成图片（文生图）

- **prompt**: 提示词（英文，不超过2000字符）
- **templateUuid**: 模板UUID，如果为空则使用配置中的默认值

#### DownloadImage(string imageUrl, Action<Texture2D> onSuccess, Action<string> onError = null)

下载图片

- **imageUrl**: 图片URL
- **onSuccess**: 成功回调，返回Texture2D
- **onError**: 失败回调（可选）

#### CancelCurrentTask()

取消当前正在进行的生成任务

### 事件

- **OnImageGenerated**: 图片生成成功时触发
- **OnError**: 发生错误时触发
- **OnStatusUpdate**: 状态更新时触发（例如：处理中）

## ⚙️ 配置说明

### LiblibAPIConfig 参数

- **AccessKey**: API访问凭证
- **SecretKey**: API访问密钥
- **API Base URL**: API基础地址（默认：`https://openapi.liblibai.cloud`）
- **Template UUID**: 模板UUID（默认：星流Star-3 Alpha）
- **Max Retry Count**: 查询结果的最大重试次数（默认：60次）
- **Query Interval**: 每次查询的间隔时间（默认：2秒）

## 🔍 工作流程

1. **提交任务**: 调用 `GenerateImage()` 提交文生图任务
2. **获取UUID**: API返回 `generateUuid`
3. **轮询查询**: 自动轮询查询结果，直到状态为 `success` 或 `failed`
4. **下载图片**: 生成成功后，调用 `DownloadImage()` 下载图片

## 🐛 调试

启用 `LiblibAPIClient` 组件上的 `Enable Debug Log` 选项，可以在Console中查看：

- 请求URL和请求体
- 响应内容
- 查询状态更新
- 错误信息

## ⚠️ 注意事项

1. **提示词格式**: 提示词必须是英文，不超过2000字符
2. **网络连接**: 确保设备可以访问 `https://openapi.liblibai.cloud`
3. **API密钥**: 确保AccessKey和SecretKey正确且有效
4. **轮询间隔**: 根据实际情况调整 `Query Interval`，避免过于频繁的请求
5. **超时设置**: 如果图片生成时间较长，可以增加 `Max Retry Count`

## 📚 参考文档

- [Liblib AI API 配置指南](https://github.com/LJY227/interactive-storybook/blob/main/docs/LIBLIB_API_SETUP.md)
- [Liblib AI 官网](https://www.liblib.art/)

## 🔧 故障排除

### 问题：提交任务失败

- 检查API密钥是否正确
- 检查网络连接
- 查看Console中的错误日志

### 问题：查询超时

- 增加 `Max Retry Count`
- 检查网络连接
- 查看API服务状态

### 问题：图片下载失败

- 检查图片URL是否有效
- 检查网络连接
- 查看Console中的错误日志

