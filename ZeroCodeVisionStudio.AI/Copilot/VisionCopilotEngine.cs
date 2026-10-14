using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ZeroCodeVisionStudio.AI.Config;
using ZeroCodeVisionStudio.Shared.Diagnostics;
using ZeroCodeVisionStudio.AI.Tools;

namespace ZeroCodeVisionStudio.AI.Copilot
{
    public class VisionCopilotEngine
    {
        private readonly IVisionStudioAutomationService _automationService;
        private readonly OpenAiCompatibleClient _client;
        private readonly Dispatcher _dispatcher;

        /// <summary>本轮流式对话中通过 capture_viewport_image 收集的图片，附加到下一轮用户消息实现"AI 看图"。</summary>
        private readonly List<string> _pendingImages = new();

        public ObservableCollection<CopilotMessage> Messages { get; } = new ObservableCollection<CopilotMessage>();
        public bool IsBusy { get; private set; }

        public event Action<bool> BusyStateChanged;

        /// <summary>单轮请求内上下文的粗略 token 预算（估算值，超出时裁剪最早的历史消息）。</summary>
        private const int MaxContextTokens = 24000;

        /// <summary>单条工具结果回填对话的最大字符数（超长截断保留首尾）。</summary>
        private const int MaxToolResultChars = 6000;

        private const string SystemPrompt = @"你是一个内置在 ZeroCodeVisionStudio 工业机器视觉低代码软件中的高级 AI 视觉专家工程师。
你的目标是：
1. 协助用户创建、配置和修改机器视觉流程（如图像采集、滤波去噪、阈值分割、形态学处理、轮廓测量、特征匹配、几何拟合等），搭建完成后用 save_scheme 保存方案。
2. 在节点报错或运行异常时，调用故障诊断工具获取错误快照、分析上下游数据链条、参数设置是否超出范围，并明确指出根本原因及修改方案。
3. 闭环调参：使用 tune_node_and_verify 调节参数并立即执行验证物理测量结果（如距离、面积、圆心、得分），进行反馈迭代。
4. 深度算子理解：使用 get_operator_schema 查询算子的输入输出引脚与可调参数定义；使用 get_node_runtime_outputs 读取算子运行生成的精确物理指标。
5. 图像物理分析：使用 get_image_histogram 查看灰度动态范围，使用 auto_compute_threshold 基于 Otsu 大津法智能推荐分割阈值；调用 capture_viewport_image 后截图会自动附加到对话中，可直接基于图像内容做视觉分析。
6. 画布排版与模板：使用 auto_layout_nodes 自动整理节点拓扑；使用 apply_solution_template 一键搭建缺陷检测/尺寸测量/模板匹配标准方案；可用 list_schemes / open_scheme 加载已有方案继续完善。
7. 数据绑定与参数引用：用 get_node_input_bindings 查看节点各输入的来源，用 set_node_input_binding 建立槽位绑定或参数引用（targetInputKey 传 parameter:参数名 可让上游测量值直接驱动下游算子参数）。
8. 执行分析：用 get_execution_records 查执行历史定位瓶颈节点；用 show_node_output 把视口切到指定节点输出再 capture_viewport_image 截图分析；get_image_histogram / auto_compute_threshold 可传 nodeName 分析指定节点的输出图像。
9. 节点寻址出现同名歧义时，先用 rename_node 重命名为唯一名称再继续操作；批量重建流程前先 save_scheme 备份，再用 clear_workflow 清空。

【重要规范】
- 无论何时需要了解画布当前有什么节点或状态，优先调用 get_workflow_state。
- 调参时，建议使用 tune_node_and_verify 一步完成修改和验证，向用户汇报调整前后的物理数据对比。
- 连线时可使用 get_operator_schema 查询引脚键名后，在 connect_nodes 中指定 fromPin/toPin 精确绑定（如 Region、Contour）。
- 给出清晰、专业、严谨的技术回答。在完成一系列工具调用后，用简练语言向用户总结执行的结果。";

        public VisionCopilotEngine(IVisionStudioAutomationService automationService, Dispatcher dispatcher = null)
        {
            _automationService = automationService ?? throw new ArgumentNullException(nameof(automationService));
            _client = new OpenAiCompatibleClient();
            _dispatcher = dispatcher ?? (System.Windows.Application.Current != null ? System.Windows.Application.Current.Dispatcher : Dispatcher.CurrentDispatcher);
        }

        public void ClearHistory()
        {
            InvokeOnUi(() => Messages.Clear());
        }

        public async Task<string> SendMessageAsync(string userText, List<string> images = null, CancellationToken cancellationToken = default)
        {
            // 允许纯图片消息（用户贴图不带文字）：文本缺省为固定引导语。
            if (string.IsNullOrWhiteSpace(userText) && (images == null || images.Count == 0)) return string.Empty;
            userText = string.IsNullOrWhiteSpace(userText) ? "请分析这张图片。" : userText;
            if (IsBusy) return string.Empty;

            SetBusy(true);
            try
            {
                var settings = AiSettingsManager.Current;

                var userMsg = new CopilotMessage
                {
                    Role = MessageRole.User,
                    Content = userText
                };

                // 用户直接附加的图片（粘贴/拖放/📎）：随消息多模态发送；
                // 纯文本模型由 BuildConversation 统一降级为文字说明。
                if (images != null)
                {
                    foreach (var path in images)
                    {
                        if (!string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
                        {
                            userMsg.Images.Add(path);
                        }
                    }
                }
                // user 消息必须同步入列：BuildConversation 紧随其后执行，
                // 异步投递在非 UI 上下文调用时会竞态（请求丢失 user，GLM 报 1214 messages 参数非法）。
                if (_dispatcher.CheckAccess())
                {
                    Messages.Add(userMsg);
                }
                else
                {
                    _dispatcher.Invoke(() => Messages.Add(userMsg));
                }

                var tools = VisionToolCatalog.ToOpenAiTools();

                int loopCount = 0;
                var maxLoops = Math.Clamp(settings.MaxToolLoops > 0 ? settings.MaxToolLoops : 30, 1, 80);
                string finalResultText = "";

                while (loopCount++ < maxLoops)
                {
                    var conversation = BuildConversation(settings);

                    var assistantMsg = new CopilotMessage
                    {
                        Role = MessageRole.Assistant,
                        Content = "",
                        IsStreaming = true
                    };
                    InvokeOnUi(() => Messages.Add(assistantMsg));

                    // 流式增量合并节流：逐 chunk 立即刷新会让绑定/排版以每秒上百次重入，
                    // 与右键菜单等布局时机叠加曾触发 TextBoxView.ArrangeVisuals 内部 FailFast（进程直接终止）。
                    // 合并为约 50ms 一批再投递，收尾时补刷剩余部分。
                    var deltaBuffer = new System.Text.StringBuilder();
                    var reasoningBuffer = new System.Text.StringBuilder();
                    long lastDeltaFlush = 0;
                    var response = await _client.StreamChatAsync(
                        settings,
                        conversation,
                        tools,
                        onDeltaText: delta =>
                        {
                            deltaBuffer.Append(delta);
                            if (Environment.TickCount64 - lastDeltaFlush < 50) return;
                            lastDeltaFlush = Environment.TickCount64;
                            var chunk = deltaBuffer.ToString();
                            deltaBuffer.Clear();
                            InvokeOnUi(() => assistantMsg.Content += chunk);
                        },
                        cancellationToken: cancellationToken,
                        onDeltaReasoning: delta =>
                        {
                            // 推理模型思考流（glm-5.3 reasoning_content）：同样 50ms 合并节流，
                            // 只进界面展示、不回传 API（服务商要求思考内容不放回 messages）。
                            reasoningBuffer.Append(delta);
                            if (Environment.TickCount64 - lastDeltaFlush < 50) return;
                            lastDeltaFlush = Environment.TickCount64;
                            var chunk = reasoningBuffer.ToString();
                            reasoningBuffer.Clear();
                            InvokeOnUi(() => assistantMsg.ReasoningContent += chunk);
                        }
                    );

                    if (deltaBuffer.Length > 0)
                    {
                        var rest = deltaBuffer.ToString();
                        deltaBuffer.Clear();
                        InvokeOnUi(() => assistantMsg.Content += rest);
                    }

                    if (reasoningBuffer.Length > 0)
                    {
                        var restReasoning = reasoningBuffer.ToString();
                        reasoningBuffer.Clear();
                        InvokeOnUi(() => assistantMsg.ReasoningContent += restReasoning);
                    }

                    assistantMsg.IsStreaming = false;

                    if (!response.Success)
                    {
                        // 图片内容类型被拒（智谱 1210 content.type 取值范围 [text] 等）：
                        // 说明当前模型实际不支持图片输入——本会话自动禁用图片并重试一次（自愈，无需用户干预）。
                        if (!_sessionVisionDisabled &&
                            conversation.Any(m => m.HasImages) &&
                            LooksLikeImageTypeError(response.ErrorMessage))
                        {
                            _sessionVisionDisabled = true;
                            assistantMsg.Content = "（检测到当前模型不支持图片输入，已自动切换纯文本模式重试…）";
                            ExecutionLogService.AiChat(
                                $"模型 [{settings.ModelName}] 拒绝图片内容，本会话已自动禁用视觉输入",
                                ExecLogLevel.Warn, response.ErrorMessage);
                            continue;
                        }

                        assistantMsg.Content = $"[错误] {response.ErrorMessage}";
                        finalResultText = assistantMsg.Content;
                        break;
                    }

                    if (response.HasToolCalls)
                    {
                        foreach (var tc in response.ToolCalls)
                        {
                            assistantMsg.ToolCalls.Add(tc);
                        }

                        // 执行工具调用
                        foreach (var toolCall in response.ToolCalls)
                        {
                            var toolResultJson = await ExecuteSingleToolAsync(toolCall.Name, toolCall.Arguments);
                            toolCall.Result = toolResultJson;
                            toolCall.Status = IsToolFailure(toolResultJson)
                                ? ToolExecutionStatus.Failed
                                : ToolExecutionStatus.Success;

                            CollectPendingImage(toolCall.Name, toolResultJson);

                            var toolMsg = new CopilotMessage
                            {
                                Role = MessageRole.Tool,
                                ToolCallId = toolCall.Id,
                                ToolName = toolCall.Name,
                                Content = toolResultJson
                            };
                            InvokeOnUi(() => Messages.Add(toolMsg));
                        }

                        // 将截图作为多模态输入附加到下一轮请求：
                        // 视觉模型带图分析；纯文本模型（glm-4-flash/deepseek-chat 等）不接受
                        // image_url 内容类型（智谱报 1210），降级为文本告知 + 换模型指引。
                        if (_pendingImages.Count > 0)
                        {
                            if (IsVisionEnabled(settings))
                            {
                                var imageMsg = new CopilotMessage
                                {
                                    Role = MessageRole.User,
                                    Content = $"（系统提示：已自动附加 {_pendingImages.Count} 张当前视口截图，请直接结合图像内容进行分析）",
                                    Images = _pendingImages.ToList()
                                };
                                InvokeOnUi(() => Messages.Add(imageMsg));
                            }
                            else
                            {
                                var textMsg = new CopilotMessage
                                {
                                    Role = MessageRole.User,
                                    Content = $"（系统提示：已截取 {_pendingImages.Count} 张视口图像：{string.Join("; ", _pendingImages)}。" +
                                              $"当前模型 [{settings.ModelName}] 不支持图片输入，图像未随请求发送。" +
                                              "如需看图分析，请在 AI 设置中切换到 glm-4v / gpt-4o / gemini 等视觉模型后发送「请分析刚才的截图」）"
                                };
                                InvokeOnUi(() => Messages.Add(textMsg));
                            }
                            _pendingImages.Clear();
                        }

                        // 继续下一轮让大模型总结或继续调用
                        continue;
                    }
                    else
                    {
                        finalResultText = assistantMsg.Content;
                        break;
                    }
                }

                if (loopCount > maxLoops && string.IsNullOrEmpty(finalResultText))
                {
                    finalResultText = $"[提示] 已达到单轮最大工具调用轮次（{maxLoops} 轮），为控制上下文长度已暂停。发送「继续」我将基于对话历史接着完成剩余步骤；也可在 AI 设置中调高「单轮最大工具轮次」。";
                    InvokeOnUi(() => Messages.Add(new CopilotMessage
                    {
                        Role = MessageRole.Assistant,
                        Content = finalResultText
                    }));
                }

                return finalResultText;
            }
            finally
            {
                SetBusy(false);
            }
        }

        public async Task<string> DiagnoseFaultAsync(string nodeName = null)
        {
            var prompt = string.IsNullOrWhiteSpace(nodeName)
                ? "请对当前流程执行诊断：检查是否有失败节点或异常状态，分析故障根因并给出解决建议。"
                : $"请对节点 [{nodeName}] 进行故障诊断：分析其参数、上下游连线及错误信息，并给出修复方案。";
            return await SendMessageAsync(prompt);
        }

        /// <summary>本会话内因模型拒绝图片内容而禁用视觉输入（自愈标记，新会话自动恢复）。</summary>
        private bool _sessionVisionDisabled;

        /// <summary>视觉输入是否可用：手动覆盖优先，否则按模型名自动检测。</summary>
        private bool IsVisionEnabled(AiSettings settings) =>
            !_sessionVisionDisabled &&
            (settings.SupportsVisionOverride ?? Config.VisionModelDetector.IsVisionModel(settings.ModelName));

        /// <summary>错误文本是否为"图片内容类型被拒"（智谱 1210 / OpenAI invalid content type 等）。</summary>
        private static bool LooksLikeImageTypeError(string errorMessage)
        {
            var text = errorMessage ?? "";
            return text.Contains("content.type", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("取值范围 [text]", StringComparison.Ordinal) ||
                   text.Contains("image_url", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("does not support image", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> ExecuteSingleToolAsync(string toolName, string argumentsJson)
        {
            var result = await VisionToolCatalog.ExecuteAsync(_automationService, toolName, argumentsJson);
            return AiJson.Serialize(result);
        }

        /// <summary>capture_viewport_image 的结果自动收集为多模态输入图片。</summary>
        private void CollectPendingImage(string toolName, string resultJson)
        {
            if (!string.Equals(toolName, "capture_viewport_image", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(toolName, "export_node_display", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                using var doc = JsonDocument.Parse(resultJson);
                if (doc.RootElement.TryGetProperty("ViewportImagePath", out var pathProp) &&
                    pathProp.ValueKind == JsonValueKind.String)
                {
                    var path = pathProp.GetString();
                    if (!string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
                    {
                        _pendingImages.Add(path);
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 构建带上下文管理的对话列表：
        /// 系统提示 + 从最新往前累计的历史消息（估算 token 超预算时丢弃更早的消息），
        /// 超长工具结果截断保留首尾，防止长会话撑爆模型上下文窗口。
        /// </summary>
        private List<CopilotMessage> BuildConversation(AiSettings settings)
        {
            // 纯文本模型：历史中的图片消息会被降级为文本，避免每轮请求都因
            // image_url 内容类型被拒（智谱 1210），已污染的会话也能自愈。
            var supportsVision = IsVisionEnabled(settings);
            var systemMsg = new CopilotMessage { Role = MessageRole.System, Content = SystemPrompt };

            List<CopilotMessage> history;
            lock (Messages)
            {
                history = Messages.ToList();
            }

            // 按"工具调用组"原子分组：assistant(tool_calls) 与其全部 tool 响应同生共死。
            // GLM 等严格校验配对的服务商会拒绝被截断的半组（messages 参数非法），
            // DeepSeek/Gemini 宽容不代表结构合法——裁剪必须以组为单位。
            var groups = new List<List<CopilotMessage>>();
            int index = 0;
            while (index < history.Count)
            {
                if (history[index].Role == MessageRole.Assistant && history[index].HasToolCalls)
                {
                    var group = new List<CopilotMessage> { history[index] };
                    index++;
                    while (index < history.Count && history[index].Role == MessageRole.Tool)
                    {
                        group.Add(history[index]);
                        index++;
                    }
                    groups.Add(group);
                }
                else
                {
                    groups.Add(new List<CopilotMessage> { history[index] });
                    index++;
                }
            }

            // 从最新组往旧累计 token，预算不足时整组丢弃（至少保留最后一组）。
            var keptGroups = new List<List<CopilotMessage>>();
            int budget = MaxContextTokens - EstimateTokens(SystemPrompt);
            for (int g = groups.Count - 1; g >= 0; g--)
            {
                int cost = groups[g].Sum(EstimateMessageTokens);
                if (keptGroups.Count > 0 && budget - cost < 0)
                {
                    break;
                }

                keptGroups.Add(groups[g]);
                budget -= cost;
            }

            keptGroups.Reverse();

            var conversation = new List<CopilotMessage> { systemMsg };
            foreach (var msg in keptGroups.SelectMany(group => group))
            {
                var copy = new CopilotMessage
                {
                    Role = msg.Role,
                    Content = msg.Content,
                    ToolCallId = msg.ToolCallId,
                    ToolName = msg.ToolName,
                    Images = supportsVision ? msg.Images : null
                };

                if (!supportsVision && msg.HasImages)
                {
                    copy.Content = (copy.Content ?? "") + "\n[附图未随请求发送：当前模型不支持视觉输入]";
                }

                foreach (var tc in msg.ToolCalls)
                {
                    copy.ToolCalls.Add(tc);
                }

                // 超长工具结果截断，仅影响回传模型的上下文，UI 上仍保留完整结果。
                if (copy.Role == MessageRole.Tool && copy.Content != null && copy.Content.Length > MaxToolResultChars)
                {
                    copy.Content = copy.Content.Substring(0, MaxToolResultChars / 2) +
                        $"\n...[中间内容已省略 {copy.Content.Length - MaxToolResultChars} 字符]...\n" +
                        copy.Content.Substring(copy.Content.Length - MaxToolResultChars / 2);
                }

                conversation.Add(copy);
            }

            // GLM 等服务商要求首条非系统消息必须是 user 角色（2x2 回放对照实验验证：
            // 有 user 则 stream/非 stream 均 200，无 user 均 400）。裁剪或竞态导致 user 缺失时兜底。
            if (conversation.All(m => m.Role != MessageRole.User))
            {
                var fallbackUser = history.FirstOrDefault(m => m.Role == MessageRole.User)
                    ?? new CopilotMessage { Role = MessageRole.User, Content = "（继续执行当前任务）" };
                conversation.Insert(1, fallbackUser);
            }

            return conversation;
        }

        /// <summary>粗略 token 估算：中英混合文本约 1 字符 = 0.7 token，图片按 1000 token 计。</summary>
        private static int EstimateTokens(string text) =>
            string.IsNullOrEmpty(text) ? 0 : (int)(text.Length * 0.7);

        private static int EstimateMessageTokens(CopilotMessage msg)
        {
            int tokens = EstimateTokens(msg.Content);
            foreach (var tc in msg.ToolCalls)
            {
                tokens += EstimateTokens(tc.Arguments) + EstimateTokens(tc.Result);
            }

            if (msg.HasImages)
            {
                tokens += msg.Images.Count * 1000;
            }

            return tokens;
        }

        /// <summary>基于 JSON 解析判断工具结果是否为失败（而非脆弱的字符串匹配）。</summary>
        private static bool IsToolFailure(string json)
        {
            if (string.IsNullOrEmpty(json)) return true;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("Success", out var s) && s.ValueKind == JsonValueKind.False) return true;
                    if (root.TryGetProperty("isError", out var e) && e.ValueKind == JsonValueKind.True) return true;
                    return false;
                }
            }
            catch
            {
            }

            return json.Contains("\"Success\":false") || json.Contains("\"Success\": false");
        }

        private void SetBusy(bool busy)
        {
            IsBusy = busy;
            BusyStateChanged?.Invoke(busy);
        }

        private void InvokeOnUi(Action action)
        {
            if (_dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                // 异步投递：同步 Invoke 会在流式高频更新时与 UI 布局相互等待，
                // 堆积重入压力（曾触发 TextBoxView 布局期 FailFast 崩溃）。
                _dispatcher.BeginInvoke(action);
            }
        }
    }
}
