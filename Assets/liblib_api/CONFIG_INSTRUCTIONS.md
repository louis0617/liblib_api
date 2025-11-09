# 配置说明

## 🔑 您的 API 密钥

您已提供以下密钥信息：

- **AccessKey**: `ybgsJ3-CJYtiJJptA70lWg`
- **SecretKey**: `c1Z1wIj15LvZgSUjf4Q0raIlFU5TXYD2`

## 📝 在 Unity 中配置步骤

### 方法 1: 使用 Unity 编辑器（推荐）

1. **创建配置文件**
   - 在 Unity 编辑器中，右键点击 Project 窗口（建议在 `Assets` 或 `Resources` 文件夹下）
   - 选择 `Create > Liblib > API Config`
   - 将文件命名为 `LiblibAPIConfig`

2. **填写密钥信息**
   - 选中刚创建的 `LiblibAPIConfig` 文件
   - 在 Inspector 窗口中，找到 `API 配置` 部分
   - 在 `Access Key` 字段中填入：`ybgsJ3-CJYtiJJptA70lWg`
   - 在 `Secret Key` 字段中填入：`c1Z1wIj15LvZgSUjf4Q0raIlFU5TXYD2`
   - 确认 `Api Base Url` 为：`https://openapi.liblibai.cloud`（或根据飞书文档调整）

3. **配置场景**
   - 在场景中创建或选择一个 GameObject
   - 添加 `LiblibAPIClient` 组件
   - 将 `LiblibAPIConfig` 文件拖拽到组件的 `Api Config` 字段

### 方法 2: 直接修改代码（仅用于测试，不推荐）

如果您想快速测试，可以临时在 `LiblibAPIConfig.cs` 中设置默认值：

```csharp
public string accessKey = "ybgsJ3-CJYtiJJptA70lWg";
public string secretKey = "c1Z1wIj15LvZgSUjf4Q0raIlFU5TXYD2";
```

**⚠️ 警告**: 不要将包含密钥的代码提交到版本控制系统！

## ✅ 验证配置

配置完成后，可以通过以下方式验证：

1. 在 `LiblibAPIClient` 组件上，确保 `Api Config` 字段已赋值
2. 运行场景，调用 `GenerateImage` 方法
3. 查看 Console 日志，确认请求是否成功

## 🔒 安全提示

1. **不要提交密钥到 Git**
   - 已创建 `.gitignore` 文件来防止配置文件被提交
   - 如果使用 Git，确保 `LiblibAPIConfig.asset` 文件不会被提交

2. **保护您的密钥**
   - 不要将密钥分享给他人
   - 不要在公开场合展示密钥
   - 如果密钥泄露，请及时在 liblib.ai 平台重新生成

3. **使用环境变量（可选）**
   - 对于生产环境，考虑使用环境变量或加密存储
   - 可以在运行时从安全位置读取密钥

## 🚀 快速测试

配置完成后，可以使用以下代码测试：

```csharp
using UnityEngine;

public class TestLiblibAPI : MonoBehaviour
{
    private LiblibAPIClient apiClient;
    
    void Start()
    {
        apiClient = GetComponent<LiblibAPIClient>();
        
        // 订阅事件
        apiClient.OnImageGenerated += (response) => {
            Debug.Log($"成功！图片URL: {response.data.image_url}");
        };
        
        apiClient.OnError += (error) => {
            Debug.LogError($"错误: {error}");
        };
        
        // 测试生成图片
        apiClient.GenerateImage("a beautiful sunset");
    }
}
```

## 📞 需要帮助？

如果遇到配置问题：
1. 检查 Console 中的错误信息
2. 确认 `LiblibAPIConfig` 文件中的密钥是否正确填写
3. 确认 `LiblibAPIClient` 组件已正确引用配置文件

