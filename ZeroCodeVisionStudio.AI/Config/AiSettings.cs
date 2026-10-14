using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Prism.Mvvm;

namespace ZeroCodeVisionStudio.AI.Config
{
    public enum LlmProviderType
    {
        OpenAICompatible,
        Ollama,
        DeepSeek,
        ZhipuGLM
    }

    /// <summary>
    /// AI 设置数据模型。
    /// 实现 INotifyPropertyChanged：程序内切换服务商预设时，
    /// 界面上双向绑定的 BaseUrl / 模型名 / Temperature 等输入框会实时刷新。
    /// </summary>
    public class AiSettings : BindableBase
    {
        // ==================== 模式一：内置 Copilot 设置 ====================

        private bool isCopilotEnabled = true;
        private LlmProviderType provider = LlmProviderType.OpenAICompatible;
        private string baseUrl = "https://api.deepseek.com/v1";
        private string encryptedApiKey = "";
        private string apiKey = "";
        private string modelName = "deepseek-chat";
        private double temperature = 0.2;
        private int maxTokens = 4096;
        private int maxToolLoops = 30;
        private bool? supportsVisionOverride;
        private Dictionary<string, string> providerApiKeys = new Dictionary<string, string>();

        // ==================== 模式二：内置 MCP Server 设置 ====================

        private bool isMcpServerEnabled = true;
        private int mcpPort = 8765;
        private bool requireMcpToken = true;

        /// <summary>是否启用内置 Copilot 面板。</summary>
        public bool IsCopilotEnabled
        {
            get => isCopilotEnabled;
            set => SetProperty(ref isCopilotEnabled, value);
        }

        /// <summary>模型提供方（当前所有提供方均走 OpenAI 兼容 /chat/completions 协议）。</summary>
        public LlmProviderType Provider
        {
            get => provider;
            set => SetProperty(ref provider, value);
        }

        public string BaseUrl
        {
            get => baseUrl;
            set => SetProperty(ref baseUrl, value);
        }

        /// <summary>
        /// API Key（内存明文，运行时使用）。
        /// 持久化时通过 <see cref="EncryptedApiKey"/> 以 Windows DPAPI（当前用户范围）加密落盘，
        /// 永不以明文写入配置文件。
        /// </summary>
        [JsonIgnore]
        public string ApiKey
        {
            get => apiKey;
            set => SetProperty(ref apiKey, value);
        }

        /// <summary>DPAPI 加密后的 API Key（Base64），仅在保存/加载时使用。</summary>
        public string EncryptedApiKey
        {
            get => encryptedApiKey;
            set => SetProperty(ref encryptedApiKey, value);
        }

        public string ModelName
        {
            get => modelName;
            set => SetProperty(ref modelName, value);
        }

        public double Temperature
        {
            get => temperature;
            set => SetProperty(ref temperature, value);
        }

        public int MaxTokens
        {
            get => maxTokens;
            set => SetProperty(ref maxTokens, value);
        }

        /// <summary>
        /// 单轮对话允许的最大工具调用轮次（默认 30）。
        /// 搭建多节点流程时工具调用次数随节点数线性增长，上限过低会中途截停；
        /// 到达上限后发送「继续」可基于对话历史接力完成剩余步骤。
        /// </summary>
        public int MaxToolLoops
        {
            get => maxToolLoops;
            set => SetProperty(ref maxToolLoops, Math.Clamp(value, 1, 80));
        }

        /// <summary>
        /// 模型视觉（图片输入）能力手动覆盖：null=按模型名自动检测；true=强制开启；false=强制关闭。
        /// 模型命名无统一规律时（如新模型上线），用户可手动指定，避免截图被误发或误弃。
        /// </summary>
        public bool? SupportsVisionOverride
        {
            get => supportsVisionOverride;
            set => SetProperty(ref supportsVisionOverride, value);
        }

        /// <summary>
        /// 各服务商独立保存的 API Key（键为 Provider 枚举名，值为 DPAPI 加密 Base64）。
        /// 各家 Key 互不通用：切换服务商时自动换载对应密钥，避免拿 A 家 Key 请求 B 家（表现为智谱 1214 等"Key 无效"错误）。
        /// </summary>
        public Dictionary<string, string> ProviderApiKeys
        {
            get => providerApiKeys;
            set => SetProperty(ref providerApiKeys, value ?? new Dictionary<string, string>());
        }

        public bool IsMcpServerEnabled
        {
            get => isMcpServerEnabled;
            set => SetProperty(ref isMcpServerEnabled, value);
        }

        public int McpPort
        {
            get => mcpPort;
            set => SetProperty(ref mcpPort, value);
        }

        /// <summary>
        /// MCP Server 是否强制 Token 鉴权（默认开启）。
        /// 开启后所有请求必须携带 Authorization: Bearer &lt;token&gt;（或 ?token= 查询参数），
        /// 防止本机其它进程或浏览器页面未经授权调用自动化工具。
        /// </summary>
        public bool RequireMcpToken
        {
            get => requireMcpToken;
            set => SetProperty(ref requireMcpToken, value);
        }

        public static AiSettings CreateDefault()
        {
            return new AiSettings();
        }
    }

    /// <summary>
    /// AI 设置持久化管理器。
    /// API Key 使用 Windows DPAPI（DataProtectionScope.CurrentUser）加密存储，
    /// 绑定当前用户与机器，拷贝配置文件到其它机器无法解密。
    /// </summary>
    public static class AiSettingsManager
    {
        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ZeroCodeVisionStudio");

        private static readonly string SettingsFile = Path.Combine(SettingsDir, "ai_settings.json");

        private static AiSettings _current;

        public static AiSettings Current
        {
            get
            {
                if (_current == null)
                {
                    _current = Load();
                }
                return _current;
            }
        }

        public static AiSettings Load()
        {
            AiSettings settings = null;
            string legacyPlainKey = null;

            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    settings = JsonSerializer.Deserialize<AiSettings>(json);

                    // 兼容旧版明文 apiKey 字段：读取并迁移到加密存储。
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("apiKey", out var legacy) &&
                        legacy.ValueKind == JsonValueKind.String &&
                        (settings == null || string.IsNullOrEmpty(settings.EncryptedApiKey)))
                    {
                        legacyPlainKey = legacy.GetString();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AI Settings] 读取设置失败: {ex.Message}");
            }

            settings ??= AiSettings.CreateDefault();

            try
            {
                settings.ApiKey = Unprotect(settings.EncryptedApiKey);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AI Settings] 解密 API Key 失败: {ex.Message}");
                settings.ApiKey = "";
            }

            if (!string.IsNullOrEmpty(legacyPlainKey))
            {
                settings.ApiKey = legacyPlainKey;
                Save(settings); // 立即迁移为加密存储
            }

            return settings;
        }

        public static void Save(AiSettings settings)
        {
            try
            {
                if (!Directory.Exists(SettingsDir))
                {
                    Directory.CreateDirectory(SettingsDir);
                }

                _current = settings ?? AiSettings.CreateDefault();
                _current.EncryptedApiKey = Protect(_current.ApiKey);
                // 当前服务商的 Key 同步归档到独立密钥表（每服务商一份）。
                if (!string.IsNullOrWhiteSpace(_current.ApiKey))
                {
                    _current.ProviderApiKeys[_current.Provider.ToString()] = Protect(_current.ApiKey);
                }
                var json = AiJson.Serialize(_current);
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AI Settings] 保存设置失败: {ex.Message}");
            }
        }

        /// <summary>加密单个服务商密钥（供切换服务商时归档，公开包装）。</summary>
        public static string ProtectKey(string plain) => Protect(plain);

        /// <summary>解密单个服务商密钥（公开包装）。</summary>
        public static string UnprotectKey(string cipher) => Unprotect(cipher);

        private static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            var encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        private static string Unprotect(string cipher)
        {
            if (string.IsNullOrEmpty(cipher)) return "";
            var decrypted = ProtectedData.Unprotect(
                Convert.FromBase64String(cipher), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
    }

    /// <summary>
    /// MCP Server 访问令牌管理器。
    /// 首次调用时生成 256 位随机令牌并写入 %APPDATA%\ZeroCodeVisionStudio\mcp_token.txt，
    /// 本机的 stdio 桥接脚本（halcon-mcp-bridge.py）可直接读取该文件完成鉴权。
    /// </summary>
    public static class McpAuthTokenStore
    {
        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ZeroCodeVisionStudio");

        private static readonly string TokenFile = Path.Combine(SettingsDir, "mcp_token.txt");

        private static string _cached;

        public static string TokenFilePath => TokenFile;

        /// <summary>获取令牌；文件不存在时生成新令牌并落盘。</summary>
        public static string LoadOrCreate()
        {
            if (_cached != null) return _cached;

            try
            {
                if (File.Exists(TokenFile))
                {
                    var existing = File.ReadAllText(TokenFile).Trim();
                    if (existing.Length >= 32)
                    {
                        _cached = existing;
                        return _cached;
                    }
                }

                Directory.CreateDirectory(SettingsDir);
                var bytes = RandomNumberGenerator.GetBytes(32);
                _cached = Convert.ToHexString(bytes).ToLowerInvariant();
                File.WriteAllText(TokenFile, _cached);
                return _cached;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MCP Token] 令牌管理失败: {ex.Message}");
                _cached = "";
                return _cached;
            }
        }
    }
}
