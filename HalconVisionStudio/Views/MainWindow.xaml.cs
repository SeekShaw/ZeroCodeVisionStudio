using MahApps.Metro.Controls;
using HalconVisionStudio.Shared;

namespace HalconVisionStudio
{
    /// <summary>
    /// 主窗口代码隐藏。
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        /// <summary>
        /// 主题切换按钮点击事件。
        /// </summary>
        private void ToggleThemeButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            AppThemeManager.ToggleTheme();
        }
    }
}
