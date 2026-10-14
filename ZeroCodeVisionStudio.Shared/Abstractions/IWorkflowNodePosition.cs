namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 流程节点坐标接口。
    /// 提供画布节点位置的最小约定，便于统一处理节点拖拽和布局逻辑。
    /// </summary>
    public interface IWorkflowNodePosition
    {
        /// <summary>节点在流程画布上的水平坐标（像素，相对于画布左上角）。</summary>
        double X { get; set; }

        /// <summary>节点在流程画布上的垂直坐标（像素，相对于画布左上角）。</summary>
        double Y { get; set; }
    }
}
