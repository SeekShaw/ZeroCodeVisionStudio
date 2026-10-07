using HalconVisionStudio.Shared;

namespace HalconVisionStudio.UI.Controls
{
    /// <summary>
    /// 工作流画布上连接两个节点的有向连线。
    /// <para>
    /// 连线从上游节点（<see cref="From"/>）的底部中心出发，到达下游节点（<see cref="To"/>）的顶部中心，
    /// 路径为 SVG 折线格式，存储在 <see cref="RouteData"/> 中，直接绑定到 XAML 的 <c>Path.Data</c>。
    /// </para>
    /// <para>路由策略（<see cref="Refresh"/> 中实现）：</para>
    /// <list type="bullet">
    ///   <item>若下游节点顶部距上游节点底部足够远（&gt; <see cref="RouteGap"/>）且无水平重叠，走中点水平段折线。</item>
    ///   <item>若下游节点顶部距上游节点底部足够远但有水平重叠，走固定距离水平段折线。</item>
    ///   <item>其他情况（下游节点在上游节点旁边或上方），绕过两节点的最大下边界再折回。</item>
    /// </list>
    /// </summary>
    public class WorkflowConnection : BindableBase
    {
        /// <summary>节点卡片宽度（像素），与 XAML 样式中的节点宽度保持一致。</summary>
        public const double NodeWidth = 156;

        /// <summary>节点卡片高度（像素），与 XAML 样式中的节点高度保持一致。</summary>
        public const double NodeHeight = 52;

        /// <summary>节点水平中心偏移量（NodeWidth / 2），用于计算连线出发点和到达点的 X 坐标。</summary>
        public const double NodeCenterX = NodeWidth / 2;

        /// <summary>连线竖直方向的最小弯曲控制距离（像素）。</summary>
        public const double RouteGap = 52;

        /// <summary>并行连线的横向间距（保留，当前版本未启用多通道布线）。</summary>
        public const double RouteLaneGap = 28;

        private double x1;
        private double y1;
        private double x2;
        private double y2;
        private string routeData;
        private string decorData;
        private bool isSelected;

        /// <summary>
        /// 构造并立即计算连线路径。
        /// </summary>
        /// <param name="from">上游（数据来源）节点。</param>
        /// <param name="to">下游（数据消费）节点。</param>
        public WorkflowConnection(IHandleRunModel from, IHandleRunModel to)
        {
            From = from;
            To = to;
            Refresh();
        }

        /// <summary>上游节点（数据来源）。</summary>
        public IHandleRunModel From { get; }

        /// <summary>下游节点（数据消费）。</summary>
        public IHandleRunModel To { get; }

        /// <summary>连线起点 X 坐标（上游节点底部中心）。</summary>
        public double X1 { get => x1; private set => SetProperty(ref x1, value); }

        /// <summary>连线起点 Y 坐标（上游节点底部）。</summary>
        public double Y1 { get => y1; private set => SetProperty(ref y1, value); }

        /// <summary>连线终点 X 坐标（下游节点顶部中心）。</summary>
        public double X2 { get => x2; private set => SetProperty(ref x2, value); }

        /// <summary>连线终点 Y 坐标（下游节点顶部）。</summary>
        public double Y2 { get => y2; private set => SetProperty(ref y2, value); }

        /// <summary>贝塞尔曲线 Path Data（M 起点 + C 控制点），绑定到主线条 Path.Data。</summary>
        public string RouteData { get => routeData; private set => SetProperty(ref routeData, value); }

        /// <summary>装饰几何（终点箭头 + 两端端口圆点，封闭图形），绑定到填充装饰 Path.Data。</summary>
        public string DecorData { get => decorData; private set => SetProperty(ref decorData, value); }

        /// <summary>连线是否处于选中状态，选中时在 XAML 中高亮显示。</summary>
        public bool IsSelected { get => isSelected; set => SetProperty(ref isSelected, value); }

        /// <summary>
        /// 根据当前两端节点的位置重新计算连线路径，写入 <see cref="RouteData"/> 与 <see cref="DecorData"/>。
        /// 路由为平滑三次贝塞尔曲线：控制点沿端口垂直方向伸出，
        /// 无论下游节点在下方、侧方还是上方都能自然回勾，无折角。
        /// 节点移动后由 <see cref="WorkflowViewModel.RefreshConnections"/> 批量调用此方法。
        /// </summary>
        public void Refresh()
        {
            X1 = From.X + NodeCenterX;
            Y1 = From.Y + NodeHeight;
            X2 = To.X + NodeCenterX;
            Y2 = To.Y;

            // 弯曲强度随两端距离自适应：近了收敛、远了舒展，限制在 28~90px。
            var bend = Math.Max(28, Math.Min(90, Math.Abs(Y2 - Y1) * 0.45 + Math.Abs(X2 - X1) * 0.08));

            RouteData = $"M {X1:0.##},{Y1:0.##} C {X1:0.##},{Y1 + bend:0.##} {X2:0.##},{Y2 - bend:0.##} {X2:0.##},{Y2:0.##}";

            // 装饰：终点箭头（沿端口垂直向下指入）+ 起终点端口圆点。
            var arrow = $"M {X2 - 4.5:0.##},{Y2 - 10:0.##} L {X2:0.##},{Y2 - 1:0.##} L {X2 + 4.5:0.##},{Y2 - 10:0.##} Z";
            var dot1 = CircleGeometry(X1, Y1, 3.2);
            var dot2 = CircleGeometry(X2, Y2, 3.2);
            DecorData = $"{arrow} {dot1} {dot2}";
        }

        /// <summary>生成 SVG 圆的 Path Data（两个半圆弧拼合为封闭圆）。</summary>
        private static string CircleGeometry(double cx, double cy, double r)
        {
            return $"M {cx - r:0.##},{cy:0.##} A {r:0.##},{r:0.##} 0 1 1 {cx + r:0.##},{cy:0.##} A {r:0.##},{r:0.##} 0 1 1 {cx - r:0.##},{cy:0.##} Z";
        }
    }
}
