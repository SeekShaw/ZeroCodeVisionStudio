using System;

namespace HalconVisionStudio.AI.Config
{
    /// <summary>
    /// 按模型名粗略识别是否支持视觉（图片）输入。
    /// 纯文本模型（如 glm-4-flash / deepseek-chat）不接受 image_url 类型的 content，
    /// 智谱会报 1210（messages.content.type 取值范围 [text]）、OpenAI 报类似错误。
    /// 识别为文本模型时：截图不作为多模态附件发送，历史中的图片消息降级为文本说明。
    /// </summary>
    public static class VisionModelDetector
    {
        // 常见视觉模型的命名特征（小写子串匹配）。
        private static readonly string[] VisionPatterns =
        {
            "4v",            // glm-4v / glm-4v-flash / glm-4.5v 系列
            "5v",            // glm-5v / glm-5v-turbo
            "5.3-flash",     // glm-5.3-flash：GLM-5 系列首个原生多模态（注意 glm-5.3 纯文本版不含 flash）
            ".5v",
            "-vl", "vl-",    // qwen-vl / qwen2.5-vl / minicpm-vl
            "vision",        // yi-vision / step-1v-vision 等
            "gpt-4o",        // gpt-4o / gpt-4o-mini
            "gpt-4.1",
            "gemini",        // gemini 全系
            "claude",        // claude 3/4 全系
            "llava",
            "minicpm",
            "step-1v",
            "omni"
        };

        /// <summary>模型名是否为视觉模型（支持图片输入）。</summary>
        public static bool IsVisionModel(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName)) return false;
            var name = modelName.ToLowerInvariant();
            return Array.Exists(VisionPatterns, p => name.Contains(p, StringComparison.Ordinal));
        }
    }
}
