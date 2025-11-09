# Liblib LoRA API 使用指南

本指南将帮助您在Unity中使用 `LiblibAPILora` 组件调用带LoRA模型的文生图API。

## 一、准备工作

### 1. 创建API配置资源

1. 在Unity编辑器中，右键点击 `Project` 窗口
2. 选择 `Create > Liblib > API Config`
3. 将创建的配置资源命名为 `LiblibAPIConfig`
4. 在Inspector中填写：
   - **AccessKey**: 从 liblib.ai API 开发平台获取
   - **SecretKey**: 从 liblib.ai API 开发平台获取
   - **API Base Url**: `https://openapi.liblibai.cloud`（默认值）
   - **Template UUID**: 模板UUID（可选，有默认值）

### 2. 获取模型版本UUID

#### 获取底模（Checkpoint）UUID：
1. 访问 liblib.ai 网站
2. 在站内检索可商用的Checkpoint模型
3. 选择喜欢的模型版本
4. 从浏览器网址中复制 `versionUuid`

#### 获取LoRA模型UUID：
1. 访问 liblib.ai 网站
2. 在站内检索可商用的LoRA模型
3. 选择喜欢的LoRA模型版本
4. 从浏览器网址中复制 `versionUuid`

## 二、在Unity场景中设置

### 方法一：使用GameObject组件（推荐）

1. **创建GameObject**：
   - 在场景中创建一个空的GameObject（例如命名为 `LiblibAPILoraClient`）

2. **添加组件**：
   - 选中GameObject
   - 点击 `Add Component`
   - 搜索并添加 `LiblibAPILora` 组件

3. **配置组件**：
   - 将之前创建的 `LiblibAPIConfig` 资源拖拽到 **API Config** 字段
   - 填写以下参数：
     - **Check Point Id**: 底模的 `versionUuid`（必填）
     - **Prompt**: 提示词（英文，不超过2000字符）
     - **Negative Prompt**: 负面提示词（可选）
     - **LoRA Models**: LoRA模型列表（最多5个）
       - 点击 `+` 添加LoRA模型
       - 每个模型需要填写：
         - **Model Id**: LoRA的 `versionUuid`
         - **Weight**: LoRA权重（0.0-2.0，建议0.3-1.0）
     - **Sampler**: 采样方法（默认15）
     - **Steps**: 采样步数（默认20，范围1-100）
     - **Cfg Scale**: 提示词引导系数（默认7，范围1-30）
     - **Width/Height**: 图片尺寸（默认768x1024）
     - **Img Count**: 生成图片数量（默认1，范围1-4）
     - **Seed**: 随机种子（-1表示随机）
     - **Restore Faces**: 面部修复（0=关闭，1=开启）

4. **高分辨率修复（可选）**：
   - 勾选 **Enable Hi Res Fix**
   - 设置相关参数：
     - **Hires Steps**: 重绘步数
     - **Hires Denoising Strength**: 重绘幅度（0.0-1.0）
     - **Upscaler**: 放大算法模型枚举
     - **Resized Width/Height**: 放大后的尺寸
