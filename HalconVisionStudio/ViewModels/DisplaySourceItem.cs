using HalconVisionStudio.Shared;

namespace HalconVisionStudio
{
    /// <summary>
    /// 图像显示面板中的一个显示源条目，关联一个节点及其指定输出槽。
    /// <para>
    /// 支持显示/隐藏切换和顺序调整。当 <see cref="IsVisible"/> 或 <see cref="Order"/> 发生变化时，
    /// 会触发 <see cref="StateChanged"/> 事件，通知 <see cref="MainViewModel"/> 重新渲染图像显示面板。
    /// </para>
    /// </summary>
    public class DisplaySourceItem : BindableBase
    {
        /// <summary>当 <see cref="IsVisible"/> 或 <see cref="Order"/> 发生变化时触发，供外部监听并刷新显示。</summary>
        public event EventHandler StateChanged;

        private bool isVisible = true;
        private int order;
        private bool isAutoAdded;

        /// <summary>
        /// 构造函数。
        /// </summary>
        /// <param name="node">数据来源的运行节点。</param>
        /// <param name="output">要显示的具体输出槽描述对象。</param>
        /// <param name="order">在显示面板中的初始排列顺序（从 0 开始）。</param>
        public DisplaySourceItem(IHandleRunModel node, DisplayOutputItem output, int order)
        {
            Node = node;
            Output = output;
            this.order = order;
        }

        /// <summary>数据来源的运行节点。</summary>
        public IHandleRunModel Node { get; }

        /// <summary>要显示的具体输出槽描述对象，包含键名和可读名称。</summary>
        public DisplayOutputItem Output { get; }

        /// <summary>是否为单节点执行时自动添加的临时显示源（非用户手动添加）。</summary>
        public bool IsAutoAdded
        {
            get => isAutoAdded;
            set => SetProperty(ref isAutoAdded, value);
        }

        /// <summary>
        /// 在图像显示面板中的排列顺序（从 0 开始）。
        /// 修改后触发 <see cref="StateChanged"/> 事件。
        /// </summary>
        public int Order
        {
            get => order;
            set
            {
                if (SetProperty(ref order, value))
                {
                    StateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// 是否在图像显示面板中可见。
        /// 修改后触发 <see cref="StateChanged"/> 事件，触发重新渲染。
        /// </summary>
        public bool IsVisible
        {
            get => isVisible;
            set
            {
                if (SetProperty(ref isVisible, value))
                {
                    StateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// 条目的显示名称，优先使用 <see cref="Output"/> 的组合名称，
        /// 其次回退到 <see cref="Node"/> 的显示名称。
        /// </summary>
        public string DisplayName => Output?.DisplayName ?? Node?.DisplayName;
    }
}
