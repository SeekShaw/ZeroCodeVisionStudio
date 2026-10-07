using System.Windows;

namespace HalconVisionStudio.Views
{
    /// <summary>
    /// 系统设置窗口：执行设置（并行执行 / 节点超时）与界面主题。
    /// DataContext 为主程序 MainViewModel（主题属性与执行设置的宿主）。
    /// </summary>
    public partial class SystemSettingsWindow
    {
        public SystemSettingsWindow()
        {
            InitializeComponent();
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
