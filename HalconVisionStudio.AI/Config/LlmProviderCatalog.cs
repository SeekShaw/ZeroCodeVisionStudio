using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace HalconVisionStudio.AI.Config
{
    /// <summary>
    /// 大模型服务商预设项：一键选择后自动填充 BaseUrl / 推荐模型 / 密钥获取入口。
    /// 所有服务商均兼容 OpenAI /chat/completions 协议。
    /// </summary>
    public class LlmProviderPreset : INotifyPropertyChanged
    {
        /// <summary>预设唯一键（deepseek / openai / zhipu / ollama / custom）。</summary>
        public string Key { get; set; }

        /// <summary>显示名。</summary>
        public string DisplayName { get; set; }

        /// <summary>图标（emoji）。</summary>
        public string Emoji { get; set; }

        /// <summary>一句话说明（国内可直连 / 本地免密钥 等）。</summary>
        public string Description { get; set; }

        /// <summary>对应的 Provider 枚举值。</summary>
        public LlmProviderType Provider { get; set; }

        /// <summary>API Base Url。</summary>
        public string BaseUrl { get; set; }

        /// <summary>默认推荐模型。</summary>
        public string DefaultModel { get; set; }

        /// <summary>常用模型列表（下拉可选，也允许手动输入其它模型名）。</summary>
        public List<string> Models { get; set; } = new();

        /// <summary>密钥获取页面（浏览器打开），本地模型为 null。</summary>
        public string ApiKeyUrl { get; set; }

        /// <summary>是否需要 API Key（Ollama 本地部署不需要）。</summary>
        public bool RequiresKey => !string.IsNullOrEmpty(ApiKeyUrl);

        /// <summary>是否支持视觉多模态（截图分析）。</summary>
        public bool SupportsVision { get; set; }

        /// <summary>是否为当前选中的预设（卡片高亮）。</summary>
        public bool IsSelected
        {
            get => field;
            set
            {
                if (value != field)
                {
                    field = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// 内置服务商目录与匹配工具。
    /// </summary>
    public static class LlmProviderCatalog
    {
        public static readonly List<LlmProviderPreset> Presets = new()
        {
            new LlmProviderPreset
            {
                Key = "deepseek",
                DisplayName = "DeepSeek 深度求索",
                Emoji = "🐳",
                Description = "国内直连 · 性价比高 · 推荐",
                Provider = LlmProviderType.DeepSeek,
                BaseUrl = "https://api.deepseek.com/v1",
                DefaultModel = "deepseek-chat",
                Models = new List<string> { "deepseek-chat", "deepseek-reasoner" },
                ApiKeyUrl = "https://platform.deepseek.com/api_keys"
            },
            new LlmProviderPreset
            {
                Key = "zhipu",
                DisplayName = "智谱 GLM",
                Emoji = "🔮",
                Description = "国内直连 · glm-4v 支持看图分析",
                Provider = LlmProviderType.ZhipuGLM,
                BaseUrl = "https://open.bigmodel.cn/api/paas/v4",
                DefaultModel = "glm-5.3-flash",
                Models = new List<string> { "glm-5.3", "glm-5.3-flash", "glm-4v-flash", "glm-4v", "glm-4-flash" },
                ApiKeyUrl = "https://open.bigmodel.cn/usercenter/apikeys",
                SupportsVision = true
            },
            new LlmProviderPreset
            {
                Key = "openai",
                DisplayName = "OpenAI",
                Emoji = "🌍",
                Description = "gpt-4o 系列支持看图分析",
                Provider = LlmProviderType.OpenAICompatible,
                BaseUrl = "https://api.openai.com/v1",
                DefaultModel = "gpt-4o-mini",
                Models = new List<string> { "gpt-4o", "gpt-4o-mini", "gpt-4.1", "gpt-4.1-mini" },
                ApiKeyUrl = "https://platform.openai.com/api-keys",
                SupportsVision = true
            },
            new LlmProviderPreset
            {
                Key = "ollama",
                DisplayName = "Ollama 本地模型",
                Emoji = "🦙",
                Description = "本地部署 · 无需 API Key · 数据不出厂",
                Provider = LlmProviderType.Ollama,
                BaseUrl = "http://localhost:11434/v1",
                DefaultModel = "qwen2.5:7b",
                Models = new List<string> { "qwen2.5:7b", "llama3.2", "glm4:9b", "deepseek-r1:8b" },
                ApiKeyUrl = null
            },
            new LlmProviderPreset
            {
                Key = "siliconflow",
                DisplayName = "硅基流动 SiliconFlow",
                Emoji = "⚡",
                Description = "国内直连 · 聚合多模型 · 免费额度",
                Provider = LlmProviderType.OpenAICompatible,
                BaseUrl = "https://api.siliconflow.cn/v1",
                DefaultModel = "deepseek-ai/DeepSeek-V3",
                Models = new List<string> { "deepseek-ai/DeepSeek-V3", "deepseek-ai/DeepSeek-R1", "Qwen/Qwen2.5-72B-Instruct", "THUDM/glm-4-9b-chat" },
                ApiKeyUrl = "https://cloud.siliconflow.cn/account/ak",
                SupportsVision = true
            },
            new LlmProviderPreset
            {
                Key = "custom",
                DisplayName = "自定义 / 其它兼容服务",
                Emoji = "⚙️",
                Description = "任意 OpenAI 兼容端点（OneAPI / vLLM / Azure 代理等）",
                Provider = LlmProviderType.OpenAICompatible,
                BaseUrl = "",
                DefaultModel = "",
                Models = new List<string>(),
                ApiKeyUrl = null
            }
        };

        /// <summary>按当前 BaseUrl 识别已选服务商：完全匹配 → 前缀匹配 → 回退自定义。</summary>
        public static LlmProviderPreset DetectByBaseUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return Presets.First(p => p.Key == "custom");
            }

            var url = baseUrl.TrimEnd('/').Trim();
            var exact = Presets.FirstOrDefault(p =>
                !string.IsNullOrEmpty(p.BaseUrl) &&
                string.Equals(p.BaseUrl.TrimEnd('/'), url, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            var prefix = Presets.FirstOrDefault(p =>
                !string.IsNullOrEmpty(p.BaseUrl) &&
                url.StartsWith(p.BaseUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
            if (prefix != null) return prefix;

            return Presets.First(p => p.Key == "custom");
        }
    }
}
