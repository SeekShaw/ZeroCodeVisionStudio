using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroCodeVisionStudio.Shared;
using ZeroCodeVisionStudio.Shared.Communication;

namespace ZeroCodeVisionStudio.AI.Tools
{
    public class VisionStudioAutomationService : IVisionStudioAutomationService
    {
        private readonly IVisionStudioContext _context;

        public VisionStudioAutomationService(IVisionStudioContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<WorkflowStateDto> GetWorkflowStateAsync()
        {
            return await _context.RunOnUiAsync(() =>
            {
                var nodes = _context.GetActiveNodes();
                var connections = _context.GetActiveConnections();

                var state = new WorkflowStateDto
                {
                    SchemeTitle = _context.SchemeTitle,
                    ActiveWorkflowName = _context.ActiveWorkflowName,
                    ParallelExecution = _context.ActiveWorkflowParallelExecution,
                    AllWorkflows = _context.WorkflowNames.ToList(),
                    Nodes = nodes.Select(MapNodeToDto).ToList(),
                    Connections = connections.Select(c => new ConnectionDto
                    {
                        SourceNode = c.FromNode,
                        SourcePin = "Output",
                        TargetNode = c.ToNode,
                        TargetPin = "Input"
                    }).ToList()
                };

                return state;
            });
        }

        public async Task<List<OperatorCatalogItemDto>> GetOperatorCatalogAsync(string filter = null, bool includeDetail = false)
        {
            return await _context.RunOnUiAsync(() =>
            {
                IEnumerable<IHandleRegisterModel> registered = _context.GetRegisteredOperators();
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    var keyword = filter.Trim();
                    registered = registered.Where(r =>
                        (r.Key?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                        (r.HandleName?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                        (r.HandleType?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);
                }

                return registered.Select(r =>
                {
                    var item = new OperatorCatalogItemDto
                    {
                        Name = r.Key,
                        DisplayName = r.HandleName,
                        Category = r.HandleType,
                        // 紧凑模式不带说明/引脚（详情用 get_operator_schema 单查），控制输出体积避免被上下文裁剪刀截断。
                        Description = includeDetail ? TruncateText(r.HandleDescription, 80) : "",
                        InputPins = new List<string>(),
                        OutputPins = new List<string>()
                    };

                    if (includeDetail)
                    {
                        // 从算子实例读取真实声明槽位，避免误导 AI 选错引脚。
                        FillSlotsFromHandle(r.Handle, item);
                    }
                    return item;
                }).ToList();
            });
        }

        /// <summary>
        /// 创建算子实例用于读取元数据（槽位/参数 schema）：
        /// 插件算子构造函数需要 IDialogService（仅保存引用不调用），传 null 即可安全实例化；
        /// 无参构造的算子走第一条路径。
        /// </summary>
        private static IHandle TryCreateHandle(Type handleType)
        {
            try
            {
                if (Activator.CreateInstance(handleType) is IHandle parameterless)
                    return parameterless;
            }
            catch
            {
            }

            try
            {
                var ctor = handleType.GetConstructors().FirstOrDefault(c =>
                {
                    var parameters = c.GetParameters();
                    return parameters.Length == 1 && parameters[0].ParameterType == typeof(IDialogService);
                });
                return ctor?.Invoke(new object[] { null }) as IHandle;
            }
            catch
            {
                return null;
            }
        }

        private static string TruncateText(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? "";
            return text.Substring(0, max) + "…";
        }

        private static void FillSlotsFromHandle(Type handleType, OperatorCatalogItemDto item)
        {
            try
            {
                if (handleType == null) return;
                if (!typeof(IHandle).IsAssignableFrom(handleType)) return;
                var handle = TryCreateHandle(handleType);
                if (handle == null) return;

                try
                {
                    if (handle is IHandleDataSlots slots)
                    {
                        if (slots.InputSlots?.Count > 0)
                            item.InputPins = slots.InputSlots.Select(s => s.Key).ToList();
                        if (slots.OutputSlots?.Count > 0)
                            item.OutputPins = slots.OutputSlots.Select(s => s.Key).ToList();
                    }
                }
                finally
                {
                    handle.Dispose();
                }
            }
            catch
            {
                // 读取槽位失败时保留默认引脚。
            }
        }

        public async Task<OperationResultDto> AddNodeAsync(string operatorName, double? x = null, double? y = null)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var node = _context.AddNode(operatorName, x, y);
                if (node == null)
                {
                    return OperationResultDto.Fail($"未找到算子或创建节点失败: {operatorName}");
                }

                return OperationResultDto.Ok($"成功添加算子节点: {node.DisplayName}", MapNodeToDto(node));
            });
        }

        public async Task<OperationResultDto> RemoveNodeAsync(string nodeName)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var success = _context.RemoveNode(nodeName);
                return success
                    ? OperationResultDto.Ok($"已删除节点: {nodeName}")
                    : OperationResultDto.Fail($"未找到节点或删除失败: {nodeName}");
            });
        }

        public async Task<OperationResultDto> RenameNodeAsync(string nodeName, string newName)
        {
            return await _context.RunOnUiAsync(() =>
            {
                if (string.IsNullOrWhiteSpace(newName))
                {
                    return OperationResultDto.Fail("新名称不能为空");
                }

                var success = _context.RenameNode(nodeName, newName);
                return success
                    ? OperationResultDto.Ok($"节点已重命名: {nodeName} -> {newName}")
                    : OperationResultDto.Fail($"重命名失败（节点不存在或新名称已被占用）: {nodeName} -> {newName}");
            });
        }

        public async Task<OperationResultDto> MoveNodeAsync(string nodeName, double x, double y)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var success = _context.MoveNode(nodeName, x, y);
                return success
                    ? OperationResultDto.Ok($"节点已移动到 ({x:F0}, {y:F0}): {nodeName}")
                    : OperationResultDto.Fail($"未找到节点: {nodeName}");
            });
        }

        public async Task<OperationResultDto> SelectNodeAsync(string nodeName)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var node = _context.GetActiveNodes().FirstOrDefault(n =>
                    string.Equals(n.DisplayName, nodeName, StringComparison.OrdinalIgnoreCase));
                if (node == null)
                {
                    return OperationResultDto.Fail($"未找到节点: {nodeName}");
                }

                _context.SelectNode(nodeName);
                return OperationResultDto.Ok($"已在画布中选中并显示节点输出: {node.DisplayName}");
            });
        }

        public async Task<OperationResultDto> ConnectNodesAsync(string fromNode, string fromPin, string toNode, string toPin)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var hasPins = !string.IsNullOrWhiteSpace(fromPin) && fromPin != "Output" ||
                              !string.IsNullOrWhiteSpace(toPin) && toPin != "Input";

                var success = _context.ConnectNodes(fromNode, toNode,
                    string.IsNullOrWhiteSpace(fromPin) || fromPin == "Output" ? null : fromPin,
                    string.IsNullOrWhiteSpace(toPin) || toPin == "Input" ? null : toPin);
                return success
                    ? OperationResultDto.Ok($"已建立连线: {fromNode}{(hasPins ? $"({fromPin})" : "")} -> {toNode}{(hasPins ? $"({toPin})" : "")}")
                    : OperationResultDto.Fail($"建立连线失败，请检查节点是否存在或是否已存在连线: {fromNode} -> {toNode}");
            });
        }

        public async Task<OperationResultDto> DisconnectNodesAsync(string fromNode, string toNode)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var success = _context.DisconnectNodes(fromNode, toNode);
                return success
                    ? OperationResultDto.Ok($"已断开连线: {fromNode} -> {toNode}")
                    : OperationResultDto.Fail($"断开连线失败，未找到该连线: {fromNode} -> {toNode}");
            });
        }

        public async Task<List<ParameterDto>> GetNodeParametersAsync(string nodeName)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var node = _context.GetActiveNodes().FirstOrDefault(n =>
                    string.Equals(n.DisplayName, nodeName, StringComparison.OrdinalIgnoreCase));
                if (node == null) return new List<ParameterDto>();

                return ExtractParameters(node);
            });
        }

        public async Task<OperationResultDto> SetNodeParametersAsync(string nodeName, Dictionary<string, object> parameters)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var node = _context.GetActiveNodes().FirstOrDefault(n =>
                    string.Equals(n.DisplayName, nodeName, StringComparison.OrdinalIgnoreCase));

                if (node?.Handle?.HandleParameter == null)
                {
                    return OperationResultDto.Fail($"未找到节点或该节点没有可配置参数: {nodeName}");
                }

                var paramObj = node.Handle.HandleParameter;
                var paramType = paramObj.GetType();
                var updated = new List<string>();
                var errors = new List<string>();

                foreach (var kv in parameters)
                {
                    var prop = paramType.GetProperty(kv.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (prop == null || !prop.CanWrite)
                    {
                        errors.Add($"未知或只读参数: {kv.Key}");
                        continue;
                    }

                    if (typeof(HalconDotNet.HObject).IsAssignableFrom(prop.PropertyType))
                    {
                        errors.Add($"参数 {kv.Key} 为图像/区域资源，不允许通过 AI 修改");
                        continue;
                    }

                    try
                    {
                        var convertedValue = ConvertValue(kv.Value, prop.PropertyType);
                        prop.SetValue(paramObj, convertedValue);
                        updated.Add($"{prop.Name}={convertedValue}");
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"设置参数 {kv.Key} 失败: {ex.Message}");
                    }
                }

                if (errors.Count > 0 && updated.Count == 0)
                {
                    return OperationResultDto.Fail(string.Join("; ", errors));
                }

                var msg = $"已更新参数: {string.Join(", ", updated)}";
                if (errors.Count > 0)
                {
                    msg += $" (警告: {string.Join("; ", errors)})";
                }

                return OperationResultDto.Ok(msg);
            });
        }

        public async Task<OperationResultDto> ExecuteWorkflowAsync(string mode = "all")
        {
            if (string.Equals(mode, "all", StringComparison.OrdinalIgnoreCase))
            {
                // 等待执行真正完成（而非触发即返回），并汇总执行结果。
                var success = await _context.ExecuteWorkflowAsync();
                var summary = await BuildExecutionSummaryAsync();
                return success
                    ? OperationResultDto.Ok("工作流执行完成", summary)
                    : OperationResultDto.Fail("工作流执行失败或无法启动", summary);
            }
            else
            {
                var success = await _context.ExecuteSingleNodeAsync(mode);
                var summary = await BuildExecutionSummaryAsync();
                return success
                    ? OperationResultDto.Ok($"单节点 [{mode}] 执行完成", summary)
                    : OperationResultDto.Fail($"单节点 [{mode}] 执行失败", summary);
            }
        }

        public async Task<OperationResultDto> StopExecutionAsync()
        {
            return await _context.RunOnUiAsync(() =>
            {
                var stopped = _context.StopExecution();
                return stopped
                    ? OperationResultDto.Ok("已请求停止当前执行")
                    : OperationResultDto.Fail("当前没有正在执行的流程");
            });
        }

        private async Task<object> BuildExecutionSummaryAsync()
        {
            var records = await GetExecutionRecordsAsync(50);
            var recent = records.TakeLast(20).ToList();
            return new
            {
                RecentRecords = recent,
                // StatusText 的实际取值：Running→"执行中"，Success→"已执行"，Failed→"失败"
                SuccessCount = recent.Count(r => r.Status == "已执行" || r.Status == "Success"),
                FailedCount = recent.Count(r => r.Status == "失败" || r.Status == "Failed")
            };
        }

        public async Task<List<ExecutionRecordDto>> GetExecutionRecordsAsync(int maxCount = 20)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var records = _context.GetExecutionRecords();
                return records.TakeLast(maxCount).Select(r => new ExecutionRecordDto
                {
                    Sequence = r.Seq,
                    Time = r.Time.ToString("HH:mm:ss.fff"),
                    Module = r.Module,
                    Status = r.Status,
                    ElapsedMs = r.Elapsed.TotalMilliseconds,
                    ResultData = r.Result
                }).ToList();
            });
        }

        public async Task<List<InputBindingDto>> GetNodeInputBindingsAsync(string nodeName)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var bindings = _context.GetNodeInputBindings(nodeName) ?? new List<WorkflowInputBinding>();
                return bindings.Select(b => new InputBindingDto
                {
                    TargetInputKey = b.TargetInputKey,
                    DisplayName = b.TargetInputName,
                    IsParameterBinding = b.IsParameterBinding,
                    TargetParameterName = b.TargetParameterName,
                    SourceNode = b.SourceNode?.DisplayName,
                    SourceOutputKey = b.SourceOutputKey,
                    TypeName = b.TargetInputType?.Name
                }).ToList();
            });
        }

        public async Task<OperationResultDto> SetNodeInputBindingAsync(string nodeName, string targetInputKey, string sourceNode, string sourceOutputKey)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var ok = _context.SetNodeInputBinding(nodeName, targetInputKey, sourceNode, sourceOutputKey);
                return ok
                    ? OperationResultDto.Ok($"已将 [{nodeName}] 的输入 {targetInputKey} 绑定到 {sourceNode}:{sourceOutputKey ?? "(自动)"}")
                    : OperationResultDto.Fail($"绑定失败：节点/输入槽/上游节点不存在，或类型不兼容（{targetInputKey}）");
            });
        }

        public async Task<OperationResultDto> DuplicateNodeAsync(string nodeName, double offsetX = 40, double offsetY = 40)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var node = _context.DuplicateNode(nodeName, offsetX, offsetY);
                return node != null
                    ? OperationResultDto.Ok($"已复制节点 [{nodeName}] → [{node.DisplayName}]（含全部参数）", new { NewNodeName = node.DisplayName })
                    : OperationResultDto.Fail($"未找到节点: {nodeName}");
            });
        }

        public async Task<OperationResultDto> ClearWorkflowAsync()
        {
            return await _context.RunOnUiAsync(() =>
            {
                var ok = _context.ClearWorkflow();
                return ok
                    ? OperationResultDto.Ok("已清空当前流程的全部节点与连线（不可撤销）")
                    : OperationResultDto.Fail("当前流程已经为空");
            });
        }

        public async Task<OperationResultDto> RemoveWorkflowAsync(string workflowName)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var ok = _context.RemoveWorkflowByName(workflowName);
                return ok
                    ? OperationResultDto.Ok($"已删除流程: {workflowName}")
                    : OperationResultDto.Fail($"删除失败（流程不存在或为最后一个流程）: {workflowName}");
            });
        }

        public async Task<OperationResultDto> RenameWorkflowAsync(string oldName, string newName)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var ok = _context.RenameWorkflow(oldName, newName);
                return ok
                    ? OperationResultDto.Ok($"流程已重命名: {oldName} -> {newName}")
                    : OperationResultDto.Fail($"重命名失败（流程不存在或新名称被占用）: {oldName} -> {newName}");
            });
        }

        public async Task<OperationResultDto> ShowNodeOutputAsync(string nodeName)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var ok = _context.ShowNodeOutput(nodeName);
                return ok
                    ? OperationResultDto.Ok($"主视口已切换为显示节点 [{nodeName}] 的输出，可配合 capture_viewport_image 截图分析")
                    : OperationResultDto.Fail($"未找到节点: {nodeName}");
            });
        }

        public async Task<FaultSnapshotDto> GetFaultSnapshotAsync(string nodeName = null)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var nodes = _context.GetActiveNodes();
                IHandleRunModel targetNode = null;

                if (!string.IsNullOrWhiteSpace(nodeName))
                {
                    targetNode = nodes.FirstOrDefault(n =>
                        string.Equals(n.DisplayName, nodeName, StringComparison.OrdinalIgnoreCase));
                }

                if (targetNode == null)
                {
                    // 自动定位首个失败节点
                    targetNode = nodes.FirstOrDefault(n => n.Status == HandleStatus.Failed);
                }

                if (targetNode == null)
                {
                    // 若无明确失败节点，选最后一个有错误消息或执行过的节点
                    targetNode = nodes.LastOrDefault(n => !string.IsNullOrWhiteSpace(n.StatusMessage))
                                 ?? nodes.LastOrDefault();
                }

                if (targetNode == null)
                {
                    return new FaultSnapshotDto
                    {
                        NodeName = "无节点",
                        ErrorMessage = "当前流程无节点或尚未执行"
                    };
                }

                var connections = _context.GetActiveConnections();
                var upstreamConns = connections
                    .Where(c => string.Equals(c.ToNode, targetNode.DisplayName, StringComparison.OrdinalIgnoreCase))
                    .Select(c => new ConnectionDto
                    {
                        SourceNode = c.FromNode,
                        SourcePin = "Output",
                        TargetNode = c.ToNode,
                        TargetPin = "Input"
                    }).ToList();

                var snapshot = new FaultSnapshotDto
                {
                    NodeName = targetNode.DisplayName,
                    OperatorType = targetNode.RegisterModel?.HandleName ?? targetNode.Handle?.GetType().Name,
                    Status = targetNode.Status.ToString(),
                    ErrorMessage = targetNode.StatusMessage,
                    ElapsedMs = targetNode.LastElapsed.TotalMilliseconds,
                    UpstreamConnections = upstreamConns,
                    UpstreamNodes = upstreamConns.Select(c => c.SourceNode).Distinct().ToList()
                };

                // 参数提取
                var paramList = ExtractParameters(targetNode);
                foreach (var p in paramList)
                {
                    snapshot.Parameters[p.Name] = p.Value;
                }

                return snapshot;
            });
        }

        public async Task<string> CaptureViewportImageAsync(string targetPath = null)
        {
            return await _context.CaptureViewportImageAsync(targetPath);
        }

        public async Task<OperationResultDto> CreateWorkflowAsync(string workflowName)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var ok = _context.CreateWorkflow(workflowName);
                return ok
                    ? OperationResultDto.Ok($"成功创建工作流: {workflowName}")
                    : OperationResultDto.Fail($"创建工作流失败: {workflowName}");
            });
        }

        public async Task<OperationResultDto> SwitchWorkflowAsync(string workflowName)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var ok = _context.SwitchWorkflow(workflowName);
                return ok
                    ? OperationResultDto.Ok($"已切换到工作流: {workflowName}")
                    : OperationResultDto.Fail($"未找到工作流: {workflowName}");
            });
        }

        public async Task<OperationResultDto> SaveSchemeAsync(string path = null)
        {
            try
            {
                var savedPath = await _context.SaveSchemeAsync(path);
                return string.IsNullOrWhiteSpace(savedPath)
                    ? OperationResultDto.Fail("保存方案失败")
                    : OperationResultDto.Ok($"方案已保存: {savedPath}", new { Path = savedPath });
            }
            catch (Exception ex)
            {
                return OperationResultDto.Fail($"保存方案失败: {ex.Message}");
            }
        }

        public async Task<OperationResultDto> OpenSchemeAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return OperationResultDto.Fail("必须提供方案文件路径（可先调用 list_schemes 查询）");
            }

            try
            {
                var report = await _context.OpenSchemeAsync(path);
                return OperationResultDto.Ok($"方案已加载: {path}", new { LoadReport = report });
            }
            catch (Exception ex)
            {
                return OperationResultDto.Fail($"打开方案失败: {ex.Message}");
            }
        }

        public async Task<List<SchemeFileInfoDto>> ListSchemesAsync()
        {
            return await _context.RunOnUiAsync(() => _context.ListSchemes()
                .Select(p =>
                {
                    var info = new FileInfo(p);
                    return new SchemeFileInfoDto
                    {
                        FileName = Path.GetFileNameWithoutExtension(p),
                        Path = p,
                        ModifiedTime = info.Exists ? info.LastWriteTime : DateTime.MinValue,
                        SizeBytes = info.Exists ? info.Length : 0
                    };
                })
                .OrderByDescending(x => x.ModifiedTime)
                .ToList());
        }

        private static NodeDto MapNodeToDto(IHandleRunModel node)
        {
            var dto = new NodeDto
            {
                Id = node.DisplayName,
                Name = node.DisplayName,
                OperatorType = node.RegisterModel?.HandleName ?? node.Handle?.GetType().Name,
                Category = node.RegisterModel?.HandleType,
                Status = node.Status.ToString(),
                StatusMessage = node.StatusMessage,
                LastElapsedMs = node.LastElapsed.TotalMilliseconds,
                X = node.X,
                Y = node.Y,
                ExecutionCondition = string.IsNullOrWhiteSpace(node.ExecutionCondition) ? null : node.ExecutionCondition,
                InputPins = new List<string> { "Image" },
                OutputPins = new List<string> { "Output" },
                Parameters = ExtractParameters(node)
            };

            if (node.Handle is IHandleDataSlots slots)
            {
                if (slots.InputSlots?.Count > 0)
                    dto.InputPins = slots.InputSlots.Select(s => s.Key).ToList();
                if (slots.OutputSlots?.Count > 0)
                    dto.OutputPins = slots.OutputSlots.Select(s => s.Key).ToList();
            }

            return dto;
        }

        private static List<ParameterDto> ExtractParameters(IHandleRunModel node)
        {
            var result = new List<ParameterDto>();
            if (node?.Handle?.HandleParameter == null) return result;

            var param = node.Handle.HandleParameter;
            var props = param.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in props)
            {
                if (!prop.CanRead) continue;
                if (prop.Name == "Item" || prop.PropertyType.Name.StartsWith("HObject")) continue;

                var descAttr = prop.GetCustomAttribute<DescriptionAttribute>();
                try
                {
                    var val = prop.GetValue(param);
                    result.Add(new ParameterDto
                    {
                        Name = prop.Name,
                        Value = val,
                        Type = prop.PropertyType.Name,
                        Description = descAttr?.Description ?? prop.Name
                    });
                }
                catch
                {
                }
            }

            return result;
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null) return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            if (value is JsonElement element)
            {
                // 统一走共享转换器（支持数组/字典/枚举等），失败再回落到原分支
                try
                {
                    return Shared.Helper.JsonValueConverter.Convert(element, targetType);
                }
                catch
                {
                }

                switch (element.ValueKind)
                {
                    case JsonValueKind.Number:
                        if (targetType == typeof(int)) return element.GetInt32();
                        if (targetType == typeof(double)) return element.GetDouble();
                        if (targetType == typeof(float)) return element.GetSingle();
                        if (targetType == typeof(long)) return element.GetInt64();
                        break;
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        if (targetType == typeof(bool)) return element.GetBoolean();
                        break;
                    case JsonValueKind.String:
                        var str = element.GetString();
                        if (targetType == typeof(string)) return str;
                        if (targetType.IsEnum) return Enum.Parse(targetType, str, true);
                        if (targetType == typeof(double)) return double.Parse(str, CultureInfo.InvariantCulture);
                        if (targetType == typeof(int)) return int.Parse(str, CultureInfo.InvariantCulture);
                        break;
                }
            }

            if (targetType.IsInstanceOfType(value)) return value;

            if (targetType.IsEnum && value is string enumStr)
            {
                return Enum.Parse(targetType, enumStr, true);
            }

            return Convert.ChangeType(value, Nullable.GetUnderlyingType(targetType) ?? targetType, CultureInfo.InvariantCulture);
        }

        public async Task<OperatorSchemaDto> GetOperatorSchemaAsync(string operatorKey)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var registered = _context.GetRegisteredOperators();
                var reg = registered.FirstOrDefault(r =>
                    string.Equals(r.Key, operatorKey, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r.HandleName, operatorKey, StringComparison.OrdinalIgnoreCase));

                var activeNode = _context.GetActiveNodes().FirstOrDefault(n =>
                    string.Equals(n.DisplayName, operatorKey, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(n.RegisterModel?.Key, operatorKey, StringComparison.OrdinalIgnoreCase));

                var schema = new OperatorSchemaDto
                {
                    Key = reg?.Key ?? activeNode?.RegisterModel?.Key ?? operatorKey,
                    DisplayName = reg?.HandleName ?? activeNode?.DisplayName ?? operatorKey,
                    Category = reg?.HandleType ?? activeNode?.RegisterModel?.HandleType ?? "General",
                    Description = reg?.HandleDescription ?? activeNode?.RegisterModel?.HandleDescription ?? ""
                };

                IHandle handle = activeNode?.Handle;
                bool needDispose = false;
                if (handle == null && reg?.Handle != null)
                {
                    handle = TryCreateHandle(reg.Handle);
                    needDispose = handle != null;
                }

                if (handle != null)
                {
                    try
                    {
                        if (handle is IHandleDataSlots slots)
                        {
                            if (slots.InputSlots != null)
                            {
                                schema.InputSlots = slots.InputSlots.Select(s => new SlotDto
                                {
                                    Key = s.Key,
                                    DisplayName = s.DisplayName,
                                    TypeName = s.ValueType?.Name ?? "Object"
                                }).ToList();
                            }
                            if (slots.OutputSlots != null)
                            {
                                schema.OutputSlots = slots.OutputSlots.Select(s => new SlotDto
                                {
                                    Key = s.Key,
                                    DisplayName = s.DisplayName,
                                    TypeName = s.ValueType?.Name ?? "Object"
                                }).ToList();
                            }
                        }

                        var param = handle.HandleParameter;
                        if (param != null)
                        {
                            var props = param.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                            foreach (var p in props)
                            {
                                if (!p.CanRead) continue;
                                if (p.Name == "Item" || p.PropertyType.Name.StartsWith("HObject")) continue;

                                var descAttr = p.GetCustomAttribute<DescriptionAttribute>();
                                var dispAttr = p.GetCustomAttribute<DisplayNameAttribute>();

                                object defaultVal = null;
                                try { defaultVal = p.GetValue(param); } catch { }

                                schema.Parameters.Add(new ParameterSchemaDto
                                {
                                    Name = p.Name,
                                    DisplayName = dispAttr?.DisplayName ?? p.Name,
                                    TypeName = p.PropertyType.Name,
                                    DefaultValue = defaultVal,
                                    Description = descAttr?.Description ?? p.Name
                                });
                            }
                        }
                    }
                    finally
                    {
                        if (needDispose) handle.Dispose();
                    }
                }

                return schema;
            });
        }

        public async Task<Dictionary<string, object>> GetNodeRuntimeOutputsAsync(string nodeName)
        {
            return await _context.RunOnUiAsync(() => _context.GetNodeRuntimeOutputs(nodeName));
        }

        public async Task<TuneAndVerifyResultDto> TuneNodeAndVerifyAsync(string nodeName, Dictionary<string, object> parameters)
        {
            var result = new TuneAndVerifyResultDto
            {
                NodeName = nodeName
            };

            var currentParams = await GetNodeParametersAsync(nodeName);
            foreach (var p in currentParams)
            {
                result.PreviousParameters[p.Name] = p.Value;
            }

            var updateRes = await SetNodeParametersAsync(nodeName, parameters);
            if (!updateRes.Success)
            {
                result.Success = false;
                result.Message = $"更新参数失败: {updateRes.Message}";
                return result;
            }

            foreach (var kvp in parameters)
            {
                result.UpdatedParameters[kvp.Key] = kvp.Value;
            }

            // 上下文层现在会等待单步执行真正完成后才返回，确保读到的是最新输出。
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool execSuccess = await _context.ExecuteSingleNodeAsync(nodeName);
            sw.Stop();
            result.ElapsedMs = sw.Elapsed.TotalMilliseconds;

            result.RuntimeOutputs = await _context.RunOnUiAsync(() => _context.GetNodeRuntimeOutputs(nodeName));
            result.Success = execSuccess;
            result.Message = execSuccess
                ? $"参数更新成功，单步执行成功 (耗时: {result.ElapsedMs:F2} ms)"
                : "参数更新成功，但单步执行报错，请查看 RuntimeOutputs 中的错误详情";

            return result;
        }

        public async Task<OperationResultDto> OpenNodeDialogAsync(string nodeName)
        {
            return await _context.RunOnUiAsync(() =>
            {
                bool ok = _context.OpenNodeDialog(nodeName);
                return ok
                    ? OperationResultDto.Ok($"已打开节点 [{nodeName}] 的配置窗口。")
                    : OperationResultDto.Fail($"未找到节点 [{nodeName}] 或该算子不支持交互式窗口。");
            });
        }

        public async Task<OperationResultDto> AutoLayoutNodesAsync()
        {
            return await _context.RunOnUiAsync(() =>
            {
                bool ok = _context.AutoLayoutNodes();
                return ok
                    ? OperationResultDto.Ok("流程画布所有节点已按拓扑层次自动排版整齐。")
                    : OperationResultDto.Fail("排版失败，当前画布没有节点。");
            });
        }

        public async Task<ImageHistogramDto> GetImageHistogramAsync(string nodeName = null)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var stats = _context.GetImageStats(nodeName);
                var dto = new ImageHistogramDto
                {
                    Mean = Math.Round(stats.Mean, 2),
                    Deviation = Math.Round(stats.Dev, 2),
                    MinGray = Math.Round(stats.Min, 2),
                    MaxGray = Math.Round(stats.Max, 2),
                    DynamicRange = Math.Round(stats.Max - stats.Min, 2),
                    HistogramPercentiles = stats.Percentiles
                };

                if (dto.DynamicRange < 50)
                {
                    dto.Evaluation = "图像灰度动态范围偏低（对比度不足），建议先进行增强对比度或直方图均衡化预处理。";
                }
                else if (dto.Mean < 50)
                {
                    dto.Evaluation = "图像整体偏暗（欠曝），请注意调整曝光时间或增益。";
                }
                else if (dto.Mean > 200)
                {
                    dto.Evaluation = "图像整体偏亮（可能存在过曝区域），请注意避免工件高光反射影响边缘检测。";
                }
                else
                {
                    dto.Evaluation = "图像对比度与灰度分布良好，适合二值化分割与模板匹配特征提取。";
                }

                return dto;
            });
        }

        public async Task<AdaptiveThresholdResultDto> AutoComputeThresholdAsync(string nodeName = null)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var stats = _context.GetImageStats(nodeName);
                var histo = stats.Percentiles;

                double optimalThreshold = 128;
                if (histo != null && histo.Count >= 256)
                {
                    long total = 0;
                    for (int i = 0; i < 256; i++) total += histo[i];

                    if (total > 0)
                    {
                        double sum = 0;
                        for (int i = 0; i < 256; i++) sum += (double)i * histo[i];

                        double sumB = 0;
                        long wB = 0;
                        double varMax = 0;
                        int bestT = 128;

                        for (int t = 0; t < 256; t++)
                        {
                            wB += histo[t];
                            if (wB == 0) continue;
                            long wF = total - wB;
                            if (wF == 0) break;

                            sumB += (double)t * histo[t];
                            double mB = sumB / wB;
                            double mF = (sum - sumB) / wF;

                            double varBetween = (double)wB * wF * (mB - mF) * (mB - mF);
                            if (varBetween > varMax)
                            {
                                varMax = varBetween;
                                bestT = t;
                            }
                        }
                        optimalThreshold = bestT;
                    }
                }

                return new AdaptiveThresholdResultDto
                {
                    OptimalMin = optimalThreshold,
                    OptimalMax = 255,
                    Algorithm = "Otsu (最大类间方差法)",
                    Rationale = $"基于当前画面直方图分布计算，最优分割分割点为 {optimalThreshold}。若目标工件为暗目标，建议区间设为 [0, {optimalThreshold}]；若为亮目标，建议设为 [{optimalThreshold}, 255]。"
                };
            });
        }

        public async Task<OperationResultDto> ApplySolutionTemplateAsync(string templateType)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var registered = _context.GetRegisteredOperators();
                string typeLower = templateType?.ToLowerInvariant() ?? "";

                if (typeLower.Contains("defect") || typeLower.Contains("surface") || typeLower.Contains("缺陷"))
                {
                    var n1 = _context.AddNode("ImageInput") ?? _context.AddNode(registered.FirstOrDefault(r => r.Key.Contains("Image") || r.HandleName.Contains("图像"))?.Key ?? "ImageInput");
                    var n2 = _context.AddNode("Preprocessing") ?? _context.AddNode(registered.FirstOrDefault(r => r.Key.Contains("Preprocess") || r.HandleName.Contains("预处理"))?.Key ?? "Preprocessing");
                    var n3 = _context.AddNode("Threshold") ?? _context.AddNode(registered.FirstOrDefault(r => r.Key.Contains("Threshold") || r.HandleName.Contains("阈值"))?.Key ?? "Threshold");
                    var n4 = _context.AddNode("RegionProcessing") ?? _context.AddNode(registered.FirstOrDefault(r => r.Key.Contains("Region") || r.HandleName.Contains("区域"))?.Key ?? "RegionProcessing");
                    var n5 = _context.AddNode("ResultOutput") ?? _context.AddNode(registered.FirstOrDefault(r => r.Key.Contains("Output") || r.HandleName.Contains("结果"))?.Key ?? "ResultOutput");

                    if (n1 != null && n2 != null) _context.ConnectNodes(n1.DisplayName, n2.DisplayName);
                    if (n2 != null && n3 != null) _context.ConnectNodes(n2.DisplayName, n3.DisplayName);
                    if (n3 != null && n4 != null) _context.ConnectNodes(n3.DisplayName, n4.DisplayName);
                    if (n4 != null && n5 != null) _context.ConnectNodes(n4.DisplayName, n5.DisplayName);

                    _context.AutoLayoutNodes();
                    return OperationResultDto.Ok("已成功构建【工业表面缺陷检测方案】：包含图像采集、预处理、阈值分割、区域形态学特征筛选与结果输出。");
                }
                else if (typeLower.Contains("measure") || typeLower.Contains("dimension") || typeLower.Contains("尺寸") || typeLower.Contains("测量"))
                {
                    var n1 = _context.AddNode("ImageInput") ?? _context.AddNode(registered.FirstOrDefault(r => r.Key.Contains("Image") || r.HandleName.Contains("图像"))?.Key ?? "ImageInput");
                    var n2 = _context.AddNode("Measurement") ?? _context.AddNode(registered.FirstOrDefault(r => r.Key.Contains("Measure") || r.HandleName.Contains("测量"))?.Key ?? "Measurement");
                    var n3 = _context.AddNode("Geometry") ?? _context.AddNode(registered.FirstOrDefault(r => r.Key.Contains("Geometry") || r.HandleName.Contains("几何"))?.Key ?? "Geometry");
                    var n4 = _context.AddNode("ResultOutput") ?? _context.AddNode(registered.FirstOrDefault(r => r.Key.Contains("Output") || r.HandleName.Contains("结果"))?.Key ?? "ResultOutput");

                    if (n1 != null && n2 != null) _context.ConnectNodes(n1.DisplayName, n2.DisplayName);
                    if (n2 != null && n3 != null) _context.ConnectNodes(n2.DisplayName, n3.DisplayName);
                    if (n3 != null && n4 != null) _context.ConnectNodes(n3.DisplayName, n4.DisplayName);

                    _context.AutoLayoutNodes();
                    return OperationResultDto.Ok("已成功构建【高精度几何与尺寸测量方案】：包含图像采集、卡尺/边缘测量、几何拟合计算与输出。");
                }
                else
                {
                    var n1 = _context.AddNode("ImageInput") ?? _context.AddNode(registered.FirstOrDefault(r => r.Key.Contains("Image") || r.HandleName.Contains("图像"))?.Key ?? "ImageInput");
                    var n2 = _context.AddNode("ShapeMatching") ?? _context.AddNode(registered.FirstOrDefault(r => r.Key.Contains("Match") || r.HandleName.Contains("匹配"))?.Key ?? "ShapeMatching");
                    var n3 = _context.AddNode("ResultOutput") ?? _context.AddNode(registered.FirstOrDefault(r => r.Key.Contains("Output") || r.HandleName.Contains("结果"))?.Key ?? "ResultOutput");

                    if (n1 != null && n2 != null) _context.ConnectNodes(n1.DisplayName, n2.DisplayName);
                    if (n2 != null && n3 != null) _context.ConnectNodes(n2.DisplayName, n3.DisplayName);

                    _context.AutoLayoutNodes();
                    return OperationResultDto.Ok("已成功构建【工件模板匹配与定位方案】：包含图像采集、形状匹配定位及输出。");
                }
            });
        }

        public async Task<string> ExportHDevelopScriptAsync()
        {
            return await _context.RunOnUiAsync(() =>
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("* ========================================================");
                sb.AppendLine("* ZeroCodeVisionStudio 自动导出的 HDevelop 脚本");
                sb.AppendLine($"* 方案名称: {_context.SchemeTitle}  流程: {_context.ActiveWorkflowName}");
                sb.AppendLine($"* 导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine("* ========================================================");
                sb.AppendLine();
                sb.AppendLine("dev_update_off ()");
                sb.AppendLine("dev_close_window ()");
                sb.AppendLine("dev_open_window (0, 0, 800, 600, 'black', WindowHandle)");
                sb.AppendLine();

                var nodes = _context.GetActiveNodes();
                int idx = 1;
                foreach (var node in nodes)
                {
                    sb.AppendLine($"* --- Step {idx++}: [{node.DisplayName}] ({node.RegisterModel?.HandleName ?? "算子"}) ---");
                    var pList = ExtractParameters(node);
                    var pDict = pList.ToDictionary(p => p.Name, p => p.Value);

                    string type = node.RegisterModel?.Key?.ToLowerInvariant() ?? "";
                    if (type.Contains("image") || type.Contains("input"))
                    {
                        sb.AppendLine("read_image (Image, 'printer_chip/printer_chip_01')");
                        sb.AppendLine("dev_display (Image)");
                    }
                    else if (type.Contains("threshold"))
                    {
                        double minVal = pDict.TryGetValue("Min", out var min) ? Convert.ToDouble(min) : 100;
                        double maxVal = pDict.TryGetValue("Max", out var max) ? Convert.ToDouble(max) : 255;
                        sb.AppendLine($"threshold (Image, SelectedRegions, {minVal}, {maxVal})");
                        sb.AppendLine("dev_set_color ('green')");
                        sb.AppendLine("dev_display (SelectedRegions)");
                    }
                    else if (type.Contains("match"))
                    {
                        double minScore = pDict.TryGetValue("MinScore", out var ms) ? Convert.ToDouble(ms) : 0.7;
                        sb.AppendLine($"find_shape_model (Image, ModelID, 0, rad(360), {minScore}, 1, 0.5, 'least_squares', 0, 0.9, Row, Column, Angle, Score)");
                    }
                    else if (type.Contains("region") || type.Contains("denoise"))
                    {
                        sb.AppendLine("connection (SelectedRegions, ConnectedRegions)");
                        sb.AppendLine("select_shape (ConnectedRegions, SelectedRegions, 'area', 'and', 50, 99999)");
                    }
                    else
                    {
                        sb.AppendLine($"* 调用 {node.DisplayName} 参数: {string.Join(", ", pDict.Select(kv => $"{kv.Key}={kv.Value}"))}");
                    }
                    sb.AppendLine();
                }

                sb.AppendLine("dev_update_on ()");
                sb.AppendLine("stop ()");
                return sb.ToString();
            });
        }

        public async Task<string> ExportCSharpCodeAsync()
        {
            return await _context.RunOnUiAsync(() =>
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("// ========================================================");
                sb.AppendLine("// ZeroCodeVisionStudio 自动导出的独立 C# 脱机执行程序");
                sb.AppendLine($"// 方案: {_context.SchemeTitle} | 流程: {_context.ActiveWorkflowName}");
                sb.AppendLine($"// 导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine("// ========================================================");
                sb.AppendLine("using System;");
                sb.AppendLine("using HalconDotNet;");
                sb.AppendLine();
                sb.AppendLine("namespace VisionStandaloneRunner");
                sb.AppendLine("{");
                sb.AppendLine("    public class Program");
                sb.AppendLine("    {");
                sb.AppendLine("        public static void Main(string[] args)");
                sb.AppendLine("        {");
                sb.AppendLine("            Console.WriteLine(\"正在执行工业视觉算法流程...\");");
                sb.AppendLine("            var runner = new WorkflowRunner();");
                sb.AppendLine("            runner.Execute();");
                sb.AppendLine("            Console.WriteLine(\"执行完毕。\");");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    public class WorkflowRunner");
                sb.AppendLine("    {");
                sb.AppendLine("        public void Execute()");
                sb.AppendLine("        {");
                sb.AppendLine("            HObject ho_Image = null;");
                sb.AppendLine("            HObject ho_Region = null;");
                sb.AppendLine("            try");
                sb.AppendLine("            {");

                var nodes = _context.GetActiveNodes();
                foreach (var node in nodes)
                {
                    sb.AppendLine($"                // 节点: {node.DisplayName}");
                    var pDict = ExtractParameters(node).ToDictionary(p => p.Name, p => p.Value);
                    string type = node.RegisterModel?.Key?.ToLowerInvariant() ?? "";

                    if (type.Contains("image") || type.Contains("input"))
                    {
                        sb.AppendLine("                HOperatorSet.ReadImage(out ho_Image, \"test_sample.png\");");
                    }
                    else if (type.Contains("threshold"))
                    {
                        double min = pDict.TryGetValue("Min", out var m) ? Convert.ToDouble(m) : 100;
                        double max = pDict.TryGetValue("Max", out var mx) ? Convert.ToDouble(mx) : 255;
                        sb.AppendLine($"                HOperatorSet.Threshold(ho_Image, out ho_Region, {min}, {max});");
                    }
                    else
                    {
                        sb.AppendLine($"                // 执行算子 {node.DisplayName} (参数: {string.Join(", ", pDict.Select(kv => $"{kv.Key}={kv.Value}"))})");
                    }
                }

                sb.AppendLine("            }");
                sb.AppendLine("            finally");
                sb.AppendLine("            {");
                sb.AppendLine("                ho_Image?.Dispose();");
                sb.AppendLine("                ho_Region?.Dispose();");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine("}");

                return sb.ToString();
            });
        }

        // ==================== 变量 / 执行条件 / 撤销重做 / 连续运行 / PLC 值读写 ====================

        private static WorkflowVariableScope ParseVariableScope(string scope)
        {
            var s = (scope ?? "global").Trim().ToLowerInvariant();
            if (s is "workflow" or "流程" or "流程级" or "w") return WorkflowVariableScope.Workflow;
            return WorkflowVariableScope.Global;
        }

        /// <summary>列出全局与流程级变量。工作流名单在 UI 线程捕获，变量读取走存储自身锁。</summary>
        public async Task<List<VariableDto>> GetVariablesAsync(string scope = null, string keyword = null)
        {
            var parsedScope = ParseVariableScope(scope);
            var wantGlobal = string.IsNullOrWhiteSpace(scope) || parsedScope == WorkflowVariableScope.Global;
            var wantWorkflow = string.IsNullOrWhiteSpace(scope) || parsedScope == WorkflowVariableScope.Workflow;

            var workflowNames = await _context.RunOnUiAsync(() => _context.WorkflowNames.ToList());
            var activeName = await _context.RunOnUiAsync(() => _context.ActiveWorkflowName);

            return await Task.Run(() =>
            {
                var list = new List<VariableDto>();
                if (wantGlobal)
                {
                    foreach (var kv in WorkflowVariableStore.GetAllGlobal())
                        list.Add(new VariableDto { Name = kv.Key, Value = kv.Value, Scope = "global" });
                }
                if (wantWorkflow)
                {
                    var targets = workflowNames.Count > 0 ? workflowNames : new List<string> { null };
                    foreach (var wf in targets)
                    foreach (var kv in WorkflowVariableStore.GetAllWorkflow(wf))
                        list.Add(new VariableDto
                        {
                            Name = kv.Key,
                            Value = kv.Value,
                            Scope = string.IsNullOrEmpty(wf) ? (activeName ?? "workflow") : wf
                        });
                }

                if (!string.IsNullOrWhiteSpace(keyword))
                    list = list.Where(v => v.Name?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                return list.OrderBy(v => v.Scope).ThenBy(v => v.Name).ToList();
            });
        }

        public async Task<OperationResultDto> SetVariableAsync(string name, string value, string scope = "global", string workflowName = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                return OperationResultDto.Fail("变量名不能为空");

            var parsed = ParseVariableScope(scope);
            string targetWorkflow = null;
            if (parsed == WorkflowVariableScope.Workflow)
            {
                targetWorkflow = await _context.RunOnUiAsync(() => string.IsNullOrWhiteSpace(workflowName)
                    ? _context.ActiveWorkflowName
                    : workflowName);
                if (string.IsNullOrWhiteSpace(targetWorkflow))
                    return OperationResultDto.Fail("未找到可用的流程级作用域（当前无活动流程）");
            }

            return await Task.Run(() =>
            {
                WorkflowVariableStore.Set(name.Trim(), value ?? "", parsed, targetWorkflow);
                var prefix = parsed == WorkflowVariableScope.Global ? "g" : "w";
                var scopeText = parsed == WorkflowVariableScope.Global ? "全局" : $"流程级[{targetWorkflow}]";
                return OperationResultDto.Ok(
                    $"已写入{scopeText}变量 {name.Trim()} = {value ?? ""}（算子参数绑定可用 var:{prefix}:{name.Trim()} 引用）",
                    new VariableDto
                    {
                        Name = name.Trim(),
                        Value = value ?? "",
                        Scope = parsed == WorkflowVariableScope.Global ? "global" : targetWorkflow
                    });
            });
        }

        public Task<OperationResultDto> DeleteVariableAsync(string name, string scope = "global", string workflowName = null)
        {
            return Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(name))
                    return OperationResultDto.Fail("变量名不能为空");

                var parsed = ParseVariableScope(scope);
                var targetWorkflow = parsed == WorkflowVariableScope.Workflow ? workflowName : null;
                if (parsed == WorkflowVariableScope.Workflow && string.IsNullOrWhiteSpace(targetWorkflow))
                    targetWorkflow = _context.ActiveWorkflowName;

                if (!WorkflowVariableStore.Contains(name.Trim(), parsed, targetWorkflow))
                    return OperationResultDto.Fail($"变量 [{name}] 不存在（作用域 {(parsed == WorkflowVariableScope.Global ? "global" : "workflow")}）");

                WorkflowVariableStore.Remove(name.Trim(), parsed, targetWorkflow);
                return OperationResultDto.Ok($"已删除变量 [{name}]（作用域 {parsed}）");
            });
        }

        public async Task<OperationResultDto> SetNodeExecutionConditionAsync(string nodeName, string condition = null)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var node = _context.GetActiveNodes().FirstOrDefault(n =>
                    string.Equals(n.DisplayName, nodeName, StringComparison.OrdinalIgnoreCase));
                if (node == null)
                    return OperationResultDto.Fail($"未找到节点 [{nodeName}]");

                var old = node.ExecutionCondition ?? "";
                var updated = (condition ?? "").Trim();
                node.ExecutionCondition = updated;

                return updated.Length == 0
                    ? OperationResultDto.Ok($"已清除节点 [{node.DisplayName}] 的执行条件（每次流程运行都会执行）", new { Previous = old })
                    : OperationResultDto.Ok($"节点 [{node.DisplayName}] 执行条件已设置：{updated}", new { Previous = old, Current = updated });
            });
        }

        public async Task<OperationResultDto> UndoAsync()
        {
            return await _context.RunOnUiAsync(() =>
                _context.Undo()
                    ? OperationResultDto.Ok("已撤销上一步操作")
                    : OperationResultDto.Fail("没有可撤销的操作"));
        }

        public async Task<OperationResultDto> RedoAsync()
        {
            return await _context.RunOnUiAsync(() =>
                _context.Redo()
                    ? OperationResultDto.Ok("已重做被撤销的操作")
                    : OperationResultDto.Fail("没有可重做的操作"));
        }

        public async Task<OperationResultDto> SetContinuousRunAsync(bool enabled)
        {
            return await _context.RunOnUiAsync(() =>
                _context.SetContinuousRun(enabled)
                    ? OperationResultDto.Ok(enabled ? "已启动连续运行（循环执行当前流程，可用 set_continuous_run(false) 或 stop_execution 停止）" : "已停止连续运行")
                    : OperationResultDto.Fail(enabled ? "连续运行已在进行中" : "当前未在连续运行"));
        }

        private static IPlcValueDriver ResolveValueDriver(string driverName)
        {
            var driver = CommunicationCenter.GetExistingDriver(driverName);
            if (driver == null)
                throw new InvalidOperationException(
                    $"未找到已创建的通讯驱动 [{driverName}]（若从未连接过，请先在通讯驱动管理中连接一次）");
            if (driver is not IPlcValueDriver valueDriver)
                throw new InvalidOperationException(
                    $"驱动 [{driverName}]（{driver.Config.Type}）不支持 PLC 值读写，仅支持 西门子S7 / 三菱MC / 欧姆龙FINS 类型驱动");
            return valueDriver;
        }

        private static object ParsePlcValue(object value, string type)
        {
            var t = (type ?? "").Trim().ToLowerInvariant();
            var text = value?.ToString()?.Trim() ?? "";
            if (t is "bit" or "bool")
            {
                if (bool.TryParse(text, out var b)) return b;
                if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var n)) return n != 0;
                return string.Equals(text, "ON", StringComparison.OrdinalIgnoreCase);
            }
            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            return value;
        }

        public async Task<OperationResultDto> CommReadValueAsync(string driverName, string address, string type = null)
        {
            if (string.IsNullOrWhiteSpace(driverName) || string.IsNullOrWhiteSpace(address))
                return OperationResultDto.Fail("缺少 driverName / address 参数");

            var valueType = string.IsNullOrWhiteSpace(type) ? "int16" : type;
            try
            {
                var value = await Task.Run(() => ResolveValueDriver(driverName).ReadValue(address, valueType));
                if (value == null)
                    return OperationResultDto.Fail($"读取 [{driverName}:{address}] 返回空（连接正常但地址可能无效，详见驱动 LastError）");
                return OperationResultDto.Ok($"读取 {address} = {value}（{valueType}）",
                    new { Driver = driverName, Address = address, Type = valueType, Value = value });
            }
            catch (Exception ex)
            {
                return OperationResultDto.Fail($"读取 [{driverName}:{address}] 失败：{ex.Message}");
            }
        }

        public async Task<OperationResultDto> SetWorkflowParallelExecutionAsync(bool enabled, string workflowName = null)
        {
            return await _context.RunOnUiAsync(() =>
            {
                var done = _context.SetWorkflowParallelExecution(enabled, workflowName);
                return done
                    ? OperationResultDto.Ok(enabled
                        ? "已开启并行执行：同层兄弟分支并发、层间按拓扑顺序等待（数据流调度）"
                        : "已关闭并行执行：恢复串行顺序执行")
                    : OperationResultDto.Fail(string.IsNullOrWhiteSpace(workflowName)
                        ? "当前没有活动流程"
                        : $"未找到流程 [{workflowName}]");
            });
        }

        public async Task<OperationResultDto> NewSchemeAsync()
        {
            await _context.RunOnUiAsync(() => _context.NewScheme());
            return OperationResultDto.Ok("已新建空白方案（未命名方案，含空的「主流程」；保存请用 save_scheme）");
        }

        public async Task<List<SchemeFileInfoDto>> ListRecentSchemesAsync()
        {
            return await Task.Run(() =>
            {
                var result = new List<SchemeFileInfoDto>();
                foreach (var path in LoadRecentSchemePaths())
                {
                    var info = new FileInfo(path);
                    result.Add(new SchemeFileInfoDto
                    {
                        FileName = Path.GetFileNameWithoutExtension(path),
                        Path = path,
                        ModifiedTime = info.Exists ? info.LastWriteTime : DateTime.MinValue,
                        SizeBytes = info.Exists ? info.Length : 0
                    });
                }
                return result;
            });
        }

        /// <summary>读取最近打开方案清单（与主程序 RecentSchemeService 同一存储：应用目录 config/recent_schemes.json）。</summary>
        private static List<string> LoadRecentSchemePaths()
        {
            try
            {
                var storePath = Path.Combine(AppContext.BaseDirectory, "config", "recent_schemes.json");
                if (!File.Exists(storePath)) return new List<string>();
                var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(File.ReadAllText(storePath)) ?? new List<string>();
                return list.FindAll(File.Exists);
            }
            catch
            {
                return new List<string>();
            }
        }

        public async Task<OperationResultDto> CommWriteValueAsync(string driverName, string address, object value, string type = null)
        {
            if (string.IsNullOrWhiteSpace(driverName) || string.IsNullOrWhiteSpace(address))
                return OperationResultDto.Fail("缺少 driverName / address 参数");

            var valueType = string.IsNullOrWhiteSpace(type) ? "int16" : type;
            try
            {
                var parsed = ParsePlcValue(value, valueType);
                var ok = await Task.Run(() => ResolveValueDriver(driverName).WriteValue(address, valueType, parsed));
                return ok
                    ? OperationResultDto.Ok($"已写入 {address} = {parsed}（{valueType}）",
                        new { Driver = driverName, Address = address, Type = valueType, Value = parsed })
                    : OperationResultDto.Fail($"写入 [{driverName}:{address}] 失败（地址格式或权限问题）");
            }
            catch (Exception ex)
            {
                return OperationResultDto.Fail($"写入 [{driverName}:{address}] 失败：{ex.Message}");
            }
        }
    }
}
