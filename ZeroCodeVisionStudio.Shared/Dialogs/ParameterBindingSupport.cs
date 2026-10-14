using System;
using System.Collections.Generic;
using System.Reflection;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>参数对话框绑定编辑区的候选源：上游节点输出，或全局/流程级变量。</summary>
    public class ParameterBindingCandidate
    {
        /// <summary>下拉显示文本（"节点名 · 输出名" / "[全局] 变量名" / "[流程] 变量名"）。</summary>
        public string Display { get; private set; }

        /// <summary>节点输出源：提供数据的节点。</summary>
        public IHandleRunModel SourceNode { get; private set; }

        /// <summary>节点输出源的输出键。</summary>
        public string OutputKey { get; private set; }

        /// <summary>变量源：变量名（与 <see cref="SourceNode"/> 互斥）。</summary>
        public string VariableName { get; private set; }

        /// <summary>变量源作用域。</summary>
        public WorkflowVariableScope VariableScope { get; private set; }

        /// <summary>是否为"未绑定"占位项。</summary>
        public bool IsEmpty => SourceNode == null && string.IsNullOrEmpty(VariableName);

        /// <summary>是否为变量源。</summary>
        public bool IsVariable => SourceNode == null && !IsEmpty;

        public static ParameterBindingCandidate Empty() => new() { Display = "（未绑定）" };

        public static ParameterBindingCandidate ForNode(IHandleRunModel node, string outputKey, string display) =>
            new() { Display = display, SourceNode = node, OutputKey = outputKey };

        public static ParameterBindingCandidate ForVariable(string name, WorkflowVariableScope scope) => new()
        {
            Display = scope == WorkflowVariableScope.Global ? $"[全局] {name}" : $"[流程] {name}",
            VariableName = name,
            VariableScope = scope
        };

        public override string ToString() => Display ?? "";
    }

    /// <summary>
    /// 参数对话框绑定编辑环境：模态对话框打开期间由主程序注入（对话框关闭后清除）。
    /// Shared 层的通用参数对话框据此展示"输入绑定"编辑区——
    /// 无需感知主程序结构，候选与变更通知全部走委托。
    /// </summary>
    public static class ParameterBindingAmbient
    {
        /// <summary>为指定节点生成绑定候选（输入槽与参数绑定共用；首项为"未绑定"占位）。</summary>
        public static Func<IHandleRunModel, List<ParameterBindingCandidate>> GetCandidates { get; set; }

        /// <summary>绑定被修改后的回调（主程序标记方案已修改、刷新绑定面板等）。</summary>
        public static Action<IHandleRunModel> OnBindingChanged { get; set; }

        /// <summary>
        /// 当前正在编辑参数的节点（模态对话框打开期间由主程序设置，关闭后清除）。
        /// 对话框窗口据此为任意算子视图（通用或自定义）挂载绑定编辑区。
        /// </summary>
        public static IHandleRunModel CurrentEditNode { get; set; }

        /// <summary>当前是否可用（对话框据此决定是否显示绑定编辑区）。</summary>
        public static bool IsAvailable => GetCandidates != null;
    }

    /// <summary>
    /// 绑定编辑区构建器：由节点（算子实例 + 参数对象 + 候选）生成绑定行集合。
    /// 输入槽行来自 IHandleDataSlots.InputSlots；参数行来自可写基础类型属性。
    /// </summary>
    public static class ParameterBindingEditorBuilder
    {
        public static System.Collections.ObjectModel.ObservableCollection<BindingRowItem> Build(
            IHandleRunModel node, List<ParameterBindingCandidate> candidates)
        {
            var rows = new System.Collections.ObjectModel.ObservableCollection<BindingRowItem>();
            if (node?.Handle == null || candidates == null || candidates.Count == 0) return rows;

            if (node.Handle is IHandleDataSlots slots && slots.InputSlots != null)
            {
                foreach (var slot in slots.InputSlots)
                {
                    var row = new BindingRowItem
                    {
                        Node = node,
                        TargetKey = slot.Key,
                        DisplayName = $"输入：{slot.DisplayName}",
                        Description = null,
                        Candidates = candidates
                    };
                    row.RestoreSelection();
                    rows.Add(row);
                }
            }

            var parameter = node.Handle.HandleParameter;
            if (parameter != null)
            {
                foreach (var prop in parameter.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    if (!prop.CanWrite || prop.GetIndexParameters().Length > 0) continue;
                    var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    if (type.IsEnum || type == typeof(bool) || type == typeof(string) ||
                        type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) ||
                        type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(float) || type == typeof(double))
                    {
                        var display = prop.GetCustomAttribute<System.ComponentModel.DisplayNameAttribute>()?.DisplayName ?? prop.Name;
                        var row = new BindingRowItem
                        {
                            Node = node,
                            TargetKey = WorkflowInputBinding.ParameterBindingPrefix + prop.Name,
                            DisplayName = $"参数：{display}",
                            Description = prop.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description,
                            Candidates = candidates
                        };
                        row.RestoreSelection();
                        rows.Add(row);
                    }
                }
            }

            return rows;
        }
    }
}
