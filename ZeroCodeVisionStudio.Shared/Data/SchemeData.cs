using System;
using System.Collections.Generic;

namespace ZeroCodeVisionStudio.Shared.Data
{
    /// <summary>
    /// 方案持久化数据模型——一个方案包含多个流程。
    /// </summary>
    [Serializable]
    public class SchemeData
    {
        /// <summary>当前方案格式的架构版本。缺少该字段（=0）的文件视为旧版（v1）格式。</summary>
        public int Version { get; set; } = CurrentVersion;

        /// <summary>程序当前写出的方案格式版本。</summary>
        public const int CurrentVersion = 2;

        /// <summary>方案名称。</summary>
        public string Name { get; set; } = "新方案";

        /// <summary>方案中包含的流程列表。</summary>
        public List<WorkflowData> Workflows { get; set; } = new();

        /// <summary>当前激活的流程索引。</summary>
        public int ActiveWorkflowIndex { get; set; }
    }

    /// <summary>
    /// 单个流程的持久化数据——包含节点、连线和输入绑定。
    /// </summary>
    [Serializable]
    public class WorkflowData
    {
        /// <summary>流程名称。</summary>
        public string Name { get; set; } = "主流程";

        /// <summary>是否并行执行：同一层兄弟分支并发（默认 false 保持向后兼容）。</summary>
        public bool ParallelExecution { get; set; } = false;

        /// <summary>流程中的节点列表。</summary>
        public List<NodeData> Nodes { get; set; } = new();

        /// <summary>流程中的连线列表。</summary>
        public List<ConnectionData> Connections { get; set; } = new();

        /// <summary>流程显示区的数据源顺序。</summary>
        public List<DisplaySourceData> DisplaySources { get; set; } = new();
    }

    /// <summary>
    /// 单个算子的持久化数据。
    /// 通过 RegisterKey 在工具箱中查找算子类型，恢复 DisplayName、坐标和参数值。
    /// </summary>
    [Serializable]
    public class NodeData
    {
        /// <summary>算子注册键，用于反序列化时从 ControlCenter 找回算子类型。</summary>
        public string RegisterKey { get; set; }

        /// <summary>节点在画布上的显示名称。</summary>
        public string DisplayName { get; set; }

        /// <summary>画布 X 坐标。</summary>
        public double X { get; set; }

        /// <summary>画布 Y 坐标。</summary>
        public double Y { get; set; }

        /// <summary>节点级执行条件表达式（空 = 无条件执行；为假时跳过该节点）。</summary>
        public string ExecutionCondition { get; set; }

        /// <summary>算子的可配置参数值。键为属性名，值为序列化后的参数值。</summary>
        public Dictionary<string, object> ParameterValues { get; set; } = new();

        /// <summary>该节点输入槽的显式绑定来源。</summary>
        public List<InputBindingData> InputBindings { get; set; } = new();
    }

    /// <summary>
    /// 节点间连线的持久化数据。
    /// 通过 FromIndex/ToIndex 引用 Nodes 列表中的节点。
    /// </summary>
    [Serializable]
    public class ConnectionData
    {
        /// <summary>上游节点在 Nodes 列表中的索引。</summary>
        public int FromIndex { get; set; }

        /// <summary>下游节点在 Nodes 列表中的索引。</summary>
        public int ToIndex { get; set; }
    }

    /// <summary>
    /// 输入来源绑定的持久化数据。
    /// SourceNodeIndex 引用上游节点索引，留空表示未绑定。
    /// </summary>
    [Serializable]
    public class InputBindingData
    {
        /// <summary>输入槽键名。</summary>
        public string TargetInputKey { get; set; }

        /// <summary>上游节点在 Nodes 列表中的索引，null=未绑定。</summary>
        public int? SourceNodeIndex { get; set; }

        /// <summary>上游节点输出槽键名。</summary>
        public string SourceOutputKey { get; set; }
    }

    /// <summary>
    /// 流程显示区数据源的持久化数据。
    /// 记录哪个节点的哪个输出槽需要展示到主界面的图像显示面板中，以及显示顺序和可见性。
    /// </summary>
    [Serializable]
    public class DisplaySourceData
    {
        /// <summary>数据源所属节点在 <see cref="WorkflowData.Nodes"/> 列表中的索引。</summary>
        public int NodeIndex { get; set; }

        /// <summary>要展示的输出槽键名，通常为 <see cref="RunOutputKeys"/> 中的常量。</summary>
        public string OutputKey { get; set; }

        /// <summary>在图像显示面板中的排列顺序（从 0 开始，数字越小越靠前）。</summary>
        public int Order { get; set; }

        /// <summary>是否在显示面板中可见。</summary>
        public bool IsVisible { get; set; } = true;
    }
}
