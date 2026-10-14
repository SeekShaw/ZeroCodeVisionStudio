using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;
namespace ZeroCodeVisionStudio.UI.Dialogs
{
    /// <summary>
    /// MetroDialogWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MetroDialogWindow : MetroWindow, IDialogWindow
    {
        public MetroDialogWindow()
        {
            InitializeComponent();
            Loaded += MetroDialogWindow_Loaded;
        }

        public IDialogResult Result { get; set; }

        private void MetroDialogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (Owner != null)
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            if (Content is FrameworkElement content && !string.IsNullOrWhiteSpace(content.GetType().Name))
            {
                Title = content.GetType().Name
                    .Replace("View", string.Empty)
                    .Replace("Dialog", string.Empty);
            }

            AttachBindingSection();
        }

        /// <summary>
        /// 激活算子视图中内嵌的「输入与参数绑定」编辑区数据。
        /// 注意：不做窗口级包装——参数对话框内可能再开嵌套对话框（如几何算子的
        /// ROI 绘制窗），包装会把嵌套小窗也套上绑定页签；所有参数视图已内嵌绑定区。
        /// </summary>
        private void AttachBindingSection()
        {
            try
            {
                if (Content is not FrameworkElement view) return;
                FindBindingSection(view)?.BindToCurrent();
            }
            catch
            {
                // 激活失败不影响对话框本身
            }
        }

        private static ZeroCodeVisionStudio.Shared.ParameterBindingSection FindBindingSection(DependencyObject root)
        {
            if (root == null) return null;
            if (root is ZeroCodeVisionStudio.Shared.ParameterBindingSection section) return section;

            // 必须走逻辑树：绑定区在 TabControl 未选中页签内时不在视觉树中，
            // 走视觉树会误判"视图未自带绑定区"而再包一层窗口级 Tab（双重页签）。
            foreach (var child in System.Windows.LogicalTreeHelper.GetChildren(root))
            {
                if (child is DependencyObject childElement &&
                    FindBindingSection(childElement) is { } found)
                {
                    return found;
                }
            }
            return null;
        }
    }
}

