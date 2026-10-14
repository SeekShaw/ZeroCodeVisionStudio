using ZeroCodeVisionStudio.Shared;

namespace ZeroCodeVisionStudio
{
    /// <summary>
    /// 表示某个节点的某个输出槽，用于在"图像显示"面板的下拉列表中选择要显示的图像/区域。
    /// <para>
    /// 由 <see cref="MainViewModel"/> 在用户选中节点时根据其 <see cref="RunParameter"/> 动态生成，
    /// 绑定到"添加显示源"下拉框。
    /// </para>
    /// </summary>
    public class DisplayOutputItem : BindableBase
    {
        /// <summary>
        /// 构造函数。
        /// </summary>
        /// <param name="node">输出槽所属的运行节点。</param>
        /// <param name="outputKey">输出槽的键名（参见 <see cref="RunOutputKeys"/>）。</param>
        /// <param name="outputName">输出槽的可读名称，如"输出图像/区域"、"灰度图像"。</param>
        public DisplayOutputItem(IHandleRunModel node, string outputKey, string outputName)
        {
            Node = node;
            OutputKey = outputKey;
            OutputName = outputName;
        }

        /// <summary>输出槽所属的运行节点。</summary>
        public IHandleRunModel Node { get; }

        /// <summary>输出槽的键名（参见 <see cref="RunOutputKeys"/>）。</summary>
        public string OutputKey { get; }

        /// <summary>输出槽的可读名称，如"输出图像/区域"、"灰度图像"。</summary>
        public string OutputName { get; }

        /// <summary>
        /// 用于 UI 展示的组合名称，格式为"节点显示名 / 输出名"。
        /// 例如："读取图像 / 原始图像"。
        /// </summary>
        public string DisplayName => $"{Node?.DisplayName} / {OutputName}";
    }
}
