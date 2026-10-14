using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroCodeVisionStudio.Shared;
using ZeroCodeVisionStudio.Shared.Data;
using ZeroCodeVisionStudio.ViewModels;

namespace ZeroCodeVisionStudio.Services
{
    /// <summary>
    /// 方案文件服务：从 MainViewModel 拆出的方案保存/加载/参数恢复/绑定恢复。
    /// <para>
    /// 职责：BuildSchemeData（节点/连线/参数/绑定/执行条件 → DTO）与
    /// ApplySchemeData（DTO → 节点/参数/绑定/连线，含类型转换与加载报告）。
    /// 宿主通过 <see cref="ISchemePersistenceHost"/> 提供工作流集合与节点工厂。
    /// </para>
    /// </summary>
    internal class SchemeFileService
    {
        /// <summary>持久化服务对宿主的最小依赖面。</summary>
        internal interface ISchemePersistenceHost
        {
            string SchemeName { get; }
            ObservableCollection<WorkflowViewModel> Workflows { get; }
            WorkflowViewModel ActiveWorkflow { get; set; }
            int ActiveWorkflowIndex { get; set; }

            /// <summary>从算子注册中心创建运行节点。</summary>
            IHandleRunModel CreateNode(IHandleRegisterModel registerModel, string displayName, double x, double y);

            /// <summary>获取/创建工作流的名字生成器（WorkflowViewModel 构造需要）。</summary>
            ZeroCodeVisionStudio.Shared.Helper.SequenceNameGenerator GetSequenceGenerator(WorkflowViewModel owner);

            /// <summary>获取已注册算子。</summary>
            IHandleRegisterModel GetRegister(string key);

            /// <summary>节点加入工作流后的显示刷新。</summary>
            void OnNodeAdded(IHandleRunModel node, WorkflowViewModel owner);

            /// <summary>方案加载完成后的整理。</summary>
            void OnSchemeApplied(SchemeData data, string filePath, string loadingReport);
        }

        private readonly ISchemePersistenceHost host;

        public SchemeFileService(ISchemePersistenceHost host)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
        }

        // ==================== 保存 ====================

        /// <summary>构建方案持久化 DTO（节点/连线/参数/输入绑定/执行条件/显示源）。</summary>
        public SchemeData BuildSchemeData(string schemeName, int activeWorkflowIndex)
        {
            var data = new SchemeData
            {
                Name = schemeName,
                ActiveWorkflowIndex = activeWorkflowIndex,
                Workflows = new List<WorkflowData>()
            };

            foreach (var wf in host.Workflows)
            {
                var wfData = new WorkflowData { Name = wf.WorkflowName, ParallelExecution = wf.ParallelExecution };
                var nodeIndexMap = new Dictionary<IHandleRunModel, int>();

                for (int i = 0; i < wf.Nodes.Count; i++)
                {
                    var node = wf.Nodes[i];
                    nodeIndexMap[node] = i;

                    var nodeData = new NodeData
                    {
                        RegisterKey = node.RegisterModel?.Key,
                        DisplayName = node.DisplayName,
                        X = node.X,
                        Y = node.Y,
                        ExecutionCondition = node.ExecutionCondition
                    };

                    // 保存参数值。
                    var parameter = node.Handle?.HandleParameter;
                    if (parameter != null)
                    {
                        foreach (var prop in parameter.GetType().GetProperties()
                            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
                        {
                            try
                            {
                                var val = prop.GetValue(parameter);
                                if (val != null)
                                    nodeData.ParameterValues[prop.Name] = val;
                            }
                            catch { }
                        }
                    }

                    // 保存输入绑定（含变量源绑定：SourceNode 为空但 SourceOutputKey 以 var: 开头）。
                    foreach (var binding in node.InputBindings.Where(b => b.SourceNode != null || b.IsVariableSource))
                    {
                        nodeData.InputBindings.Add(new InputBindingData
                        {
                            TargetInputKey = binding.TargetInputKey,
                            SourceNodeIndex = binding.SourceNode != null && nodeIndexMap.TryGetValue(binding.SourceNode, out var idx) ? idx : null,
                            SourceOutputKey = binding.SourceOutputKey
                        });
                    }

                    wfData.Nodes.Add(nodeData);
                }

                // 保存连线。
                foreach (var conn in wf.Connections)
                {
                    if (nodeIndexMap.TryGetValue(conn.From, out var fi) && nodeIndexMap.TryGetValue(conn.To, out var ti))
                    {
                        wfData.Connections.Add(new ConnectionData { FromIndex = fi, ToIndex = ti });
                    }
                }

                // 保存显示源顺序。
                for (int i = 0; i < wf.DisplaySources.Count; i++)
                {
                    var displaySource = wf.DisplaySources[i];
                    if (displaySource?.Node == null) continue;
                    if (!nodeIndexMap.TryGetValue(displaySource.Node, out var nodeIdx)) continue;
                    wfData.DisplaySources.Add(new DisplaySourceData
                    {
                        NodeIndex = nodeIdx,
                        OutputKey = displaySource.Output?.OutputKey,
                        Order = displaySource.Order,
                        IsVisible = displaySource.IsVisible
                    });
                }

                data.Workflows.Add(wfData);
            }

            return data;
        }

        // ==================== 加载 ====================

        /// <summary>
        /// 将方案 DTO 应用到宿主：重建全部工作流（节点/参数/绑定/连线/显示源），
        /// 返回加载报告（缺失算子/失败参数计数），无问题时为空字符串。
        /// </summary>
        public async Task<string> ApplySchemeDataAsync(SchemeData data, string filePath)
        {
            if (data?.Workflows == null || data.Workflows.Count == 0)
            {
                host.ActiveWorkflow = null;
                return "方案文件中没有工作流数据。";
            }

            var report = new System.Text.StringBuilder();
            var missingOperatorCount = 0;
            var failedParameterCount = 0;

            var newWorkflows = new List<WorkflowViewModel>();

            foreach (var wfData in data.Workflows)
            {
                var wf = new WorkflowViewModel(wfData.Name ?? $"流程{newWorkflows.Count + 1}", host.GetSequenceGenerator(null));
            wf.ParallelExecution = wfData.ParallelExecution;
                var nodeIndexMap = new Dictionary<IHandleRunModel, int>();

                // 1) 重建节点。
                foreach (var nodeData in wfData.Nodes)
                {
                    if (string.IsNullOrWhiteSpace(nodeData.RegisterKey))
                    {
                        missingOperatorCount++;
                        continue;
                    }

                    var registerModel = host.GetRegister(nodeData.RegisterKey);
                    if (registerModel == null)
                    {
                        missingOperatorCount++;
                        report.AppendLine($"缺少算子 [{nodeData.RegisterKey}]（节点 [{nodeData.DisplayName}] 未恢复）");
                        continue;
                    }

                    var runModel = host.CreateNode(registerModel, nodeData.DisplayName ?? registerModel.HandleName, nodeData.X, nodeData.Y);
                    runModel.ExecutionCondition = nodeData.ExecutionCondition ?? "";
                    wf.Nodes.Add(runModel);
                    nodeIndexMap[runModel] = wf.Nodes.Count - 1;
                    host.OnNodeAdded(runModel, wf);

                    // 恢复参数值。
                    if (nodeData.ParameterValues != null)
                    {
                        var parameter = runModel.Handle?.HandleParameter;
                        if (parameter != null)
                        {
                            foreach (var kv in nodeData.ParameterValues)
                            {
                                try
                                {
                                    var prop = parameter.GetType().GetProperty(kv.Key);
                                    if (prop == null || !prop.CanWrite) continue;
                                    prop.SetValue(parameter, ConvertParameterForLoad(kv.Value, prop.PropertyType));
                                }
                                catch
                                {
                                    failedParameterCount++;
                                }
                            }
                        }
                    }
                }

                // 2) 恢复输入绑定。
                var nodeByIndex = new Dictionary<int, IHandleRunModel>();
                foreach (var pair in nodeIndexMap)
                {
                    nodeByIndex[pair.Value] = pair.Key;
                }

                foreach (var pair in nodeIndexMap)
                {
                    var nodeData = wfData.Nodes[pair.Value];
                    var node = pair.Key;
                    if (nodeData.InputBindings == null) continue;

                    node.RefreshInputBindings();
                    foreach (var bindData in nodeData.InputBindings)
                    {
                        var binding = node.InputBindings.FirstOrDefault(b => b.TargetInputKey == bindData.TargetInputKey);
                        if (binding == null) continue;

                        if (bindData.SourceNodeIndex.HasValue &&
                            nodeByIndex.TryGetValue(bindData.SourceNodeIndex.Value, out var sourceNode))
                        {
                            binding.SourceNode = sourceNode;
                            binding.SourceOutputKey = bindData.SourceOutputKey;
                        }
                        else if (!string.IsNullOrWhiteSpace(bindData.SourceOutputKey))
                        {
                            // 变量源绑定（var:g:名 / var:w:名）。
                            binding.SourceNode = null;
                            binding.SourceOutputKey = bindData.SourceOutputKey;
                        }
                    }
                }

                // 3) 恢复连线。
                if (wfData.Connections != null)
                {
                    foreach (var connData in wfData.Connections)
                    {
                        if (nodeByIndex.TryGetValue(connData.FromIndex, out var fromNode) &&
                            nodeByIndex.TryGetValue(connData.ToIndex, out var toNode))
                        {
                            wf.Connections.Add(new ZeroCodeVisionStudio.UI.Controls.WorkflowConnection(fromNode, toNode));
                        }
                    }
                }

                wf.RefreshConnections();
                newWorkflows.Add(wf);
            }

            // 4) 替换宿主工作流集合。
            host.Workflows.Clear();
            foreach (var wf in newWorkflows)
            {
                host.Workflows.Add(wf);
            }

            var activeIndex = Math.Clamp(data.ActiveWorkflowIndex, 0, Math.Max(0, newWorkflows.Count - 1));
            host.ActiveWorkflowIndex = activeIndex;
            host.ActiveWorkflow = newWorkflows.Count > 0 ? newWorkflows[activeIndex] : null;

            // 5) 生成加载报告。
            var reportText = report.ToString();
            if (missingOperatorCount > 0 || failedParameterCount > 0)
            {
                reportText = $"方案加载完成但有部分数据未恢复：缺失算子 {missingOperatorCount} 个，参数恢复失败 {failedParameterCount} 项。\n{reportText}";
            }

            host.OnSchemeApplied(data, filePath, reportText);
            await Task.CompletedTask;
            return reportText;
        }

        // ==================== 参数类型转换 ====================

        internal static object ConvertParameterForLoad(object value, Type targetType)
        {
            targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (value == null)
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            if (targetType.IsInstanceOfType(value)) return value;

            if (value is JsonElement element)
            {
                return ConvertJsonElement(element, targetType);
            }

            if (value is System.Collections.IList list && targetType.IsGenericType &&
                targetType.GetGenericTypeDefinition() == typeof(ObservableCollection<>))
            {
                var itemType = targetType.GetGenericArguments()[0];
                var result = (System.Collections.IList)Activator.CreateInstance(targetType);
                foreach (var item in list)
                {
                    result.Add(ConvertParameterForLoad(item, itemType));
                }
                return result;
            }

            if (value is string text)
            {
                if (targetType == typeof(string)) return text;
                if (targetType == typeof(bool)) return bool.TryParse(text, out var b) ? b : false;
                if (targetType.IsEnum) return Enum.Parse(targetType, text);
                if (typeof(IConvertible).IsAssignableFrom(targetType))
                    return Convert.ChangeType(text, targetType);
            }

            if (value is IConvertible convertible && typeof(IConvertible).IsAssignableFrom(targetType))
            {
                return Convert.ChangeType(convertible, targetType);
            }

            return value;
        }

        private static object ConvertJsonElement(JsonElement element, Type targetType)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number when targetType == typeof(int) => element.GetInt32(),
                JsonValueKind.Number when targetType == typeof(long) => element.GetInt64(),
                JsonValueKind.Number when targetType == typeof(double) => element.GetDouble(),
                JsonValueKind.String when targetType == typeof(string) => element.GetString(),
                JsonValueKind.True when targetType == typeof(bool) => true,
                JsonValueKind.False when targetType == typeof(bool) => false,
                JsonValueKind.Array => ConvertJsonArray(element, targetType),
                _ => element.ToString()
            };
        }

        private static object ConvertJsonArray(JsonElement element, Type targetType)
        {
            if (!targetType.IsGenericType) return element.ToString();
            var def = targetType.GetGenericTypeDefinition();
            if (def != typeof(List<>) && def != typeof(ObservableCollection<>)) return element.ToString();

            var itemType = targetType.GetGenericArguments()[0];
            var listType = typeof(List<>).MakeGenericType(itemType);
            var list = (System.Collections.IList)Activator.CreateInstance(listType);
            foreach (var item in element.EnumerateArray())
            {
                list.Add(ConvertJsonElement(item, itemType));
            }

            if (def == typeof(ObservableCollection<>))
            {
                return Activator.CreateInstance(targetType, list);
            }
            return list;
        }
    }
}
