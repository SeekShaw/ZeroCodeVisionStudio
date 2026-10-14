using System.Windows;
using System.Windows.Controls;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 通用「输入与参数绑定」编辑区：由对话框窗口（MetroDialogWindow）挂载在算子视图下方，
    /// 对通用参数对话框与全部自定义算子视图生效。数据来自 <see cref="ParameterBindingAmbient"/>。
    /// </summary>
    public partial class ParameterBindingSection : UserControl
    {
        public ParameterBindingSection()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 绑定到环境上下文登记的当前编辑节点（通用对话框内嵌场景使用）。
        /// </summary>
        public void BindToCurrent()
        {
            BindTo(ParameterBindingAmbient.CurrentEditNode);
        }

        /// <summary>
        /// 绑定到当前编辑节点并构建行；无可绑定项（无输入槽且无可写参数）时隐藏自身。
        /// </summary>
        public void BindTo(IHandleRunModel node)
        {
            var candidates = ParameterBindingAmbient.IsAvailable
                ? ParameterBindingAmbient.GetCandidates?.Invoke(node)
                : null;

            var rows = candidates != null && candidates.Count > 0
                ? ParameterBindingEditorBuilder.Build(node, candidates)
                : null;

            if (rows == null || rows.Count == 0)
            {
                // 连同宿主 TabItem 一起隐藏：绑定区内嵌在页签里，只折叠自身
                // 会留下一个点开空白的页签头。
                HideWithHostTab();
                return;
            }

            RowsHost.ItemsSource = rows;
            Visibility = Visibility.Visible;
        }

        /// <summary>折叠自身并隐藏宿主 TabItem。</summary>
        private void HideWithHostTab()
        {
            Visibility = Visibility.Collapsed;
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(this);
            while (parent != null)
            {
                if (parent is TabItem hostTab)
                {
                    hostTab.Visibility = Visibility.Collapsed;
                    return;
                }
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
        }
    }
}
