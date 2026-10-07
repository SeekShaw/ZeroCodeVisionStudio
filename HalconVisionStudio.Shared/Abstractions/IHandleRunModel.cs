using System;
using System.Collections.Generic;
using System.Text;

namespace HalconVisionStudio.Shared
{
    /// <summary>
    /// 算子运行模型接口。
    /// 描述流程画布上一个运行节点的完整运行时状态，包括算子实例、执行状态、输入绑定、坐标等。
    /// 通常由 HandleRunModel 实现，是 ViewModel 层与流程引擎的核心数据载体。
    /// </summary>
    public interface IHandleRunModel : IDisposable
    {
        /// <summary>该节点对应的算子实例，负责实际执行图像处理逻辑。</summary>
        IHandle Handle { get; }

        /// <summary>算子的注册信息（类型、名称、键名等元数据）。</summary>
        IHandleRegisterModel RegisterModel { get; }

        /// <summary>节点在画布上的显示名称（允许用户重命名）。</summary>
        string DisplayName { get; set; }

        /// <summary>算子参数类的类型名称，用于"模块结果"表格中展示参数类别。</summary>
        string ParameterName { get; }

        /// <summary>当前执行状态（Pending / Running / Success / Failed）。</summary>
        HandleStatus Status { get; set; }

        /// <summary>执行状态说明文字，成功时为结果摘要，失败时为错误信息。</summary>
        string StatusMessage { get; set; }
        TimeSpan LastElapsed { get; set; }
        string LastElapsedText { get; }

        /// <summary>节点是否处于选中状态，用于 UI 高亮显示。</summary>
        bool IsSelected { get; set; }

        /// <summary>
        /// 节点的输入槽绑定列表。
        /// 每个 <see cref="WorkflowInputBinding"/> 描述一个输入槽与上游节点输出之间的映射关系。
        /// </summary>
        IList<WorkflowInputBinding> InputBindings { get; }

        /// <summary>
        /// 节点级执行条件表达式（可选）：流程执行到此节点时先求值，为假则跳过本节点。
        /// 语法：$Key$ 引用上游输出（插值），支持 ==、!=、&gt;、&gt;=、&lt;、&lt;= 单个比较与前置 ! 取反；
        /// 裸值按真值判断（true/1/非零数字为真）。空字符串表示无条件执行。
        /// </summary>
        string ExecutionCondition { get; set; }

        /// <summary>节点在流程画布上的水平坐标（左上角 X）。</summary>
        double X { get; set; }

        /// <summary>节点在流程画布上的垂直坐标（左上角 Y）。</summary>
        double Y { get; set; }

        /// <summary>
        /// 根据算子当前配置重新生成可绑定输入项。
        /// 用于支持输入槽数量会变化的算子，例如显示信息这类可动态增减显示项的节点。
        /// 已存在且键名相同的绑定会保留原有来源节点和来源输出，避免用户重新配置。
        /// </summary>
        void RefreshInputBindings();

        /// <summary>
        /// 重置节点状态（清空 RunParameter 数据并将状态恢复为 Pending）。
        /// 在每次完整流程执行前会对所有节点调用此方法。
        /// </summary>
        void Reset();
    }
}
