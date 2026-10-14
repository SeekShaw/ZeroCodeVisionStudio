using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using ZeroCodeVisionStudio.AI.Config;
using ZeroCodeVisionStudio.AI.Copilot;
using ZeroCodeVisionStudio.AI.Mcp;
using ZeroCodeVisionStudio.AI.Tools;
using ZeroCodeVisionStudio.Core;
using ZeroCodeVisionStudio.Shared;

namespace ZeroCodeVisionStudio.ViewModels
{
    /// <summary>模型列表获取状态（驱动界面提示颜色）。</summary>
    public enum ModelsFetchState
    {
        None,
        Info,
        Busy,
        Success,
        Fail
    }

    public class VisionCopilotViewModel : BindableBase
    {
        private readonly IVisionStudioAutomationService _automationService;
        private readonly VisionCopilotEngine _engine;
        private readonly McpServer _mcpServer;
        private string _inputText = "";
        private bool _isSettingsOpen;
        private bool _isBusy;
        private string _mcpStatusText = "MCP: 未启动";
        private bool _isMcpRunning;
        private int _mcpPort = 8765;
        private AiSettings _settings;

        public VisionCopilotEngine Engine => _engine;
        public ObservableCollection<CopilotMessage> Messages => _engine.Messages;

        public string InputText
        {
            get => _inputText;
            set => SetProperty(ref _inputText, value);
        }

        /// <summary>待随下一条消息发送的图片路径（用户粘贴/拖放/📎 附加）。</summary>
        public ObservableCollection<string> PendingInputImages { get; } = new();

        /// <summary>移除一张待发图片。</summary>
        public ICommand RemoveImageCommand { get; private set; }

        /// <summary>添加待发图片（去重、仅常见图片格式）。</summary>
        public void AddPendingImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return;
            var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            if (ext is not (".png" or ".jpg" or ".jpeg" or ".bmp")) return;
            if (!PendingInputImages.Contains(path)) PendingInputImages.Add(path);
        }

        public bool IsSettingsOpen
        {
            get => _isSettingsOpen;
            set => SetProperty(ref _isSettingsOpen, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public bool IsMcpRunning
        {
            get => _isMcpRunning;
            set => SetProperty(ref _isMcpRunning, value);
        }

        public string McpStatusText
        {
            get => _mcpStatusText;
            set => SetProperty(ref _mcpStatusText, value);
        }

        public string ToggleMcpText => IsMcpRunning ? "停止服务" : "启动服务";

        public int McpPort
        {
            get => _mcpPort;
            set => SetProperty(ref _mcpPort, value);
        }

        public AiSettings Settings
        {
            get => _settings;
            set => SetProperty(ref _settings, value);
        }

        public Action RequestClose { get; set; }
        public DelegateCommand CloseCommand { get; }
        public DelegateCommand SendCommand { get; }
        public DelegateCommand QuickDiagnoseCommand { get; }
        public DelegateCommand QuickExecuteCommand { get; }
        public DelegateCommand QuickCreateSchemeCommand { get; }
        public DelegateCommand QuickCaptureImageCommand { get; }
        public DelegateCommand ClearHistoryCommand { get; }
        public DelegateCommand ToggleSettingsCommand { get; }
        public DelegateCommand SaveSettingsCommand { get; }
        public DelegateCommand ToggleMcpCommand { get; }
        public DelegateCommand CopyMcpCliCommand { get; }
        public DelegateCommand CopyIdeMcpCommand { get; }
        public DelegateCommand CopyMcpTokenCommand { get; }

        /// <summary>当前 MCP 访问令牌（McpAuthTokenStore 自动生成并落盘）。</summary>
        public string McpToken => McpAuthTokenStore.LoadOrCreate();

        // ==================== 服务商预设 / 连接测试 ====================

        /// <summary>可选服务商预设卡片。</summary>
        public ObservableCollection<LlmProviderPreset> ProviderPresets { get; } =
            new ObservableCollection<LlmProviderPreset>(LlmProviderCatalog.Presets);

        /// <summary>当前选中的服务商预设。</summary>
        public LlmProviderPreset SelectedPreset { get => field; private set => SetProperty(ref field, value); }

        /// <summary>
        /// 模型下拉的可选列表：默认为服务商预设推荐，从 /models 接口拉取成功后替换为真实可用列表。
        /// </summary>
        public ObservableCollection<string> AvailableModels { get; } = new ObservableCollection<string>();

        /// <summary>是否正在拉取模型列表。</summary>
        public bool IsFetchingModels
        {
            get => field;
            private set
            {
                if (SetProperty(ref field, value))
                {
                    RaisePropertyChanged(nameof(FetchModelsButtonText));
                    FetchModelsCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>模型列表刷新按钮文本。</summary>
        public string FetchModelsButtonText => IsFetchingModels ? "获取中…" : "获取模型";

        /// <summary>模型列表获取状态提示（成功条数/失败原因，空=无提示）。</summary>
        public string ModelsFetchHint { get => field; private set => SetProperty(ref field, value); }

        /// <summary>模型列表获取状态（驱动提示文字颜色：忙碌=主色 成功=绿 失败=红 提示=灰）。</summary>
        public ModelsFetchState ModelsHintState { get => field; private set => SetProperty(ref field, value); }

        /// <summary>缺密钥导致无法获取模型列表时，请求界面聚焦密钥输入框。</summary>
        public event Action RequestFocusApiKey;

        /// <summary>程序化换载 API Key 后请求视图刷新 PasswordBox 显示。</summary>
        public event Action RequestRefreshApiKey;

        private void SetModelsHint(ModelsFetchState state, string text)
        {
            ModelsHintState = state;
            ModelsFetchHint = text;
        }

        /// <summary>
        /// 刷新成功后自动展开模型下拉（TwoWay：用户手动收起会写回 false）。
        /// </summary>
        public bool AutoOpenModelDropdown
        {
            get => field;
            set => SetProperty(ref field, value);
        }

        /// <summary>密钥变更自动拉取的一次性标记（避免每个字符触发一次请求）。</summary>
        private bool hasAutoFetchedOnKey;

        /// <summary>当前服务商是否需要 API Key（Ollama 本地部署不需要，隐藏密钥行）。</summary>
        public bool KeyRequired => SelectedPreset is not { RequiresKey: false };

        /// <summary>当前服务商的密钥获取入口（无则隐藏链接）。</summary>
        public string CurrentApiKeyUrl => SelectedPreset?.ApiKeyUrl;

        /// <summary>是否正在测试连接。</summary>
        public bool IsTesting
        {
            get => field;
            private set
            {
                if (SetProperty(ref field, value))
                {
                    RaisePropertyChanged(nameof(TestButtonText));
                    TestConnectionCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>测试按钮文本。</summary>
        public string TestButtonText => IsTesting ? "正在测试…" : "测试连接";

        /// <summary>连接测试结果文本（null=未测试）。</summary>
        public string TestResultText { get => field; private set => SetProperty(ref field, value); }

        /// <summary>连接测试是否通过（null=未测试，用于染色）。</summary>
        public bool? TestPassed { get => field; private set => SetProperty(ref field, value); }

        public DelegateCommand<string> SelectProviderCommand { get; }
        public DelegateCommand TestConnectionCommand { get; }
        public DelegateCommand OpenApiKeyPageCommand { get; }
        public DelegateCommand FetchModelsCommand { get; }

        public VisionCopilotViewModel(IVisionStudioAutomationService automationService)
        {
            _automationService = automationService ?? throw new ArgumentNullException(nameof(automationService));
            _engine = new VisionCopilotEngine(_automationService);
            _engine.BusyStateChanged += b => IsBusy = b;

            // 使用全局单例设置：引擎发送请求时读取 AiSettingsManager.Current，
            // 与面板编辑的是同一实例，改动即时生效。
            _settings = AiSettingsManager.Current;
            _mcpPort = _settings.McpPort;

            // 设置改动即自动持久化（防抖 800ms），无需手动点保存。
            _settings.PropertyChanged += OnSettingsPropertyChanged;
            autoSaveTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(800)
            };
            autoSaveTimer.Tick += (_, __) =>
            {
                autoSaveTimer.Stop();
                SaveNow(showHint: true);
            };

            _mcpServer = new McpServer(_automationService);
            _mcpServer.ServerStatusChanged += OnMcpStatusChanged;
            _mcpServer.LogMessage += msg => System.Diagnostics.Debug.WriteLine(msg);

            CloseCommand = new DelegateCommand(() => RequestClose?.Invoke());
            SendCommand = new DelegateCommand(async () => await SendAsync());
            RemoveImageCommand = new DelegateCommand<string>(path => PendingInputImages.Remove(path));
            QuickDiagnoseCommand = new DelegateCommand(async () => await QuickDiagnoseAsync());
            QuickExecuteCommand = new DelegateCommand(async () => await SendCustomAsync("请执行当前流程，并在执行完成后检查各节点的耗时与输出状态。"));
            QuickCreateSchemeCommand = new DelegateCommand(async () => await SendCustomAsync("请为我创建一个标准的工业工件缺陷检测工作流：添加图像采集、灰度预处理、阈值分割、形态学去噪和结果输出节点，并按顺序连接起来。"));
            QuickCaptureImageCommand = new DelegateCommand(async () => await SendCustomAsync("请截取当前视口图像并分析当前画面中的工件特征。"));
            ClearHistoryCommand = new DelegateCommand(() => _engine.ClearHistory());
            ToggleSettingsCommand = new DelegateCommand(() => IsSettingsOpen = !IsSettingsOpen);
            SaveSettingsCommand = new DelegateCommand(SaveSettings);
            ToggleMcpCommand = new DelegateCommand(async () => await ToggleMcpAsync());
            CopyMcpCliCommand = new DelegateCommand(CopyMcpCliInstructions);
            CopyIdeMcpCommand = new DelegateCommand(CopyIdeMcpConfig);
            CopyMcpTokenCommand = new DelegateCommand(CopyMcpToken);
            SelectProviderCommand = new DelegateCommand<string>(SelectProvider);
            TestConnectionCommand = new DelegateCommand(async () => await TestConnectionAsync(), () => !IsTesting);
            OpenApiKeyPageCommand = new DelegateCommand(OpenApiKeyPage);
            FetchModelsCommand = new DelegateCommand(async () => await FetchModelsAsync());

            // 依据已保存的 BaseUrl 识别服务商并高亮对应预设卡片。
            ApplyDetectedPreset();

            // 自动启动内置 MCP 服务
            if (_settings.IsMcpServerEnabled)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _mcpServer.StartAsync(_settings.McpPort);
                    }
                    catch
                    {
                    }
                });
            }
        }

        private async Task SendAsync()
        {
            var text = InputText?.Trim();
            var images = PendingInputImages.ToList();
            if ((string.IsNullOrWhiteSpace(text) && images.Count == 0) || IsBusy) return;

            InputText = "";
            PendingInputImages.Clear();

            if (KeyRequired && string.IsNullOrWhiteSpace(Settings.ApiKey))
            {
                _engine.Messages.Add(new CopilotMessage { Role = MessageRole.User, Content = text });
                _engine.Messages.Add(new CopilotMessage
                {
                    Role = MessageRole.Assistant,
                    Content = "检测到尚未配置大模型 API Key。请点击面板右上角的设置图标进入设置，选择服务商并填入 API Key 后即可正常对话。"
                });
                return;
            }

            await _engine.SendMessageAsync(text, images);
        }

        // ==================== 服务商预设逻辑 ====================

        /// <summary>按已保存 BaseUrl 识别服务商，高亮对应预设（不覆盖任何已填值）。</summary>
        private void ApplyDetectedPreset()
        {
            var preset = LlmProviderCatalog.DetectByBaseUrl(Settings?.BaseUrl);
            SetPresetHighlight(preset);
        }

        /// <summary>
        /// 选择服务商预设：自动填充 BaseUrl / 推荐模型 / Provider 枚举，并按服务商换载各自的 API Key
        /// （各家 Key 互不通用，避免拿上一家的 Key 请求新服务商）。
        /// </summary>
        private void SelectProvider(string presetKey)
        {
            var preset = ProviderPresets.FirstOrDefault(p => p.Key == presetKey);
            if (preset == null || Settings == null) return;

            // 切换前：把当前 Key 归档到原服务商名下。
            var previousProviderKey = Settings.Provider.ToString();
            if (!string.IsNullOrWhiteSpace(Settings.ApiKey))
            {
                try { Settings.ProviderApiKeys[previousProviderKey] = AiSettingsManager.ProtectKey(Settings.ApiKey); }
                catch { }
            }

            Settings.Provider = preset.Provider;

            if (preset.Key != "custom")
            {
                // 非自定义：套用预设地址；若当前模型不属于该服务商候选列表则换成默认推荐模型。
                Settings.BaseUrl = preset.BaseUrl;
                if (!string.IsNullOrEmpty(preset.DefaultModel) &&
                    (string.IsNullOrWhiteSpace(Settings.ModelName) ||
                     preset.Models.Count > 0 && !preset.Models.Contains(Settings.ModelName, StringComparer.OrdinalIgnoreCase)))
                {
                    Settings.ModelName = preset.DefaultModel;
                }
            }

            SetPresetHighlight(preset);
            TestPassed = null;
            TestResultText = null;

            // 切换后：载入新服务商自己的 Key（没存过则为空，模型拉取会引导填写）。
            Settings.ApiKey = GetProviderKey(preset.Provider.ToString());
            RequestRefreshApiKey?.Invoke();

            // 选中服务商后自动拉取真实模型列表（无 Key 的服务商会直接请求，本地 Ollama 列出已装模型）。
            _ = FetchModelsAsync();
        }

        private string GetProviderKey(string providerKey)
        {
            if (Settings.ProviderApiKeys.TryGetValue(providerKey, out var cipher))
            {
                try { return AiSettingsManager.UnprotectKey(cipher); }
                catch { return ""; }
            }
            return "";
        }

        private void SetPresetHighlight(LlmProviderPreset preset)
        {
            foreach (var p in ProviderPresets)
            {
                p.IsSelected = ReferenceEquals(p, preset);
            }

            SelectedPreset = preset;
            FillModels(preset?.Models ?? new List<string>(), null, ModelsFetchState.None);
            RaisePropertyChanged(nameof(KeyRequired));
            RaisePropertyChanged(nameof(CurrentApiKeyUrl));
        }

        /// <summary>填充模型下拉列表（保持当前已选模型名在列表中优先展示）。</summary>
        private void FillModels(IEnumerable<string> models, string hint, ModelsFetchState state)
        {
            AvailableModels.Clear();
            var current = Settings?.ModelName;
            if (!string.IsNullOrWhiteSpace(current) && models != null &&
                !models.Contains(current, StringComparer.OrdinalIgnoreCase))
            {
                AvailableModels.Add(current);
            }

            foreach (var m in models ?? Enumerable.Empty<string>())
            {
                AvailableModels.Add(m);
            }

            SetModelsHint(state, hint);
        }

        /// <summary>
        /// 从 {BaseUrl}/models 拉取真实可用模型列表（OpenAI 兼容标准接口）。
        /// Ollama 返回本地已安装模型；需密钥的服务商在无 Key 时跳过并提示。
        /// </summary>
        private async Task FetchModelsAsync(bool openDropdownOnSuccess = true)
        {
            if (IsFetchingModels) return;

            if (Settings == null || string.IsNullOrWhiteSpace(Settings.BaseUrl))
            {
                SetModelsHint(ModelsFetchState.Fail, "✗ 请先填写 API 地址，再获取模型列表");
                return;
            }

            if (KeyRequired && string.IsNullOrWhiteSpace(Settings.ApiKey))
            {
                SetModelsHint(ModelsFetchState.Fail, "✗ 未填写 API Key，无法获取（请在下方 ③ 输入密钥）");
                RequestFocusApiKey?.Invoke();
                return;
            }

            IsFetchingModels = true;
            SetModelsHint(ModelsFetchState.Busy, "正在获取模型列表…");
            try
            {
                var url = Settings.BaseUrl.TrimEnd('/');
                if (!url.EndsWith("/models", StringComparison.OrdinalIgnoreCase))
                {
                    url += "/models";
                }

                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrWhiteSpace(Settings.ApiKey))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Settings.ApiKey.Trim());
                }

                var response = await http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var reason = (int)response.StatusCode switch
                    {
                        401 => "密钥无效或与该服务商不匹配",
                        403 => "密钥无权访问模型列表",
                        404 => "该地址不支持 /models 接口",
                        _ => ""
                    };
                    FillModels(SelectedPreset?.Models ?? new List<string>(),
                        string.IsNullOrWhiteSpace(reason)
                            ? $"✗ 获取失败（HTTP {(int)response.StatusCode}），已显示推荐列表"
                            : $"✗ 获取失败：{reason}，已显示推荐列表",
                        ModelsFetchState.Fail);
                    return;
                }

                var body = await response.Content.ReadAsStringAsync();
                var models = new List<string>();
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in data.EnumerateArray())
                        {
                            if (item.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                            {
                                var id = idProp.GetString();
                                if (string.IsNullOrWhiteSpace(id)) continue;

                                // 过滤非对话类模型（向量化/语音/图像生成等）。
                                var lower = id.ToLowerInvariant();
                                if (lower.Contains("embedding") || lower.Contains("whisper") ||
                                    lower.Contains("tts") || lower.Contains("dall-e") ||
                                    lower.Contains("audio") || lower.Contains("realtime") ||
                                    lower.Contains("moderation"))
                                {
                                    continue;
                                }

                                models.Add(id);
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                    FillModels(SelectedPreset?.Models ?? new List<string>(), "✗ 响应格式异常，已显示推荐列表", ModelsFetchState.Fail);
                    return;
                }

                if (models.Count == 0)
                {
                    FillModels(SelectedPreset?.Models ?? new List<string>(), "✗ 接口未返回可用模型，已显示推荐列表", ModelsFetchState.Fail);
                    return;
                }

                models.Sort(StringComparer.OrdinalIgnoreCase);

                // 当前模型不在真实列表中时，自动切换到第一个可用模型。
                // 例外：当前模型属于该服务商预设列表（如智谱的视觉模型 glm-4v 不在 /models 返回中，
                // 但可正常调用），保持用户选择不变。
                var current = Settings.ModelName;
                var presetModels = SelectedPreset?.Models ?? new List<string>();
                if (!string.IsNullOrWhiteSpace(current) &&
                    !models.Contains(current, StringComparer.OrdinalIgnoreCase) &&
                    !presetModels.Contains(current, StringComparer.OrdinalIgnoreCase))
                {
                    Settings.ModelName = models[0];
                    FillModels(models, $"✓ 已获取 {models.Count} 个可用模型，模型已切换为 {models[0]}", ModelsFetchState.Success);
                }
                else
                {
                    FillModels(models, $"✓ 已获取 {models.Count} 个可用模型", ModelsFetchState.Success);
                }

                if (openDropdownOnSuccess)
                {
                    AutoOpenModelDropdown = true;
                }
            }
            catch (Exception ex)
            {
                FillModels(SelectedPreset?.Models ?? new List<string>(),
                    $"✗ 获取失败：{ex.Message}，已显示推荐列表", ModelsFetchState.Fail);
            }
            finally
            {
                IsFetchingModels = false;
            }
        }

        /// <summary>
        /// 供 PasswordBox 同步调用：密钥填入后自动拉取一次真实模型列表（每次会话仅一次，避免逐字符触发请求）。
        /// </summary>
        internal void NotifyApiKeyChanged()
        {
            if (hasAutoFetchedOnKey || !KeyRequired || string.IsNullOrWhiteSpace(Settings?.ApiKey))
            {
                return;
            }

            hasAutoFetchedOnKey = true;
            _ = FetchModelsAsync();
        }

        /// <summary>浏览器打开当前服务商的密钥获取页面。</summary>
        private void OpenApiKeyPage()
        {
            var url = CurrentApiKeyUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("本地模型无需 API Key；自定义服务请前往对应服务商控制台获取密钥。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开浏览器失败：{ex.Message}\n\n地址：{url}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 测试当前配置连通性：向 {BaseUrl}/chat/completions 发送 max_tokens=1 的最小请求，
        /// 校验 地址 + 密钥 + 模型名 三者组合是否可用，并显示往返延迟。
        /// </summary>
        private async Task TestConnectionAsync()
        {
            if (IsTesting) return;
            if (string.IsNullOrWhiteSpace(Settings?.BaseUrl))
            {
                TestPassed = false;
                TestResultText = "请先填写 API Base URL";
                return;
            }

            IsTesting = true;
            TestPassed = null;
            TestResultText = "正在连接模型服务…";
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var url = Settings.BaseUrl.TrimEnd('/');
                if (!url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
                {
                    url += "/chat/completions";
                }

                var payload = JsonSerializer.Serialize(new
                {
                    model = Settings.ModelName,
                    messages = new[] { new { role = "user", content = "ping" } },
                    max_tokens = 1,
                    stream = false
                });

                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                if (!string.IsNullOrWhiteSpace(Settings.ApiKey))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Settings.ApiKey.Trim());
                }

                var response = await http.SendAsync(request);
                sw.Stop();

                if (response.IsSuccessStatusCode)
                {
                    TestPassed = true;
                    TestResultText = $"✓ 连接成功：{Settings.ModelName} 响应正常（{sw.ElapsedMilliseconds} ms）";
                    // 密钥验证可用，顺手刷新真实模型列表。
                    _ = FetchModelsAsync();
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync();
                    if (body.Length > 220) body = body.Substring(0, 220) + "…";
                    TestPassed = false;
                    var hint = (int)response.StatusCode == 401
                        ? "密钥无效或已过期，请检查 API Key"
                        : (int)response.StatusCode == 404
                            ? "地址或模型名不存在，请检查 Base URL 与模型名称"
                            : body;
                    TestResultText = $"✗ 连接失败 (HTTP {(int)response.StatusCode})：{hint}";
                }
            }
            catch (TaskCanceledException)
            {
                TestPassed = false;
                TestResultText = "✗ 连接超时（20 秒无响应）：请检查地址是否正确、本地模型是否已启动";
            }
            catch (Exception ex)
            {
                TestPassed = false;
                TestResultText = $"✗ 连接失败：{ex.Message}";
            }
            finally
            {
                IsTesting = false;
                TestConnectionCommand.RaiseCanExecuteChanged();
            }
        }

        public async Task SendCustomAsync(string prompt)
        {
            if (IsBusy || string.IsNullOrWhiteSpace(prompt)) return;
            await _engine.SendMessageAsync(prompt);
        }

        public async Task QuickDiagnoseAsync(string nodeName = null)
        {
            if (IsBusy) return;
            await _engine.DiagnoseFaultAsync(nodeName);
        }

        // ==================== 设置持久化 ====================

        /// <summary>自动保存防抖计时器。</summary>
        private readonly System.Windows.Threading.DispatcherTimer autoSaveTimer;

        /// <summary>自动保存提示文本（界面显示"✓ 已自动保存"后自动淡出）。</summary>
        public string AutoSaveHint { get => field; private set => SetProperty(ref field, value); }

        private void OnSettingsPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // EncryptedApiKey 是 Save 过程内部写入的持久化产物，跳过以防保存死循环。
            if (e.PropertyName == nameof(AiSettings.EncryptedApiKey)) return;

            autoSaveTimer.Stop();
            autoSaveTimer.Start();
        }

        /// <summary>保存设置并应用 MCP 服务相关副作用；showHint 时在界面显示保存反馈。</summary>
        private void SaveNow(bool showHint)
        {
            AiSettingsManager.Save(Settings);

            // 端口/开关变化时同步重启 MCP 服务。
            if (_mcpServer.IsRunning && _mcpServer.Port != Settings.McpPort)
            {
                _mcpServer.Stop();
                _ = _mcpServer.StartAsync(Settings.McpPort);
            }
            else if (!_mcpServer.IsRunning && Settings.IsMcpServerEnabled)
            {
                _ = _mcpServer.StartAsync(Settings.McpPort);
            }
            else if (_mcpServer.IsRunning && !Settings.IsMcpServerEnabled)
            {
                _mcpServer.Stop();
            }

            if (showHint)
            {
                AutoSaveHint = $"✓ 已自动保存 {DateTime.Now:HH:mm:ss}";
                _ = DelayClearAutoSaveHintAsync();
            }
        }

        private async Task DelayClearAutoSaveHintAsync()
        {
            await Task.Delay(2500);
            AutoSaveHint = "";
        }

        private void SaveSettings()
        {
            // 兼容保留：立即保存并关闭设置页（常规路径已由自动保存覆盖）。
            autoSaveTimer.Stop();
            SaveNow(showHint: false);
            IsSettingsOpen = false;
        }

        private async Task ToggleMcpAsync()
        {
            if (_mcpServer.IsRunning)
            {
                _mcpServer.Stop();
            }
            else
            {
                try
                {
                    await _mcpServer.StartAsync(Settings.McpPort);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"启动 MCP 失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnMcpStatusChanged(bool running)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                IsMcpRunning = running;
                McpStatusText = running ? $"MCP: 端口 {_mcpServer.Port} (运行中)" : "MCP: 已停止";
                RaisePropertyChanged(nameof(ToggleMcpText));
            });
        }

        private void CopyMcpCliInstructions()
        {
            var bridgePath = ResolveBridgeScriptPath();
            var cmd = $"claude mcp add halcon-studio -- python \"{bridgePath}\"";
            try
            {
                Clipboard.SetText(cmd);
                MessageBox.Show(
                    $"已复制 Claude Code CLI 挂载命令到剪贴板：\n\n{cmd}\n\n" +
                    "桥接脚本会自动读取 %APPDATA%\\ZeroCodeVisionStudio\\mcp_token.txt 完成鉴权；\n" +
                    "如端口已修改，请同时设置环境变量 HALCON_MCP_PORT。",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch
            {
            }
        }

        /// <summary>定位 halcon-mcp-bridge.py：优先程序输出目录（随构建拷贝），回退到仓库根目录。</summary>
        private static string ResolveBridgeScriptPath()
        {
            var candidates = new List<string>
            {
                System.IO.Path.Combine(AppContext.BaseDirectory, "halcon-mcp-bridge.py"),
            };

            // 开发调试场景：bin/Debug(net10.0-windows)/ → 回溯到仓库根。
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 4 && dir != null; i++)
            {
                candidates.Add(System.IO.Path.Combine(dir.FullName, "halcon-mcp-bridge.py"));
                dir = dir.Parent;
            }

            return candidates.FirstOrDefault(System.IO.File.Exists)
                ?? candidates[0];
        }

        private void CopyMcpToken()
        {
            var token = McpToken;
            if (string.IsNullOrEmpty(token))
            {
                MessageBox.Show("令牌尚未生成，请先启动一次 MCP 服务。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Clipboard.SetText(token);
                MessageBox.Show($"已复制 MCP 访问令牌。\n\n外部 MCP 客户端请求时需通过 Authorization: Bearer 头或 ?token= 参数携带该令牌。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch
            {
            }
        }

        private void CopyIdeMcpConfig()
        {
            var port = _mcpServer.Port;
            var token = McpToken;
            var jsonConfig = $@"{{
  ""mcpServers"": {{
    ""halcon-visionstudio-mcp"": {{
      ""url"": ""http://127.0.0.1:{port}/sse"",
      ""headers"": {{
        ""Authorization"": ""Bearer {token}""
      }}
    }}
  }}
}}";
            try
            {
                Clipboard.SetText(jsonConfig);
                MessageBox.Show($"已复制 Cursor / IDE MCP 配置到剪贴板（含鉴权头）！\n\nSSE URL: http://127.0.0.1:{port}/sse 或 http://127.0.0.1:{port}/mcp", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch
            {
            }
        }
    }
}
