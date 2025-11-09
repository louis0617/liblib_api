# Liblib API Unity SDK

Unity 插件，用于调用 Liblib AI 文生图 API，支持基础文生图和带 LoRA 模型的文生图功能。

## 📋 前置要求

1. Unity 2019.4 或更高版本
2. 有效的 Liblib AI API 密钥（AccessKey 和 SecretKey）
3. 网络连接（需要访问 `https://openapi.liblibai.cloud`）

## 🚀 快速开始

### 1. 创建 API 配置

#### 基础 API 配置（LiblibAPIConfig）

1. 在 Unity 编辑器中，右键点击 Project 窗口
2. 选择 `Create > Liblib > API Config`
3. 将创建的配置资源命名为 `LiblibAPIConfig`
4. 在 Inspector 中填写：
   - **AccessKey**: 从 Liblib AI 开发平台获取
   - **SecretKey**: 从 Liblib AI 开发平台获取
   - **API Base Url**: `https://openapi.liblibai.cloud`（默认值）
   - **Template UUID**: 模板 UUID（默认：星流Star-3 Alpha `5d7e67009b344550bc1aa6ccbfa1d7f4`）
   - **Max Retry Count**: 查询结果的最大重试次数（默认：60次）
   - **Query Interval**: 每次查询的间隔时间（默认：2秒）

#### LoRA API 配置（LiblibAPILoraConfig，可选）

1. 右键点击 Project 窗口
2. 选择 `Create > Liblib > LoRA API Config`
3. 命名为 `LiblibAPILoraConfig`
4. 配置参数：
   - 填写 AccessKey 和 SecretKey
   - 设置默认底模 ID (defaultCheckPointId)
   - 配置默认 LoRA 模型列表
   - 设置其他默认参数（提示词、采样步数等）

### 2. 获取模型版本 UUID

#### 获取底模（Checkpoint）UUID：

1. 访问 [liblib.ai](https://www.liblib.art/) 网站
2. 在站内检索可商用的 Checkpoint 模型
3. 选择喜欢的模型版本
4. 从浏览器网址中复制 `versionUuid`

例如：`https://www.liblib.art/modelinfo/xxx?versionUuid=412b427ddb674b4dbab9e5abd5ae6057`

#### 获取 LoRA 模型 UUID：

1. 访问 [liblib.ai](https://www.liblib.art/) 网站
2. 在站内检索可商用的 LoRA 模型
3. 选择喜欢的 LoRA 模型版本
4. 从浏览器网址中复制 `versionUuid`

## 📖 使用指南

### 基础文生图（LiblibAPIClient）

#### 设置场景

1. 在场景中创建一个空的 GameObject（例如：`LiblibAPIManager`）
2. 添加 `LiblibAPIClient` 组件
3. 将创建的 `LiblibAPIConfig` 资源拖拽到组件的 `Api Config` 字段
4. （可选）启用 `Enable Debug Log` 以查看详细日志

#### 方法1：使用示例脚本

1. 在同一个 GameObject 上添加 `LiblibAPIExample` 组件
2. （可选）设置 UI 引用：
   - `Prompt Input Field`: 输入提示词的输入框
   - `Status Text`: 显示状态的文本
   - `Image Display`: 显示生成图片的 RawImage
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
        Debug.Log($"图片生成成功！");
        
        // 下载图片
        apiClient.DownloadImage(response.data.imageUrl, (texture) => {
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

### LoRA 文生图（LiblibAPILora）

#### 设置场景

1. **创建 GameObject**：
   - 在场景中创建一个空的 GameObject（例如命名为 `LiblibAPILoraClient`）

2. **添加组件**：
   - 选中 GameObject
   - 点击 `Add Component`
   - 搜索并添加 `LiblibAPILora` 组件

3. **配置组件**：
   - 将之前创建的 `LiblibAPILoraConfig` 资源拖拽到 **Lora Config** 字段（或使用 `Api Config` 作为向后兼容）
   - 填写以下参数：
     - **Check Point Id**: 底模的 `versionUuid`（选填，如果需要指定特定的 Checkpoint 模型）
     - **Prompt**: 提示词（英文，不超过2000字符）
     - **Negative Prompt**: 负面提示词（可选）
     - **LoRA Models**: LoRA 模型列表（最多5个）
       - 点击 `+` 添加 LoRA 模型
       - 每个模型需要填写：
         - **Model Id**: LoRA 的 `versionUuid`
         - **Weight**: LoRA 权重（0.0-2.0，建议 0.3-1.0）
     - **Sampler**: 采样方法（默认15）
     - **Steps**: 采样步数（默认20，范围1-100）
     - **Cfg Scale**: 提示词引导系数（默认7，范围1-30）
     - **Width/Height**: 图片尺寸（默认768x1024）
     - **Img Count**: 生成图片数量（默认1，范围1-4）
     - **Seed**: 随机种子（-1表示随机）
     - **Restore Faces**: 面部修复（0=关闭，1=开启）

#### 按钮配置（推荐）

1. **创建按钮**：
   - 在场景中创建多个 Button（例如：`lora_merge`、`1_lora`、`2_lora`）

2. **配置按钮**：
   - 在 `LiblibAPILora` 组件的 Inspector 中，展开 **按钮设置** 部分
   - 设置 `Button Configs` 数组大小（例如：3个）
   - 为每个按钮配置：
     - 拖入对应的 Button
     - 设置 **Button Name**（例如：`lora_merge`、`1_lora`、`2_lora`）
     - 设置 **Lora Count**（使用的 LoRA 数量，0-5）
     - 可选：启用 `Use Custom Prompt` 并设置自定义提示词

3. **运行游戏**：
   - 点击不同按钮会自动使用对应数量的 LoRA 模型进行生成
   - 图片会自动保存到 `GeneratedImages` 文件夹

#### 图片自动保存

- **自动保存**：图片生成成功后会自动下载并保存到本地文件夹
- **保存位置**：项目根目录下的 `GeneratedImages` 文件夹（可在 Inspector 中修改 `Save Folder Path`）
- **文件命名**：`{buttonName}_{timestamp}.png`
  - 例如：`lora_merge_20241201_143022.png`
  - 多张图片：`lora_merge_20241201_143022_1.png`、`lora_merge_20241201_143022_2.png`

## 📝 API 说明

### LiblibAPIClient 主要方法

#### GenerateImage(string prompt, string templateUuid = null)

生成图片（文生图）

- **prompt**: 提示词（英文，不超过2000字符）
- **templateUuid**: 模板 UUID，如果为空则使用配置中的默认值

#### DownloadImage(string imageUrl, Action<Texture2D> onSuccess, Action<string> onError = null)

下载图片

- **imageUrl**: 图片 URL
- **onSuccess**: 成功回调，返回 Texture2D
- **onError**: 失败回调（可选）

#### CancelCurrentTask()

取消当前正在进行的生成任务

### LiblibAPILora 主要方法

#### Generate()

生成图片（使用 Inspector 中设置的参数）

#### GenerateImageWithLora(string prompt = null, LoraModel[] loraModels = null, string templateUuid = null)

生成图片（带 LoRA）

- **prompt**: 提示词（选填）
- **loraModels**: LoRA 模型列表（最多5个）
- **templateUuid**: 模板 UUID，如果为空则使用配置中的默认值

#### DownloadImage(string imageUrl, Action<Texture2D> onSuccess, Action<string> onError = null)

下载图片

### 事件

- **OnImageGenerated**: 图片生成成功时触发
- **OnError**: 发生错误时触发
- **OnStatusUpdate**: 状态更新时触发（例如：处理中）

## ⚙️ 配置说明

### LiblibAPIConfig 参数

- **AccessKey**: API 访问凭证
- **SecretKey**: API 访问密钥
- **API Base URL**: API 基础地址（默认：`https://openapi.liblibai.cloud`）
- **Template UUID**: 模板 UUID（默认：星流Star-3 Alpha）
- **Max Retry Count**: 查询结果的最大重试次数（默认：60次）
- **Query Interval**: 每次查询的间隔时间（默认：2秒）

### LiblibAPILoraConfig 参数

包含所有 `LiblibAPIConfig` 的参数，并额外提供：

- **Default Check Point Id**: 默认底模 ID
- **Default Prompt**: 默认提示词
- **Default Negative Prompt**: 默认负面提示词
- **Default LoRA Models**: 默认 LoRA 模型列表
- **Default Sampler/Steps/CfgScale**: 默认生成参数

### LiblibAPILora 图片保存设置

- **Auto Save Images**: 是否自动保存生成的图片（默认：开启）
- **Save Folder Path**: 保存图片的文件夹路径（默认：`GeneratedImages`，相对于项目根目录）

## 🔍 工作流程

1. **提交任务**: 调用生成方法提交文生图任务
2. **获取 UUID**: API 返回 `generateUuid`
3. **轮询查询**: 自动轮询查询结果，直到状态为完成或失败
4. **下载图片**: 生成成功后，自动下载并保存图片（如果启用了自动保存）

## 🐛 调试

启用组件上的 `Enable Debug Log` 选项，可以在 Console 中查看：

- 请求 URL 和请求体
- 响应内容
- 查询状态更新
- 错误信息
- 保存文件夹路径

## ⚠️ 注意事项

1. **提示词格式**: 提示词必须是英文，不超过2000字符
2. **网络连接**: 确保设备可以访问 `https://openapi.liblibai.cloud`
3. **API 密钥**: 确保 AccessKey 和 SecretKey 正确且有效
4. **轮询间隔**: 根据实际情况调整 `Query Interval`，避免过于频繁的请求
5. **超时设置**: 如果图片生成时间较长，可以增加 `Max Retry Count`
6. **LoRA 数量限制**: 最多支持 5 个 LoRA 模型同时使用
7. **文件保存**: 确保有写入权限，文件夹会在游戏启动时自动创建

## 🔒 安全提示

1. **不要提交密钥到 Git**
   - 确保 `LiblibAPIConfig.asset` 和 `LiblibAPILoraConfig.asset` 文件不会被提交到版本控制系统
   - 建议将这些文件添加到 `.gitignore`

2. **保护您的密钥**
   - 不要将密钥分享给他人
   - 不要在公开场合展示密钥
   - 如果密钥泄露，请及时在 liblib.ai 平台重新生成

3. **使用环境变量（可选）**
   - 对于生产环境，考虑使用环境变量或加密存储
   - 可以在运行时从安全位置读取密钥

## 🔧 故障排除

### 问题：提交任务失败

- 检查 API 密钥是否正确
- 检查网络连接
- 查看 Console 中的错误日志
- 确认 API Base URL 是否正确

### 问题：查询超时

- 增加 `Max Retry Count`
- 检查网络连接
- 查看 API 服务状态

### 问题：图片下载失败

- 检查图片 URL 是否有效
- 检查网络连接
- 查看 Console 中的错误日志

### 问题：找不到保存文件夹

- 确认 `Auto Save Images` 已启用
- 查看 Console 日志，确认文件夹路径
- 检查是否有写入权限
- 文件夹会在游戏启动时自动创建

### 问题：按钮点击无响应

- 确认按钮已正确拖入 `Button Configs` 数组
- 检查 `Button Name` 是否已填写
- 确认 `Lora Count` 设置正确
- 查看 Console 日志确认按钮绑定状态

## 📚 参考文档

- [Liblib AI 官网](https://www.liblib.art/)
- [Liblib AI API 文档](https://openapi.liblibai.cloud)

## 📞 需要帮助？

如果遇到问题：

1. 检查 Console 中的错误信息
2. 确认配置文件中的密钥是否正确填写
3. 确认组件已正确引用配置文件
4. 启用 `Enable Debug Log` 查看详细日志

