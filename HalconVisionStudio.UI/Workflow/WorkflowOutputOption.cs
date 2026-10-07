using HalconVisionStudio.Shared;

namespace HalconVisionStudio.UI.Controls
{
    /// <summary>
    /// 表示工作流中某个上游节点的输出选项，用于输入绑定下拉框。
    /// SourceNode + OutputKey 唯一标识一个输出槽；DisplayName 是显示给用户的可读名称。
    /// </summary>
    public class WorkflowOutputOption
    {
        public WorkflowOutputOption(IHandleRunModel sourceNode, string outputKey, string displayName, Type valueType = null)
        {
            SourceNode = sourceNode;
            OutputKey = outputKey;
            DisplayName = displayName;
            ValueType = valueType;
        }

        /// <summary>变量源选项（var:g:名称 / var:w:名称），与节点输出源互斥。</summary>
        public WorkflowOutputOption(string variableName, WorkflowVariableScope scope)
        {
            VariableName = variableName;
            VariableScope = scope;
            DisplayName = scope == WorkflowVariableScope.Global ? $"[全局] {variableName}" : $"[流程] {variableName}";
        }

        public IHandleRunModel SourceNode { get; }

        public string OutputKey { get; }

        public string DisplayName { get; }

        public Type ValueType { get; }

        /// <summary>变量源的变量名（节点输出源为 null）。</summary>
        public string VariableName { get; }

        /// <summary>变量源的作用域（节点输出源下无意义）。</summary>
        public WorkflowVariableScope VariableScope { get; }

        public bool IsVariable => SourceNode == null && !string.IsNullOrEmpty(VariableName);

        /// <summary>
        /// 判断该选项是否为"未绑定"占位选项。
        /// </summary>
        public bool IsEmpty => !IsVariable && (SourceNode == null || string.IsNullOrWhiteSpace(OutputKey));

        public override string ToString() => DisplayName ?? string.Empty;
    }
}

