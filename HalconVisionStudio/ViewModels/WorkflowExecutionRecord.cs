using HalconVisionStudio.Shared;

namespace HalconVisionStudio
{
    /// <summary>
    /// 工作流节点执行记录，对应"模块结果"表格中的一行数据。
    /// <para>每次节点执行（无论成功或失败）都会产生一条记录，最多保留 500 条。</para>
    /// </summary>
    public class WorkflowExecutionRecord
    {
        /// <summary>本次执行的全局序号（从 1 开始自增）。</summary>
        public int Sequence { get; init; }

        /// <summary>执行发生的本地时间。</summary>
        public DateTime ExecuteTime { get; init; }

        /// <summary>执行的节点显示名称。</summary>
        public string Module { get; init; }

        /// <summary>节点执行后的状态枚举值。</summary>
        public HandleStatus Status { get; init; }

        public TimeSpan Elapsed { get; init; }

        public string ElapsedText => ExecutionTimeFormatter.Format(Elapsed);

        /// <summary>
        /// 状态的中文显示文本，根据 <see cref="Status"/> 映射：
        /// Running→"执行中"，Success→"已执行"，Failed→"失败"，其他→"等待"。
        /// </summary>
        public string StatusText => Status switch
        {
            HandleStatus.Running => "执行中",
            HandleStatus.Success => "已执行",
            HandleStatus.Failed => "失败",
            _ => "等待"
        };

        /// <summary>执行结果的格式化文本，包含关键输出值（如缺陷数量、面积等）或错误消息。</summary>
        public string ResultData { get; init; }
    }
}
