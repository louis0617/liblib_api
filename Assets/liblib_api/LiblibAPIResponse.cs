using System;
using UnityEngine;

/// <summary>
/// Liblib API 响应数据模型
/// </summary>
namespace LiblibAPI
{
    /// <summary>
    /// 文生图请求参数
    /// </summary>
    [Serializable]
    public class Text2ImageRequest
    {
        /// <summary>
        /// 模板UUID（星流Star-3 Alpha模板ID）
        /// </summary>
        public string templateUuid;
        
        /// <summary>
        /// 生成参数
        /// </summary>
        public GenerateParams generateParams;
    }
    
    /// <summary>
    /// 生成参数
    /// </summary>
    [Serializable]
    public class GenerateParams
    {
        /// <summary>
        /// 提示词（英文，不超过2000字符）
        /// </summary>
        public string prompt;
        
        /// <summary>
        /// 宽高比（例如："portrait", "landscape", "square"）
        /// </summary>
        public string aspectRatio;
        
        /// <summary>
        /// 生成图片数量
        /// </summary>
        public int imgCount;
        
        /// <summary>
        /// 参考图片URL（图生图时使用）
        /// </summary>
        public string[] init_images;
    }
    
    /// <summary>
    /// 提交任务响应
    /// </summary>
    [Serializable]
    public class SubmitTaskResponse
    {
        public int code;
        public SubmitTaskData data;
        public string msg;
    }
    
    /// <summary>
    /// 提交任务响应数据
    /// </summary>
    [Serializable]
    public class SubmitTaskData
    {
        /// <summary>
        /// 生成任务的UUID
        /// </summary>
        public string generateUuid;
    }
    
    /// <summary>
    /// 查询状态请求
    /// </summary>
    [Serializable]
    public class QueryStatusRequest
    {
        /// <summary>
        /// 生成的图片任务ID
        /// </summary>
        public string generateUuid;
    }
    
    /// <summary>
    /// 查询结果响应
    /// </summary>
    [Serializable]
    public class QueryResultResponse
    {
        public int code;
        public QueryResultData data;
        public string msg;
    }
    
    /// <summary>
    /// 查询结果响应数据
    /// </summary>
    [Serializable]
    public class QueryResultData
    {
        /// <summary>
        /// 生成任务UUID
        /// </summary>
        public string generateUuid;
        
        /// <summary>
        /// 生成状态（数字，5表示完成）
        /// </summary>
        public int generateStatus;
        
        /// <summary>
        /// 完成百分比（0.0-1.0）
        /// </summary>
        public float percentCompleted;
        
        /// <summary>
        /// 生成消息
        /// </summary>
        public string generateMsg;
        
        /// <summary>
        /// 消耗的积分
        /// </summary>
        public int pointsCost;
        
        /// <summary>
        /// 账户余额
        /// </summary>
        public int accountBalance;
        
        /// <summary>
        /// 图片数组（对象数组，每个对象包含imageUrl等）
        /// </summary>
        public ImageResult[] images;
        
        /// <summary>
        /// 视频数组
        /// </summary>
        public object[] videos;
        
        /// <summary>
        /// 音频数组
        /// </summary>
        public object[] audios;
        
        // 兼容旧版本的字段（用于向后兼容）
        /// <summary>
        /// 状态：success|failed|processing（兼容字段）
        /// </summary>
        public string status;
        
        /// <summary>
        /// 生成的图片URL（单个图片时使用，兼容字段）
        /// </summary>
        public string imageUrl;
        
        /// <summary>
        /// 状态信息（兼容字段）
        /// </summary>
        public string message;
    }
    
    /// <summary>
    /// 图片结果
    /// </summary>
    [Serializable]
    public class ImageResult
    {
        /// <summary>
        /// 图片URL
        /// </summary>
        public string imageUrl;
        
        /// <summary>
        /// 随机种子
        /// </summary>
        public long seed;
        
        /// <summary>
        /// 审核状态
        /// </summary>
        public int auditStatus;
    }
    
    /// <summary>
    /// API错误响应
    /// </summary>
    [Serializable]
    public class APIErrorResponse
    {
        public int code;
        public object data;
        public string msg;
    }
    
    /// <summary>
    /// LoRA文生图请求参数
    /// </summary>
    [Serializable]
    public class Text2ImageLoraRequest
    {
        /// <summary>
        /// 模板UUID
        /// </summary>
        public string templateUuid;
        
        /// <summary>
        /// 生成参数（包含LoRA相关参数）
        /// </summary>
        public GenerateParamsLora generateParams;
    }
    
    /// <summary>
    /// LoRA生成参数（按照官方文档F.1文生图 - 自定义完整参数示例）
    /// </summary>
    [Serializable]
    public class GenerateParamsLora
    {
        /// <summary>
        /// 底模 modelVersionUUID（选填，如果需要指定特定的Checkpoint模型）
        /// 从模型页面URL中获取versionUuid，例如：https://www.liblib.art/modelinfo/xxx?versionUuid=412b427ddb674b4dbab9e5abd5ae6057
        /// </summary>
        public string checkPointId;
        
        /// <summary>
        /// 提示词（选填）
        /// </summary>
        public string prompt;
        
        /// <summary>
        /// 采样步数
        /// </summary>
        public int steps;
        
        /// <summary>
        /// 宽度
        /// </summary>
        public int width;
        
        /// <summary>
        /// 高度
        /// </summary>
        public int height;
        
        /// <summary>
        /// 图片数量
        /// </summary>
        public int imgCount;
        
        /// <summary>
        /// 随机种子值，-1表示随机
        /// </summary>
        public long seed;
        
        /// <summary>
        /// 面部修复，0关闭，1开启
        /// </summary>
        public int restoreFaces;
        
        /// <summary>
        /// LoRA添加，最多5个（不添加lora时请删除此结构体）
        /// </summary>
        public AdditionalNetwork[] additionalNetwork;
    }
    
    /// <summary>
    /// LoRA附加网络
    /// </summary>
    [Serializable]
    public class AdditionalNetwork
    {
        /// <summary>
        /// LoRA的模型版本versionuuid
        /// </summary>
        public string modelId;
        
        /// <summary>
        /// LoRA权重
        /// </summary>
        public float weight;
    }
}

