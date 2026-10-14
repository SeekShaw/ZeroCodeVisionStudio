namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 算子执行状态枚举。
    /// 描述流程画布上某个节点在当前执行周期中的状态，用于 UI 显示节点状态图标和颜色。
    /// </summary>
    public enum HandleStatus
    {
        /// <summary>等待执行状态（初始状态，或流程重置后的状态）。</summary>
        Pending,

        /// <summary>正在运行状态（算子正在执行图像处理逻辑）。</summary>
        Running,

        /// <summary>执行成功状态（算子正常完成处理）。</summary>
        Success,

        /// <summary>执行失败状态（算子执行过程中抛出异常）。</summary>
        Failed
    }
}
