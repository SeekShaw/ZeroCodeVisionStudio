using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroCodeVisionStudio.Shared.Communication;
using ZeroCodeVisionStudio.Shared.Diagnostics;

namespace ZeroCodeVisionStudio.AI.Tools
{
    /// <summary>单个工具的定义：名称、描述、JSON Schema 参数与执行器。</summary>
    public class VisionToolDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public object ParametersSchema { get; set; }
        public Func<IVisionStudioAutomationService, JsonElement?, Task<object>> Executor { get; set; }
    }

    /// <summary>
    /// 统一的工具目录与分发器。
    /// 内置 Copilot（OpenAI Function Calling）与 MCP Server（tools/call）共用同一份定义和执行逻辑，
    /// 避免两套 switch 分发器重复维护。
    /// </summary>
    public static class VisionToolCatalog
    {
        /// <summary>全量工具定义（含执行器），按名称字典索引。</summary>
        public static readonly List<VisionToolDefinition> All = BuildCatalog();

        public static VisionToolDefinition Find(string name) =>
            string.IsNullOrEmpty(name) ? null : All.FirstOrDefault(t =>
                string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>执行一次工具调用。未知工具返回 {Success:false}。全量记录执行日志（内置 Copilot 与 MCP Server 共用此入口）。</summary>
        public static async Task<object> ExecuteAsync(
            IVisionStudioAutomationService service, string toolName, string argumentsJson)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var tool = Find(toolName);
                if (tool == null)
                {
                    ExecutionLogService.AiTool(toolName, argumentsJson, 0, false, "未识别的工具名称");
                    return new { Success = false, Message = $"未识别的工具名称: {toolName}" };
                }

                JsonElement? root = null;
                if (!string.IsNullOrWhiteSpace(argumentsJson))
                {
                    using var doc = JsonDocument.Parse(argumentsJson);
                    root = doc.RootElement.Clone();
                }

                var result = await tool.Executor(service, root);
                ExecutionLogService.AiTool(toolName, argumentsJson, stopwatch.Elapsed.TotalMilliseconds,
                    IsResultSuccess(result), DescribeResult(result));
                return result;
            }
            catch (Exception ex)
            {
                ExecutionLogService.AiTool(toolName, argumentsJson, stopwatch.Elapsed.TotalMilliseconds,
                    false, ex.Message, ex.ToString());
                return new { Success = false, Message = $"工具调用异常: {ex.Message}" };
            }
        }

        /// <summary>结果对象带 Success 属性按其判断；列表等无该属性的结果默认视为成功。</summary>
        private static bool IsResultSuccess(object result)
        {
            try
            {
                if (result?.GetType().GetProperty("Success")?.GetValue(result) is bool flag)
                    return flag;
                return true;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>结果摘要（Message 优先，其次集合数量，避免为大结果做全量序列化）。</summary>
        private static string DescribeResult(object result)
        {
            try
            {
                if (result == null) return "(无返回)";
                if (result.GetType().GetProperty("Message")?.GetValue(result) is string message &&
                    !string.IsNullOrWhiteSpace(message))
                    return message.Length > 300 ? message.Substring(0, 300) + "…" : message;
                if (result is System.Collections.ICollection collection)
                    return $"{collection.Count} 项";
                var text = result.ToString();
                return text.Length > 300 ? text.Substring(0, 300) + "…" : text;
            }
            catch
            {
                return "";
            }
        }

        /// <summary>输出 OpenAI Function Calling 格式的工具定义列表。</summary>
        public static List<object> ToOpenAiTools() => All.Select(t => (object)new
        {
            type = "function",
            function = new
            {
                name = t.Name,
                description = t.Description,
                parameters = t.ParametersSchema
            }
        }).ToList();

        /// <summary>输出 MCP tools/list 格式的工具定义列表。</summary>
        public static List<object> ToMcpTools() => All.Select(t => (object)new
        {
            name = t.Name,
            description = t.Description,
            inputSchema = t.ParametersSchema
        }).ToList();

        // ==================== 日志 / 通讯查询 ====================

        /// <summary>get_execution_logs：查询统一执行日志（内存缓冲最近 2000 条）。</summary>
        private static Task<object> QueryExecutionLogs(JsonElement? e)
        {
            var category = S(e, "category");
            var level = S(e, "level");
            var keyword = S(e, "keyword");
            var limit = (int)Math.Clamp(D(e, "limit") ?? 50, 1, 200);

            var catFilter = ParseCategory(category);
            var levelFilter = ParseLevel(level);

            var entries = ExecutionLogService.Snapshot()
                .Where(x => catFilter == null || x.Category == catFilter)
                .Where(x => levelFilter == null || x.Level == levelFilter)
                .Where(x => string.IsNullOrWhiteSpace(keyword) ||
                            (x.Scope?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                            (x.Message?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                .TakeLast(limit)
                .Select(x => new
                {
                    Time = x.Timestamp.ToString("HH:mm:ss.fff"),
                    Category = x.CategoryText,
                    Level = x.LevelText,
                    Scope = x.Scope,
                    Message = x.Message,
                    Detail = string.IsNullOrWhiteSpace(x.Detail) ? null : x.Detail,
                    DurationMs = x.DurationMs
                })
                .ToList();

            return Task.FromResult<object>(new
            {
                Success = true,
                Message = $"共 {entries.Count} 条日志（类别 {catFilter?.ToString() ?? "全部"}，级别 {levelFilter?.ToString() ?? "全部"}）",
                Logs = entries
            });
        }

        private static ExecLogCategory? ParseCategory(string text) => (text ?? "").Trim().ToLowerInvariant() switch
        {
            "workflow" or "流程" => ExecLogCategory.Workflow,
            "node" or "算子" => ExecLogCategory.Node,
            "aitool" or "ai工具" => ExecLogCategory.AiTool,
            "aichat" or "ai会话" => ExecLogCategory.AiChat,
            "comm" or "通讯" => ExecLogCategory.Comm,
            "app" or "应用" => ExecLogCategory.App,
            _ => null
        };

        private static ExecLogLevel? ParseLevel(string text) => (text ?? "").Trim().ToLowerInvariant() switch
        {
            "info" or "信息" => ExecLogLevel.Info,
            "warn" or "warning" or "警告" => ExecLogLevel.Warn,
            "error" or "错误" => ExecLogLevel.Error,
            _ => null
        };

        /// <summary>get_comm_drivers：通讯驱动中心只读清单（配置 + 实时状态）。</summary>
        private static Task<object> QueryCommDrivers()
        {
            List<object> drivers;
            try
            {
                drivers = CommunicationCenter.Configs.Select(c =>
                {
                    var instance = CommunicationCenter.GetExistingDriver(c.Name);
                    return new
                    {
                        c.Name,
                        Type = c.Type.ToString(),
                        Summary = c.Summary,
                        c.AutoConnect,
                        c.TriggerEnabled,
                        c.TriggerWorkflowName,
                        IsConnected = instance?.IsConnected == true,
                        Status = instance?.StatusText ?? "未连接（无实例）",
                        LastError = instance?.LastError ?? "",
                        Stats = instance?.StatsText ?? ""
                    };
                }).Cast<object>().ToList();
            }
            catch (Exception ex)
            {
                return Task.FromResult<object>(new { Success = false, Message = $"读取通讯驱动配置失败: {ex.Message}" });
            }

            return Task.FromResult<object>(new
            {
                Success = true,
                Message = $"共 {drivers.Count} 个通讯驱动",
                Drivers = drivers
            });
        }

        // ==================== 参数扫描 / 图像导出 / 流程体检 ====================

        /// <summary>sweep_node_parameter：候选值逐档执行并采集输出，结束后恢复原参数。</summary>
        private static async Task<object> SweepNodeParameterAsync(IVisionStudioAutomationService service, JsonElement? e)
        {
            var nodeName = S(e, "nodeName");
            var parameterName = S(e, "parameterName");
            if (string.IsNullOrWhiteSpace(nodeName) || string.IsNullOrWhiteSpace(parameterName))
                return new { Success = false, Message = "缺少 nodeName / parameterName 参数" };

            if (!e.HasValue || !e.Value.TryGetProperty("values", out var valuesArr) || valuesArr.ValueKind != JsonValueKind.Array)
                return new { Success = false, Message = "缺少 values 候选值数组" };

            var values = new List<double>();
            foreach (var item in valuesArr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number) values.Add(item.GetDouble());
                else if (item.ValueKind == JsonValueKind.String && double.TryParse(item.GetString(), out var parsed)) values.Add(parsed);
            }
            if (values.Count == 0)
                return new { Success = false, Message = "values 中没有有效数值" };
            if (values.Count > 12)
                return new { Success = false, Message = "单次最多扫描 12 个候选值" };

            var scope = (S(e, "scope") ?? "workflow").Trim().ToLowerInvariant();
            var executeMode = scope == "node" ? nodeName : "all";
            var restore = !e.Value.TryGetProperty("restoreOriginal", out var restoreFlag) || restoreFlag.ValueKind != JsonValueKind.False;
            var metricKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (e.Value.TryGetProperty("metricKeys", out var keysArr) && keysArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var k in keysArr.EnumerateArray())
                    if (k.ValueKind == JsonValueKind.String) metricKeys.Add(k.GetString());
            }

            object original = null;
            var parameters = await service.GetNodeParametersAsync(nodeName);
            original = parameters?.FirstOrDefault(p => string.Equals(p.Name, parameterName, StringComparison.OrdinalIgnoreCase))?.Value;

            var results = new List<object>();
            var bestIndex = -1;
            var bestScore = double.MinValue;
            try
            {
                for (var i = 0; i < values.Count; i++)
                {
                    var set = await service.SetNodeParametersAsync(nodeName, new Dictionary<string, object> { [parameterName] = values[i] });
                    var exec = await service.ExecuteWorkflowAsync(executeMode);
                    var outputs = await CollectOutputsAsync(service, nodeName, metricKeys);
                    var success = set.Success && exec.Success;

                    // 以首个数值型输出作为"越大越好"的择优参考（仅提示，不替用户做决策）
                    if (success && outputs.Count > 0)
                    {
                        var firstNumeric = outputs.FirstOrDefault(kv => kv.Value is double or int or long);
                        if (firstNumeric.Value is double d && d > bestScore) { bestScore = d; bestIndex = i; }
                        else if (firstNumeric.Value is int iv && iv > bestScore) { bestScore = iv; bestIndex = i; }
                    }

                    results.Add(new
                    {
                        Value = values[i],
                        Success = success,
                        Error = success ? null : (set.Success ? exec.Message : set.Message),
                        Outputs = outputs
                    });
                }
            }
            finally
            {
                if (restore && original != null)
                {
                    await service.SetNodeParametersAsync(nodeName, new Dictionary<string, object> { [parameterName] = original });
                }
            }

            return new
            {
                Success = true,
                Message = $"参数 [{parameterName}] 扫描 {values.Count} 档完成（每档执行 {(scope == "node" ? "单节点" : "整个流程")}，已{(restore ? "恢复原值" : "保持最后一档")}）" +
                          (bestIndex >= 0 ? $"；按首个数值输出择优提示：第 {bestIndex + 1} 档 ({values[bestIndex]})" : ""),
                ParameterName = parameterName,
                Restored = restore && original != null,
                Results = results
            };
        }

        private static async Task<Dictionary<string, object>> CollectOutputsAsync(
            IVisionStudioAutomationService service, string nodeName, HashSet<string> metricKeys)
        {
            var result = new Dictionary<string, object>();
            try
            {
                var outputs = await service.GetNodeRuntimeOutputsAsync(nodeName);
                if (outputs == null) return result;
                foreach (var kv in outputs)
                {
                    if (metricKeys.Count > 0 && !metricKeys.Contains(kv.Key)) continue;
                    if (kv.Value == null) continue;
                    if (kv.Value is double or float or int or long or short or bool or string)
                        result[kv.Key] = kv.Value is float f ? (double)f : kv.Value;
                }
            }
            catch
            {
                // 输出采集失败不影响扫描继续
            }
            return result;
        }

        /// <summary>export_node_display：切换视口到节点并导出 PNG（结果自动进入多模态对话）。</summary>
        private static async Task<object> ExportNodeDisplayAsync(
            IVisionStudioAutomationService service, string nodeName, string filePath)
        {
            if (string.IsNullOrWhiteSpace(nodeName))
                return new { Success = false, Message = "缺少 nodeName 参数" };

            var show = await service.ShowNodeOutputAsync(nodeName);
            if (!show.Success)
                return new { Success = false, Message = $"切换节点显示失败：{show.Message}" };

            string path;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                var safeName = string.Join("_", nodeName.Split(Path.GetInvalidFileNameChars()));
                path = Path.Combine(Path.GetTempPath(), $"hvs_node_{safeName}_{DateTime.Now:HHmmss_fff}.png");
            }
            else
            {
                path = filePath;
            }

            var captured = await service.CaptureViewportImageAsync(path);
            if (string.IsNullOrWhiteSpace(captured))
                return new { Success = false, Message = "导出图像失败（视口无显示内容？节点需先执行）" };

            return new
            {
                Success = true,
                Message = $"已导出节点 [{nodeName}] 显示图像",
                ViewportImagePath = captured,
                Path = captured
            };
        }

        /// <summary>validate_workflow：运行前体检（孤立节点/无上游输入/上次失败）。</summary>
        private static async Task<object> ValidateWorkflowAsync(IVisionStudioAutomationService service)
        {
            var state = await service.GetWorkflowStateAsync();
            var issues = new List<object>();
            var conditionalNodes = new List<string>();

            var connected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var conn in state.Connections)
            {
                connected.Add(conn.SourceNode);
                connected.Add(conn.TargetNode);
            }

            foreach (var node in state.Nodes)
            {
                if (!connected.Contains(node.Name))
                    issues.Add(new { Severity = "警告", Node = node.Name, Issue = "孤立节点：未与任何节点连线，不会产生有效数据流" });

                var hasIncoming = state.Connections.Any(c =>
                    string.Equals(c.TargetNode, node.Name, StringComparison.OrdinalIgnoreCase));
                if (!hasIncoming && node.InputPins != null && node.InputPins.Count > 0)
                    issues.Add(new { Severity = "提示", Node = node.Name, Issue = $"声明了 {node.InputPins.Count} 个输入槽但无任何上游连线（若非图像源节点请检查）" });

                if (string.Equals(node.Status, "Failed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(node.Status, "失败", StringComparison.OrdinalIgnoreCase))
                    issues.Add(new { Severity = "错误", Node = node.Name, Issue = $"上次执行失败：{node.StatusMessage}" });

                if (!string.IsNullOrWhiteSpace(node.ExecutionCondition))
                    conditionalNodes.Add(node.Name);
            }

            // 环检测：DFS 三色标记。成环的节点无法完成拓扑排序，流程将无法执行
            var graph = state.Nodes.ToDictionary(
                n => n.Name,
                n => state.Connections
                    .Where(c => string.Equals(c.SourceNode, n.Name, StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.TargetNode)
                    .Where(t => state.Nodes.Any(n => string.Equals(n.Name, t, StringComparison.OrdinalIgnoreCase)))
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);
            var color = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); // 0=未访问 1=栈中 2=完成
            var inCycle = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in graph.Keys)
            {
                if (color.TryGetValue(name, out var c0) && c0 != 0) continue;
                Dfs(name);
            }
            void Dfs(string name)
            {
                color[name] = 1;
                if (graph.TryGetValue(name, out var nexts))
                {
                    foreach (var next in nexts)
                    {
                        if (!color.TryGetValue(next, out var c1)) c1 = 0;
                        if (c1 == 1) { inCycle.Add(name); inCycle.Add(next); }
                        else if (c1 == 0) Dfs(next);
                    }
                }
                color[name] = 2;
            }
            if (inCycle.Count > 0)
                issues.Add(new
                {
                    Severity = "错误",
                    Node = string.Join(", ", inCycle.OrderBy(x => x)),
                    Issue = $"检测到连线成环（{inCycle.Count} 个节点互为上下游），拓扑排序无法完成，请断开环上一条连线"
                });

            return new
            {
                Success = true,
                Message = issues.Count == 0
                    ? $"流程体检通过：{state.Nodes.Count} 个节点无发现隐患{(conditionalNodes.Count > 0 ? $"（{conditionalNodes.Count} 个节点带执行条件）" : "")}"
                    : $"流程体检发现 {issues.Count} 个问题（错误 {issues.Count(i => ((dynamic)i).Severity == "错误")} / 警告 {issues.Count(i => ((dynamic)i).Severity == "警告")}）",
                Issues = issues,
                ConditionalNodes = conditionalNodes
            };
        }

        // ==================== 批量建链 ====================

        /// <summary>
        /// build_pipeline 执行器：顺序 add_node + 首尾 connect_nodes，
        /// 把 2N-1 次工具调用折叠为 1 次（多节点流程搭建不再触发单轮调用上限）。
        /// </summary>
        private static async Task<object> BuildPipelineAsync(IVisionStudioAutomationService service, JsonElement? e)
        {
            if (!e.HasValue || !e.Value.TryGetProperty("operators", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return new { Success = false, Message = "缺少 operators 数组参数" };

            // 元素支持两种形式：纯字符串算子名，或 { "operator": 算子名, "name": 自定义节点名, "parameters": { 参数 } }
            var plan = new List<(string Operator, string Name, Dictionary<string, object> Parameters)>();
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var op = item.GetString();
                    if (!string.IsNullOrWhiteSpace(op)) plan.Add((op, null, null));
                }
                else if (item.ValueKind == JsonValueKind.Object &&
                         item.TryGetProperty("operator", out var opProp) &&
                         opProp.ValueKind == JsonValueKind.String &&
                         !string.IsNullOrWhiteSpace(opProp.GetString()))
                {
                    string customName = null;
                    if (item.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                        customName = nameProp.GetString();
                    Dictionary<string, object> nodeParams = null;
                    if (item.TryGetProperty("parameters", out var paramObj) && paramObj.ValueKind == JsonValueKind.Object)
                    {
                        nodeParams = new Dictionary<string, object>();
                        foreach (var kv in paramObj.EnumerateObject()) nodeParams[kv.Name] = kv.Value.Clone();
                    }
                    plan.Add((opProp.GetString(), customName, nodeParams));
                }
            }
            if (plan.Count == 0)
                return new { Success = false, Message = "operators 数组为空或元素格式无效" };
            var operators = plan.Select(x => x.Operator).ToList();
            if (operators.Count > 30)
                return new { Success = false, Message = "单次最多创建 30 个节点，请分批" };

            var autoConnect = !e.Value.TryGetProperty("autoConnect", out var flag) || flag.ValueKind != JsonValueKind.False;

            var created = new List<string>();
            for (var idx = 0; idx < plan.Count; idx++)
            {
                var step = plan[idx];
                var add = await service.AddNodeAsync(step.Operator);
                var nodeName = (add.Data as NodeDto)?.Name;
                if (!add.Success || string.IsNullOrEmpty(nodeName))
                    return new
                    {
                        Success = false,
                        Message = $"创建 [{step.Operator}] 失败：{add.Message}（本次已创建 {created.Count} 个：{string.Join(" → ", created)}）",
                        Nodes = created
                    };

                if (!string.IsNullOrWhiteSpace(step.Name))
                {
                    var rename = await service.RenameNodeAsync(nodeName, step.Name);
                    if (rename.Success) nodeName = step.Name;
                }
                if (step.Parameters != null && step.Parameters.Count > 0)
                {
                    await service.SetNodeParametersAsync(nodeName, step.Parameters);
                }
                created.Add(nodeName);
            }

            var connections = new List<string>();
            if (autoConnect)
            {
                for (var i = 0; i + 1 < created.Count; i++)
                {
                    var link = await service.ConnectNodesAsync(created[i], null, created[i + 1], null);
                    if (!link.Success)
                        return new
                        {
                            Success = false,
                            Message = $"连线 {created[i]} → {created[i + 1]} 失败：{link.Message}（节点已全部创建，可用 connect_nodes 手动补线）",
                            Nodes = created,
                            Connections = connections
                        };
                    connections.Add($"{created[i]} → {created[i + 1]}");
                }
            }

            return new
            {
                Success = true,
                Message = $"已创建 {created.Count} 个节点{(autoConnect ? $"并自动连接 {connections.Count} 条连线" : "")}：{string.Join(" → ", created)}",
                Nodes = created,
                Connections = connections
            };
        }

        // ==================== 参数读取辅助 ====================

        private static string S(JsonElement? e, string name) =>
            e.HasValue && e.Value.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
                ? p.GetString() : null;

        private static double? D(JsonElement? e, string name)
        {
            if (e.HasValue && e.Value.TryGetProperty(name, out var p))
            {
                if (p.ValueKind == JsonValueKind.Number) return p.GetDouble();
                if (p.ValueKind == JsonValueKind.String && double.TryParse(p.GetString(), out var v)) return v;
            }
            return null;
        }

        private static bool B(JsonElement? e, string name) =>
            e.HasValue && e.Value.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;

        private static Dictionary<string, object> ParamDict(JsonElement? e, string prop = "parameters")
        {
            var dict = new Dictionary<string, object>();
            if (e.HasValue && e.Value.TryGetProperty(prop, out var elem) && elem.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in elem.EnumerateObject())
                {
                    dict[p.Name] = p.Value.Clone();
                }
            }
            return dict;
        }

        private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

        // ==================== 目录构建 ====================

        private static List<VisionToolDefinition> BuildCatalog()
        {
            var tools = new List<VisionToolDefinition>
            {
                new VisionToolDefinition
                {
                    Name = "get_workflow_state",
                    Description = "获取当前活动工作流的完整节点列表、连线拓扑与节点运行状态（节点含 executionCondition 执行条件与参数快照；parallelExecution 表示当前流程并行模式：同层兄弟分支并发、层间串行）",
                    ParametersSchema = new { type = "object", properties = new { } },
                    Executor = async (s, e) => await s.GetWorkflowStateAsync()
                },
                new VisionToolDefinition
                {
                    Name = "get_operator_catalog",
                    Description = "获取可用算子目录。默认紧凑模式只返回名称+分类（适合浏览全量）；filter 按名称/分类子串过滤；includeDetail=true 附带功能说明与输入输出引脚",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            filter = new { type = "string", description = "可选：按算子名/显示名/分类的子串过滤（如 阈值、测量、Image）" },
                            includeDetail = new { type = "boolean", description = "可选：附带说明与引脚（输出较大，建议配合 filter 缩小范围）" }
                        }
                    },
                    Executor = async (s, e) => await s.GetOperatorCatalogAsync(S(e, "filter"), B(e, "includeDetail"))
                },
                new VisionToolDefinition
                {
                    Name = "add_node",
                    Description = "在画布中添加算子节点",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            operatorName = new { type = "string", description = "算子名称或Key，例如 读取图像, 阈值分割, MeanImage, Threshold 等" },
                            x = new { type = "number", description = "可选的画布 X 坐标" },
                            y = new { type = "number", description = "可选的画布 Y 坐标" }
                        },
                        required = new[] { "operatorName" }
                    },
                    Executor = async (s, e) => await s.AddNodeAsync(S(e, "operatorName") ?? S(e, "operator_name"), D(e, "x"), D(e, "y"))
                },
                new VisionToolDefinition
                {
                    Name = "build_pipeline",
                    Description = "按顺序批量创建算子节点并自动首尾相连：一次调用完成整条流水线（等价于多次 add_node + connect_nodes，节点多时优先用本工具避免触发单轮调用上限）。operators 元素可以是算子名字符串，或对象 {\"operator\": 算子名, \"name\": 自定义节点名, \"parameters\": {初始参数}}，一步完成建链+命名+配参",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            operators = new { type = "array", items = new { type = "string" }, description = "按执行顺序的算子列表：算子名字符串，或 {operator,name,parameters} 对象（一步建链+命名+配参）" },
                            autoConnect = new { type = "boolean", description = "是否按列表顺序自动首尾相连（默认 true；为 false 时仅创建节点）" }
                        },
                        required = new[] { "operators" }
                    },
                    Executor = BuildPipelineAsync
                },
                new VisionToolDefinition
                {
                    Name = "get_execution_logs",
                    Description = "查询统一执行日志（流程运行/算子执行/AI工具调用/大模型请求/应用事件），是故障排查的第一数据源：节点失败的完整异常堆栈、AI 工具调用参数与耗时、模型接口 HTTP 错误都在这里",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            category = new { type = "string", description = "可选类别过滤：workflow(流程)/node(算子)/aiTool(AI工具)/aiChat(AI会话)/comm(通讯)/app(应用)；留空=全部" },
                            level = new { type = "string", description = "可选级别过滤：info/warn/error（也接受 信息/警告/错误）；留空=全部" },
                            keyword = new { type = "string", description = "可选关键字（匹配节点名/工具名/消息内容）" },
                            limit = new { type = "number", description = "最多返回条数（默认 50，从最新往前取）" }
                        }
                    },
                    Executor = async (s, e) => QueryExecutionLogs(e)
                },
                new VisionToolDefinition
                {
                    Name = "get_comm_drivers",
                    Description = "列出通讯驱动中心配置的全部驱动（TCP/UDP/串口/Modbus/MQTT/WebSocket/西门子S7/三菱MC/欧姆龙FINS）及其实时连接状态、最近错误与收发统计，用于排查设备通讯/PLC 连接问题",
                    ParametersSchema = new { type = "object", properties = new { } },
                    Executor = async (s, e) => QueryCommDrivers()
                },
                new VisionToolDefinition
                {
                    Name = "sweep_node_parameter",
                    Description = "参数扫描自动调参：对指定节点的单个数值参数依次尝试一组候选值，每档执行一次流程（或单节点）并采集该节点输出，最后恢复原参数值。适合阈值/分数等参数寻优（如 values [80,100,120,140]）",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            nodeName = new { type = "string", description = "目标节点名" },
                            parameterName = new { type = "string", description = "要扫描的参数属性名（先可用 get_node_parameters 查看）" },
                            values = new { type = "array", items = new { type = "number" }, description = "候选值列表（最多 12 个）" },
                            scope = new { type = "string", description = "每档执行范围：workflow(默认，整个流程) 或 node(仅该节点及其上游按单步执行)" },
                            metricKeys = new { type = "array", items = new { type = "string" }, description = "可选：只采集这些输出键（如 MatchScores/RegionArea）；留空采集全部可序列化输出" },
                            restoreOriginal = new { type = "boolean", description = "结束后是否恢复原参数值（默认 true）" }
                        },
                        required = new[] { "nodeName", "parameterName", "values" }
                    },
                    Executor = SweepNodeParameterAsync
                },
                new VisionToolDefinition
                {
                    Name = "export_node_display",
                    Description = "导出指定节点的显示输出为 PNG 图片（切换视口到该节点后截取），返回图片路径；该结果会自动作为多模态图片进入对话，AI 可直接查看中间处理效果并据此调参",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            nodeName = new { type = "string", description = "目标节点名（需已执行过、有显示输出）" },
                            filePath = new { type = "string", description = "可选保存路径；留空自动生成临时文件" }
                        },
                        required = new[] { "nodeName" }
                    },
                    Executor = async (s, e) => await ExportNodeDisplayAsync(s, S(e, "nodeName"), S(e, "filePath"))
                },
                new VisionToolDefinition
                {
                    Name = "validate_workflow",
                    Description = "流程运行前体检：检查连线成环（拓扑死锁，流程无法执行）、孤立节点、无上游输入的非源节点、上次执行失败的节点，并列出带执行条件的节点，返回问题清单",
                    ParametersSchema = new { type = "object", properties = new { } },
                    Executor = async (s, e) => await ValidateWorkflowAsync(s)
                },
                new VisionToolDefinition
                {
                    Name = "remove_node",
                    Description = "从当前工作流中删除指定的算子节点",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new { nodeName = new { type = "string", description = "要删除的节点显示名称" } },
                        required = new[] { "nodeName" }
                    },
                    Executor = async (s, e) => await s.RemoveNodeAsync(S(e, "nodeName") ?? S(e, "node_name"))
                },
                new VisionToolDefinition
                {
                    Name = "rename_node",
                    Description = "重命名画布上的算子节点。当出现同名节点造成寻址歧义时，可用它重命名为唯一名称",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            nodeName = new { type = "string", description = "当前节点显示名称" },
                            newName = new { type = "string", description = "新的节点名称（同一流程内不可与其它节点重名）" }
                        },
                        required = new[] { "nodeName", "newName" }
                    },
                    Executor = async (s, e) => await s.RenameNodeAsync(S(e, "nodeName") ?? S(e, "node_name"), S(e, "newName") ?? S(e, "new_name"))
                },
                new VisionToolDefinition
                {
                    Name = "move_node",
                    Description = "移动节点到指定画布坐标",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            nodeName = new { type = "string", description = "节点显示名称" },
                            x = new { type = "number", description = "目标 X 坐标" },
                            y = new { type = "number", description = "目标 Y 坐标" }
                        },
                        required = new[] { "nodeName", "x", "y" }
                    },
                    Executor = async (s, e) => await s.MoveNodeAsync(S(e, "nodeName") ?? S(e, "node_name"), D(e, "x") ?? 0, D(e, "y") ?? 0)
                },
                new VisionToolDefinition
                {
                    Name = "select_node",
                    Description = "在画布中高亮选中指定节点并显示其输出，便于向用户指示当前关注的节点",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new { nodeName = new { type = "string", description = "节点显示名称" } },
                        required = new[] { "nodeName" }
                    },
                    Executor = async (s, e) => await s.SelectNodeAsync(S(e, "nodeName") ?? S(e, "node_name"))
                },
                new VisionToolDefinition
                {
                    Name = "connect_nodes",
                    Description = "在两个算子节点之间建立连线。连线即数据流：会自动按类型匹配建立默认输入绑定（数据只通过显式绑定传递），可选指定引脚（数据槽键名，如 Region/Contour/Output/RawImage）实现精确绑定",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            fromNode = new { type = "string", description = "上游节点名称" },
                            fromPin = new { type = "string", description = "可选：上游输出槽键名（可用 get_operator_schema 查询），如 Region、Contour、Output" },
                            toNode = new { type = "string", description = "下游节点名称" },
                            toPin = new { type = "string", description = "可选：下游输入槽键名" }
                        },
                        required = new[] { "fromNode", "toNode" }
                    },
                    Executor = async (s, e) => await s.ConnectNodesAsync(
                        S(e, "fromNode") ?? S(e, "from_node"),
                        S(e, "fromPin") ?? S(e, "from_pin") ?? "Output",
                        S(e, "toNode") ?? S(e, "to_node"),
                        S(e, "toPin") ?? S(e, "to_pin") ?? "Input")
                },
                new VisionToolDefinition
                {
                    Name = "disconnect_nodes",
                    Description = "断开两个节点之间的连线",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            fromNode = new { type = "string", description = "上游节点名称" },
                            toNode = new { type = "string", description = "下游节点名称" }
                        },
                        required = new[] { "fromNode", "toNode" }
                    },
                    Executor = async (s, e) => await s.DisconnectNodesAsync(S(e, "fromNode") ?? S(e, "from_node"), S(e, "toNode") ?? S(e, "to_node"))
                },
                new VisionToolDefinition
                {
                    Name = "get_node_parameters",
                    Description = "读取指定算子节点的当前参数配置",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new { nodeName = new { type = "string", description = "节点显示名称" } },
                        required = new[] { "nodeName" }
                    },
                    Executor = async (s, e) => await s.GetNodeParametersAsync(S(e, "nodeName") ?? S(e, "node_name"))
                },
                new VisionToolDefinition
                {
                    Name = "set_node_parameters",
                    Description = "批量修改指定算子节点的参数值",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            nodeName = new { type = "string", description = "节点显示名称" },
                            parameters = new { type = "object", description = "要修改的参数键值对字典，例如 {\"MinGray\": 100, \"MaxGray\": 255}" }
                        },
                        required = new[] { "nodeName", "parameters" }
                    },
                    Executor = async (s, e) => await s.SetNodeParametersAsync(S(e, "nodeName") ?? S(e, "node_name"), ParamDict(e))
                },
                new VisionToolDefinition
                {
                    Name = "execute_workflow",
                    Description = "同步执行工作流并等待完成，返回执行结果汇总（成功/失败节点数、失败原因）。mode 传 'all' 执行整个流程，或传节点名单步执行",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            mode = new { type = "string", description = "执行模式：'all' 执行整个流程；或传入指定节点名称进行单节点执行" }
                        }
                    },
                    Executor = async (s, e) => await s.ExecuteWorkflowAsync(S(e, "mode") ?? "all")
                },
                new VisionToolDefinition
                {
                    Name = "stop_execution",
                    Description = "停止当前正在执行的工作流或连续运行循环",
                    ParametersSchema = new { type = "object", properties = new { } },
                    Executor = async (s, e) => await s.StopExecutionAsync()
                },
                new VisionToolDefinition
                {
                    Name = "get_fault_snapshot",
                    Description = "获取算子节点的报错详情、参数快照及上游拓扑，用于 AI 根因排查诊断",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new { nodeName = new { type = "string", description = "可选。指定排查的节点名称；留空则自动定位首个失败节点" } }
                    },
                    Executor = async (s, e) => await s.GetFaultSnapshotAsync(S(e, "nodeName") ?? S(e, "node_name"))
                },
                new VisionToolDefinition
                {
                    Name = "capture_viewport_image",
                    Description = "截取当前视口图像。对支持视觉的多模态模型，截图会自动作为图片附加到后续对话中供直接查看分析",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new { targetPath = new { type = "string", description = "可选的保存路径（仅允许临时目录/用户目录内）" } }
                    },
                    Executor = async (s, e) => new { ViewportImagePath = await s.CaptureViewportImageAsync(S(e, "targetPath")) }
                },
                new VisionToolDefinition
                {
                    Name = "create_workflow",
                    Description = "在当前方案中新建一个独立工作流 Tab",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new { workflowName = new { type = "string", description = "新工作流的名称" } },
                        required = new[] { "workflowName" }
                    },
                    Executor = async (s, e) => await s.CreateWorkflowAsync(S(e, "workflowName") ?? S(e, "workflow_name") ?? "新流程")
                },
                new VisionToolDefinition
                {
                    Name = "switch_workflow",
                    Description = "切换当前活动的工作流 Tab（后续的节点操作将在切换后的流程中进行）",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new { workflowName = new { type = "string", description = "目标工作流名称（可从 get_workflow_state 的 AllWorkflows 查询）" } },
                        required = new[] { "workflowName" }
                    },
                    Executor = async (s, e) => await s.SwitchWorkflowAsync(S(e, "workflowName") ?? S(e, "workflow_name"))
                },
                new VisionToolDefinition
                {
                    Name = "save_scheme",
                    Description = "保存当前方案（全部流程、节点、参数、连线）到 JSON 文件。path 省略时保存到当前路径或默认方案目录",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new { path = new { type = "string", description = "可选的目标文件路径（.json）。建议放在默认方案目录内" } }
                    },
                    Executor = async (s, e) => await s.SaveSchemeAsync(S(e, "path"))
                },
                new VisionToolDefinition
                {
                    Name = "open_scheme",
                    Description = "打开并加载已保存的方案文件（替换当前画布内容），返回加载报告（缺失算子、恢复失败的参数等）",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new { path = new { type = "string", description = "方案文件路径，可先用 list_schemes 查询" } },
                        required = new[] { "path" }
                    },
                    Executor = async (s, e) => await s.OpenSchemeAsync(S(e, "path"))
                },
                new VisionToolDefinition
                {
                    Name = "list_schemes",
                    Description = "列出默认方案目录中所有已保存的方案文件（文件名、路径、修改时间、大小；不含「示例方案」子目录的 13 个内置示例，打开示例可直接 open_scheme 传 示例方案/xxx.json 相对完整路径）",
                    ParametersSchema = new { type = "object", properties = new { } },
                    Executor = async (s, e) => await s.ListSchemesAsync()
                },
                new VisionToolDefinition
                {
                    Name = "get_operator_schema",
                    Description = "深度透视算子元数据定义：输入引脚、输出引脚、全部可配置参数名、类型、默认值与说明",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new { operatorKey = new { type = "string", description = "算子唯一键名或类型名称，如 Threshold, ImageInput, ContourProcessing 等" } },
                        required = new[] { "operatorKey" }
                    },
                    Executor = async (s, e) => await s.GetOperatorSchemaAsync(S(e, "operatorKey") ?? S(e, "operator_key") ?? S(e, "operatorName"))
                },
                new VisionToolDefinition
                {
                    Name = "get_node_runtime_outputs",
                    Description = "获取算子节点执行后产生的物理输出数据（测量尺寸、面积、圆心坐标、得分、耗时等）",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new { nodeName = new { type = "string", description = "画布上的节点显示名称" } },
                        required = new[] { "nodeName" }
                    },
                    Executor = async (s, e) => await s.GetNodeRuntimeOutputsAsync(S(e, "nodeName") ?? S(e, "node_name"))
                },
                new VisionToolDefinition
                {
                    Name = "tune_node_and_verify",
                    Description = "闭环调参：修改指定算子节点的参数并立即单步执行验证（等待执行真正完成），返回修改前后的参数对比与最新输出数据",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            nodeName = new { type = "string", description = "节点显示名称" },
                            parameters = new { type = "object", description = "待调参数键值对，例如 {\"Min\": 120, \"Max\": 255}" }
                        },
                        required = new[] { "nodeName", "parameters" }
                    },
                    Executor = async (s, e) => await s.TuneNodeAndVerifyAsync(S(e, "nodeName") ?? S(e, "node_name"), ParamDict(e))
                },
                new VisionToolDefinition
                {
                    Name = "open_node_dialog",
                    Description = "在界面中弹出指定算子的原生交互式 WPF 参数配置与交互式 ROI 窗口（需要用户手动操作）",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new { nodeName = new { type = "string", description = "节点显示名称" } },
                        required = new[] { "nodeName" }
                    },
                    Executor = async (s, e) => await s.OpenNodeDialogAsync(S(e, "nodeName") ?? S(e, "node_name"))
                },
                new VisionToolDefinition
                {
                    Name = "auto_layout_nodes",
                    Description = "对当前流程画布上的所有节点执行拓扑分层智能对齐，消除节点重叠与混乱布局",
                    ParametersSchema = new { type = "object", properties = new { } },
                    Executor = async (s, e) => await s.AutoLayoutNodesAsync()
                },
                new VisionToolDefinition
                {
                    Name = "get_image_histogram",
                    Description = "分析当前视口图像的灰度直方图分布、均值、方差、最小/最大灰度与动态对比度评估",
                    ParametersSchema = new { type = "object", properties = new { } },
                    Executor = async (s, e) => await s.GetImageHistogramAsync()
                },
                new VisionToolDefinition
                {
                    Name = "auto_compute_threshold",
                    Description = "基于图像直方图自适应计算最优二值化分割阈值（Otsu 最大类间方差法），推荐暗目标与亮目标区间",
                    ParametersSchema = new { type = "object", properties = new { } },
                    Executor = async (s, e) => await s.AutoComputeThresholdAsync()
                },
                new VisionToolDefinition
                {
                    Name = "apply_solution_template",
                    Description = "一键注入工业标准视觉方案模板（template_matching 模板匹配, dimension_measurement 尺寸测量, surface_defect 表面缺陷检测）",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new { templateType = new { type = "string", description = "模板类型：'template_matching', 'dimension_measurement', 或 'surface_defect'" } },
                        required = new[] { "templateType" }
                    },
                    Executor = async (s, e) => await s.ApplySolutionTemplateAsync(S(e, "templateType") ?? S(e, "template_type") ?? "surface_defect")
                },
                new VisionToolDefinition
                {
                    Name = "get_execution_records",
                    Description = "查询最近执行历史记录（时间、节点、状态、耗时、结果数据），用于分析流程瓶颈与失败历史",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            maxCount = new { type = "number", description = "返回最近 N 条，默认 20" }
                        }
                    },
                    Executor = async (s, e) => await s.GetExecutionRecordsAsync(D(e, "maxCount").HasValue ? (int)D(e, "maxCount").Value : 20)
                },
                new VisionToolDefinition
                {
                    Name = "get_node_input_bindings",
                    Description = "读取指定节点全部输入绑定的当前状态：每个输入槽绑了哪个上游节点的哪个输出，以及参数引用绑定（parameter:前缀）",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new { nodeName = new { type = "string", description = "节点显示名称" } },
                        required = new[] { "nodeName" }
                    },
                    Executor = async (s, e) => await s.GetNodeInputBindingsAsync(S(e, "nodeName") ?? S(e, "node_name"))
                },
                new VisionToolDefinition
                {
                    Name = "set_node_input_binding",
                    Description = "设置节点的一个输入绑定（三源：上游节点输出 / 变量 var:g:名称、var:w:名称 / 清除）。建议只绑定该节点的直接父节点或父级连线追溯的上级节点（界面绑定候选即按此限制），绑定无连线关系的节点会绕过流程数据流。targetInputKey 为输入槽键名或 parameter:参数名（参数引用：上游测量值直接驱动下游算子参数，如 parameter:MinGray）。sourceNode 为空表示清除该绑定。可先用 get_operator_schema 查询槽键名",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            nodeName = new { type = "string", description = "目标节点显示名称" },
                            targetInputKey = new { type = "string", description = "目标输入槽键名，如 Region、Contour、parameter:MinGray" },
                            sourceNode = new { type = "string", description = "上游节点名；留空/null 表示解除绑定" },
                            sourceOutputKey = new { type = "string", description = "上游输出槽键名；留空则自动匹配（如 Output、Region、MatchScores）" }
                        },
                        required = new[] { "nodeName", "targetInputKey" }
                    },
                    Executor = async (s, e) => await s.SetNodeInputBindingAsync(
                        S(e, "nodeName") ?? S(e, "node_name"),
                        S(e, "targetInputKey") ?? S(e, "target_input_key"),
                        S(e, "sourceNode") ?? S(e, "source_node"),
                        S(e, "sourceOutputKey") ?? S(e, "source_output_key"))
                },
                new VisionToolDefinition
                {
                    Name = "duplicate_node",
                    Description = "复制一个节点（含全部参数配置），新节点出现在原节点右下方，返回新节点名",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            nodeName = new { type = "string", description = "要复制的节点名" },
                            offsetX = new { type = "number", description = "新节点 X 偏移，默认 40" },
                            offsetY = new { type = "number", description = "新节点 Y 偏移，默认 40" }
                        },
                        required = new[] { "nodeName" }
                    },
                    Executor = async (s, e) => await s.DuplicateNodeAsync(
                        S(e, "nodeName") ?? S(e, "node_name"),
                        D(e, "offsetX") ?? 40, D(e, "offsetY") ?? 40)
                },
                new VisionToolDefinition
                {
                    Name = "clear_workflow",
                    Description = "清空当前流程的全部节点与连线（不可撤销！仅在用户明确要求重建流程时使用，清空前建议先 save_scheme 备份）",
                    ParametersSchema = new { type = "object", properties = new { } },
                    Executor = async (s, e) => await s.ClearWorkflowAsync()
                },
                new VisionToolDefinition
                {
                    Name = "remove_workflow",
                    Description = "删除指定名称的流程 Tab（至少保留一个流程）",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new { workflowName = new { type = "string", description = "要删除的流程名" } },
                        required = new[] { "workflowName" }
                    },
                    Executor = async (s, e) => await s.RemoveWorkflowAsync(S(e, "workflowName") ?? S(e, "workflow_name"))
                },
                new VisionToolDefinition
                {
                    Name = "rename_workflow",
                    Description = "重命名流程 Tab",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            oldName = new { type = "string", description = "当前流程名" },
                            newName = new { type = "string", description = "新流程名" }
                        },
                        required = new[] { "oldName", "newName" }
                    },
                    Executor = async (s, e) => await s.RenameWorkflowAsync(S(e, "oldName") ?? S(e, "old_name"), S(e, "newName") ?? S(e, "new_name"))
                },
                new VisionToolDefinition
                {
                    Name = "show_node_output",
                    Description = "将主界面视口切换为显示指定节点的输出图像（配合 capture_viewport_image 截图分析指定节点结果）",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new { nodeName = new { type = "string", description = "节点显示名称" } },
                        required = new[] { "nodeName" }
                    },
                    Executor = async (s, e) => await s.ShowNodeOutputAsync(S(e, "nodeName") ?? S(e, "node_name"))
                },
                new VisionToolDefinition
                {
                    Name = "export_hdevelop_script",
                    Description = "将当前活动工作流导出为 HDevelop (.hdev) 风格脚本文本（部分算子为参数注释形式，仅供参考）",
                    ParametersSchema = new { type = "object", properties = new { } },
                    Executor = async (s, e) => new { HDevelopScript = await s.ExportHDevelopScriptAsync() }
                },
                new VisionToolDefinition
                {
                    Name = "export_csharp_code",
                    Description = "将当前活动工作流导出为 C# / HalconDotNet 风格源码文本（骨架代码，部分算子为注释形式）",
                    ParametersSchema = new { type = "object", properties = new { } },
                    Executor = async (s, e) => new { CSharpCode = await s.ExportCSharpCodeAsync() }
                },
                new VisionToolDefinition
                {
                    Name = "set_workflow_parallel_execution",
                    Description = "设置流程的并行执行开关：开启后同层兄弟分支并发执行、层间按拓扑顺序等待（数据流调度），关闭恢复严格串行。适合多条独立检测分支提速；同层分支写同一驱动/变量时顺序不确定（有风险告警）。等价于系统设置中的执行设置开关",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            enabled = new { type = "boolean", description = "true=开启并行 / false=关闭" },
                            workflowName = new { type = "string", description = "可选：目标流程名；留空=当前活动流程" }
                        },
                        required = new[] { "enabled" }
                    },
                    Executor = async (s, e) => await s.SetWorkflowParallelExecutionAsync(B(e, "enabled"), S(e, "workflowName"))
                },
                new VisionToolDefinition
                {
                    Name = "new_scheme",
                    Description = "新建空白方案（未命名，含空的「主流程」）。注意：会丢弃当前未保存的修改（不弹保存确认框），需要保留请先 save_scheme",
                    ParametersSchema = new { type = "object", properties = new { } },
                    Executor = async (s, e) => await s.NewSchemeAsync()
                },
                new VisionToolDefinition
                {
                    Name = "list_recent_schemes",
                    Description = "列出最近打开的方案（MRU 最多 10 条，按时间倒序，已剔除不存在的文件）。配合 open_scheme 快速重开用户近期方案",
                    ParametersSchema = new { type = "object", properties = new { } },
                    Executor = async (s, e) => await s.ListRecentSchemesAsync()
                },
                new VisionToolDefinition
                {
                    Name = "get_variables",
                    Description = "列出全局与流程级变量。全局变量所有流程共享；流程级变量仅所属流程可见（解析优先于同名全局）。变量可被算子参数绑定（var:g:名称=全局 / var:w:名称=流程级）、逻辑算子与执行条件 $Key$ 引用，是流程间传参与工位状态数据的统一载体",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            scope = new { type = "string", description = "可选：global / workflow；留空=全部" },
                            keyword = new { type = "string", description = "可选：按变量名子串过滤" }
                        }
                    },
                    Executor = async (s, e) => await s.GetVariablesAsync(S(e, "scope"), S(e, "keyword"))
                },
                new VisionToolDefinition
                {
                    Name = "set_variable",
                    Description = "写入变量（不存在则创建）。常用于：跨流程传递检测结果、PLC 联动计数、给下游算子参数提供动态值（参数绑定 var:g:名称 / var:w:名称）",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string", description = "变量名（建议英文驼峰，如 ngCount / stationId）" },
                            value = new { type = "string", description = "变量值（统一按字符串存储，数值比较在引用时进行）" },
                            scope = new { type = "string", description = "global=全局（默认）/ workflow=当前流程级" },
                            workflowName = new { type = "string", description = "可选：流程级作用域的流程名；留空=当前活动流程" }
                        },
                        required = new[] { "name" }
                    },
                    Executor = async (s, e) => await s.SetVariableAsync(S(e, "name"), S(e, "value"), S(e, "scope") ?? "global", S(e, "workflowName"))
                },
                new VisionToolDefinition
                {
                    Name = "delete_variable",
                    Description = "删除变量（先 get_variables 确认作用域）。删除全局变量影响所有引用它的流程",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string", description = "变量名" },
                            scope = new { type = "string", description = "global（默认）/ workflow" },
                            workflowName = new { type = "string", description = "可选：流程级作用域的流程名" }
                        },
                        required = new[] { "name" }
                    },
                    Executor = async (s, e) => await s.DeleteVariableAsync(S(e, "name"), S(e, "scope") ?? "global", S(e, "workflowName"))
                },
                new VisionToolDefinition
                {
                    Name = "set_node_execution_condition",
                    Description = "设置节点执行条件：流程运行时条件为假则跳过该节点（跳过不报错）。条件语言：$输出键$ 插值（如 $NG$ == true、$Count$ > 5、$Score$ >= 0.8，键名来自上游输出或变量）、== != > >= < <=、前置 ! 取反、单独 $Key$ 非空即真。condition 传空字符串清除条件",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            nodeName = new { type = "string", description = "目标节点显示名称" },
                            condition = new { type = "string", description = "条件表达式；空字符串/缺省=清除条件" }
                        },
                        required = new[] { "nodeName" }
                    },
                    Executor = async (s, e) => await s.SetNodeExecutionConditionAsync(
                        S(e, "nodeName") ?? S(e, "node_name"),
                        e.HasValue && e.Value.TryGetProperty("condition", out var cond) && cond.ValueKind == JsonValueKind.String
                            ? cond.GetString()
                            : "")
                },
                new VisionToolDefinition
                {
                    Name = "undo",
                    Description = "撤销上一步画布操作（添加/删除/移动/连线/参数修改等）。AI 修改出错时可用它回退，连续调用可回退多步",
                    ParametersSchema = new { type = "object", properties = new { } },
                    Executor = async (s, e) => await s.UndoAsync()
                },
                new VisionToolDefinition
                {
                    Name = "redo",
                    Description = "重做被撤销的操作（与 undo 相反方向）",
                    ParametersSchema = new { type = "object", properties = new { } },
                    Executor = async (s, e) => await s.RedoAsync()
                },
                new VisionToolDefinition
                {
                    Name = "set_continuous_run",
                    Description = "启动/停止连续运行：循环执行当前流程直到停止（生产模式模拟，用于节拍测试与长时间稳定性验证）。停止也可用 stop_execution",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            enabled = new { type = "boolean", description = "true=启动连续运行 / false=停止" }
                        },
                        required = new[] { "enabled" }
                    },
                    Executor = async (s, e) => await s.SetContinuousRunAsync(B(e, "enabled"))
                },
                new VisionToolDefinition
                {
                    Name = "comm_read_value",
                    Description = "通过通讯驱动读取 PLC 地址当前值（西门子S7 / 三菱MC / 欧姆龙FINS 驱动）。用于核验 PLC 数据、联动调试与排查通讯问题；驱动需已在通讯驱动管理中创建并连接",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            driverName = new { type = "string", description = "通讯驱动名称（get_comm_drivers 可查）" },
                            address = new { type = "string", description = "PLC 地址（按驱动语法，如 DB1.DBW0 / D100 / CIO10）" },
                            type = new { type = "string", description = "值类型：bit / int16 / uint16 / int32 / uint32 / float（默认 int16）" }
                        },
                        required = new[] { "driverName", "address" }
                    },
                    Executor = async (s, e) => await s.CommReadValueAsync(S(e, "driverName") ?? S(e, "driver_name"), S(e, "address"), S(e, "type"))
                },
                new VisionToolDefinition
                {
                    Name = "comm_write_value",
                    Description = "写值到 PLC 地址（西门子S7 / 三菱MC / 欧姆龙FINS 驱动）。常用于回写检测结果（OK/NG 标志、测量值）与工位握手信号。写前建议先用 comm_read_value 确认地址有效",
                    ParametersSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            driverName = new { type = "string", description = "通讯驱动名称" },
                            address = new { type = "string", description = "PLC 地址（按驱动语法）" },
                            value = new { type = "string", description = "写入值：bit 用 true/false 或 1/0，数值直接写数字" },
                            type = new { type = "string", description = "值类型：bit / int16 / uint16 / int32 / uint32 / float（默认 int16）" }
                        },
                        required = new[] { "driverName", "address", "value" }
                    },
                    Executor = async (s, e) => await s.CommWriteValueAsync(
                        S(e, "driverName") ?? S(e, "driver_name"),
                        S(e, "address"),
                        e.HasValue && e.Value.TryGetProperty("value", out var v)
                            ? (object)(v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText())
                            : null,
                        S(e, "type"))
                }
            };

            return tools;
        }
    }
}
