using System.Text.Json;
using System.Text.Encodings.Web;

namespace HalconVisionStudio.AI
{
    /// <summary>
    /// AI 侧统一 JSON 序列化选项：中文等非 ASCII 字符直接输出而非 \uXXXX 转义
    /// （转义既浪费 token 又不可读，工具结果/协议帧/配置文件统一走此选项）。
    /// </summary>
    public static class AiJson
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static string Serialize<TValue>(TValue value) => JsonSerializer.Serialize(value, Options);
    }
}
