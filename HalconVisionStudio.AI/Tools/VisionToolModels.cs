using System;
using System.Collections.Generic;

namespace HalconVisionStudio.AI.Tools
{
    public class WorkflowStateDto
    {
        /// <summary>当前流程是否启用并行执行（同层兄弟分支并发）。</summary>
        public bool ParallelExecution { get; set; }
        public string SchemeTitle { get; set; }
        public string ActiveWorkflowName { get; set; }
        public List<string> AllWorkflows { get; set; } = new List<string>();
        public List<NodeDto> Nodes { get; set; } = new List<NodeDto>();
        public List<ConnectionDto> Connections { get; set; } = new List<ConnectionDto>();
    }

    public class NodeDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string OperatorType { get; set; }
        public string Category { get; set; }
        public string Status { get; set; }
        public string StatusMessage { get; set; }
        public double LastElapsedMs { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public string ExecutionCondition { get; set; }
        public List<ParameterDto> Parameters { get; set; } = new List<ParameterDto>();
        public List<string> InputPins { get; set; } = new List<string>();
        public List<string> OutputPins { get; set; } = new List<string>();
    }

    public class ConnectionDto
    {
        public string SourceNode { get; set; }
        public string SourcePin { get; set; }
        public string TargetNode { get; set; }
        public string TargetPin { get; set; }
    }

    public class ParameterDto
    {
        public string Name { get; set; }
        public object Value { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
    }

    public class OperatorCatalogItemDto
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public List<string> InputPins { get; set; } = new List<string>();
        public List<string> OutputPins { get; set; } = new List<string>();
    }

    public class ExecutionRecordDto
    {
        public int Sequence { get; set; }
        public string Time { get; set; }
        public string Module { get; set; }
        public string Status { get; set; }
        public double ElapsedMs { get; set; }
        public string ResultData { get; set; }
    }

    public class FaultSnapshotDto
    {
        public string NodeName { get; set; }
        public string OperatorType { get; set; }
        public string Status { get; set; }
        public string ErrorMessage { get; set; }
        public double ElapsedMs { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public List<ConnectionDto> UpstreamConnections { get; set; } = new List<ConnectionDto>();
        public List<string> UpstreamNodes { get; set; } = new List<string>();
    }

    public class OperationResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }

        public static OperationResultDto Ok(string message = "Success", object data = null) =>
            new OperationResultDto { Success = true, Message = message, Data = data };

        public static OperationResultDto Fail(string message) =>
            new OperationResultDto { Success = false, Message = message };

        public static OperationResultDto Fail(string message, object data) =>
            new OperationResultDto { Success = false, Message = message, Data = data };
    }

    public class OperatorSchemaDto
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public List<SlotDto> InputSlots { get; set; } = new List<SlotDto>();
        public List<SlotDto> OutputSlots { get; set; } = new List<SlotDto>();
        public List<ParameterSchemaDto> Parameters { get; set; } = new List<ParameterSchemaDto>();
    }

    public class SlotDto
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public string TypeName { get; set; }
    }

    public class ParameterSchemaDto
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string TypeName { get; set; }
        public object DefaultValue { get; set; }
        public string Description { get; set; }
    }

    public class TuneAndVerifyResultDto
    {
        public string NodeName { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public double ElapsedMs { get; set; }
        public Dictionary<string, object> PreviousParameters { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, object> UpdatedParameters { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, object> RuntimeOutputs { get; set; } = new Dictionary<string, object>();
    }

    public class ImageHistogramDto
    {
        public double Mean { get; set; }
        public double Deviation { get; set; }
        public double MinGray { get; set; }
        public double MaxGray { get; set; }
        public double DynamicRange { get; set; }
        public List<int> HistogramPercentiles { get; set; } = new List<int>();
        public string Evaluation { get; set; }
    }

    public class AdaptiveThresholdResultDto
    {
        public double OptimalMin { get; set; }
        public double OptimalMax { get; set; }
        public string Algorithm { get; set; }
        public string Rationale { get; set; }
    }

    /// <summary>节点输入绑定的当前状态（数据槽绑定 / 参数引用绑定）。</summary>
    public class InputBindingDto
    {
        /// <summary>目标输入槽键名（如 Image、Region、parameter:MinGray）。</summary>
        public string TargetInputKey { get; set; }
        /// <summary>目标输入槽显示名。</summary>
        public string DisplayName { get; set; }
        /// <summary>是否为参数属性引用绑定（上游值写入下游算子参数）。</summary>
        public bool IsParameterBinding { get; set; }
        /// <summary>参数引用绑定的目标参数属性名（非参数绑定为空）。</summary>
        public string TargetParameterName { get; set; }
        /// <summary>绑定的上游节点名（null=未绑定）。</summary>
        public string SourceNode { get; set; }
        /// <summary>绑定的上游输出槽键名。</summary>
        public string SourceOutputKey { get; set; }
        /// <summary>期望的数据类型名。</summary>
        public string TypeName { get; set; }
    }

    /// <summary>默认方案目录中的方案文件摘要信息。</summary>
    /// <summary>变量（全局或流程级）。</summary>
    public class VariableDto
    {
        public string Name { get; set; }
        public string Value { get; set; }
        /// <summary>global 或 流程名（流程级变量）。</summary>
        public string Scope { get; set; }
    }

    public class SchemeFileInfoDto
    {
        public string FileName { get; set; }
        public string Path { get; set; }
        public DateTime ModifiedTime { get; set; }
        public long SizeBytes { get; set; }
    }
}
