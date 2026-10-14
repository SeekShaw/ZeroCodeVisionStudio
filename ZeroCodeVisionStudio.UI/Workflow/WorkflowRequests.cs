using ZeroCodeVisionStudio.Shared;

namespace ZeroCodeVisionStudio.UI.Controls
{
    /// <summary>
    /// 工作流节点创建请求。
    /// 从工具箱拖拽算子到画布松开时发起，携带算子注册模型及在画布上的目标坐标 (X, Y)，由 ViewModel 创建节点并加入工作流。
    /// </summary>
    public class WorkflowNodeCreateRequest
    {
        public WorkflowNodeCreateRequest(IHandleRegisterModel handle, double x, double y)
        {
            Handle = handle;
            X = x;
            Y = y;
        }

        /// <summary>算子注册信息模型。</summary>
        public IHandleRegisterModel Handle { get; }

        /// <summary>放置在画布上的水平 X 坐标。</summary>
        public double X { get; }

        /// <summary>放置在画布上的垂直 Y 坐标。</summary>
        public double Y { get; }
    }

    /// <summary>
    /// 工作流节点移动请求。
    /// 鼠标在画布上拖拽节点时持续发送，由 ViewModel 更新节点坐标并刷新关联连线路由。
    /// </summary>
    public class WorkflowNodeMoveRequest
    {
        public WorkflowNodeMoveRequest(IHandleRunModel node, double x, double y)
        {
            Node = node;
            X = x;
            Y = y;
        }

        /// <summary>被拖动移动的节点运行模型。</summary>
        public IHandleRunModel Node { get; }

        /// <summary>节点新的水平 X 坐标。</summary>
        public double X { get; }

        /// <summary>节点新的垂直 Y 坐标。</summary>
        public double Y { get; }
    }

    /// <summary>
    /// 工作流节点连线请求。
    /// 从源节点的输出端连接到目标节点的输入端时发起，由 ViewModel 建立正式连线。
    /// </summary>
    public class WorkflowConnectionRequest
    {
        public WorkflowConnectionRequest(IHandleRunModel from, IHandleRunModel to)
        {
            From = from;
            To = to;
        }

        /// <summary>连线起始源节点（输出端）。</summary>
        public IHandleRunModel From { get; }

        /// <summary>连线目标节点（输入端）。</summary>
        public IHandleRunModel To { get; }
    }

    /// <summary>
    /// 工作流输入绑定配置请求。
    /// 用于将目标节点的指定输入槽绑定到某个上游节点的具体输出项。
    /// </summary>
    public class WorkflowInputBindingRequest
    {
        public WorkflowInputBindingRequest(WorkflowInputBinding binding, WorkflowOutputOption option)
        {
            Binding = binding;
            Option = option;
        }

        /// <summary>需要配置的目标输入槽绑定模型。</summary>
        public WorkflowInputBinding Binding { get; }

        /// <summary>所选的上游输出选项（若为 null 或未绑定表示解除绑定）。</summary>
        public WorkflowOutputOption Option { get; }
    }
}
