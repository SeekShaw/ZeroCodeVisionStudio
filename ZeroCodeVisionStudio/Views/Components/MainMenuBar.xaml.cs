using System.Windows.Controls;
using ZeroCodeVisionStudio;
using ZeroCodeVisionStudio.Shared;
using ZeroCodeVisionStudio.Views.Communication;
using ZeroCodeVisionStudio.Views.Logic;

namespace ZeroCodeVisionStudio.Views.Components;

public partial class MainMenuBar : UserControl
{
    private CommunicationManagerWindow communicationManagerWindow;
    private SystemSettingsWindow systemSettingsWindow;
    private VariableManagerWindow variableManagerWindow;

    public MainMenuBar()
    {
        InitializeComponent();
    }

    private void OnNewSchemeClicked(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.NewSchemeCommand.Execute();
            return;
        }

        System.Windows.MessageBox.Show(
            "主窗口方案命令尚未就绪，无法新建方案。",
            "新建方案",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);
    }

    /// <summary>打开全局变量管理窗口（已打开时置前）。</summary>
    private void OnOpenVariableManager(object sender, System.Windows.RoutedEventArgs e)
    {
        if (variableManagerWindow == null || !variableManagerWindow.IsLoaded)
        {
            variableManagerWindow = new VariableManagerWindow
            {
                Owner = System.Windows.Window.GetWindow(this)
            };
            variableManagerWindow.Show();
        }
        else
        {
            variableManagerWindow.Activate();
        }
    }

    /// <summary>打开执行日志窗口（当前版本暂未提供独立窗口，保持可用的降级提示）。</summary>
    private void OnOpenExecutionLog(object sender, System.Windows.RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show(
            "执行日志查看窗体尚未实现，当前可通过日志服务/运行输出查看执行记录。",
            "执行日志",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    /// <summary>打开系统设置窗口（已打开时置前）。</summary>
    private void OnOpenSystemSettings(object sender, System.Windows.RoutedEventArgs e)
    {
        if (systemSettingsWindow == null || !systemSettingsWindow.IsLoaded)
        {
            systemSettingsWindow = new SystemSettingsWindow
            {
                Owner = System.Windows.Window.GetWindow(this),
                DataContext = DataContext
            };
            systemSettingsWindow.Show();
        }
        else
        {
            systemSettingsWindow.Activate();
        }
    }

    /// <summary>打开通讯驱动管理窗口（已打开时置前）。</summary>
    private void OnOpenCommunicationManager(object sender, System.Windows.RoutedEventArgs e)
    {
        if (communicationManagerWindow == null || !communicationManagerWindow.IsLoaded)
        {
            communicationManagerWindow = new CommunicationManagerWindow
            {
                Owner = System.Windows.Window.GetWindow(this)
            };
            communicationManagerWindow.Show();
        }
        else
        {
            communicationManagerWindow.Activate();
        }
    }
    /// <summary>打开快捷键帮助说明。</summary>
    private void OnOpenShortcutsHelp(object sender, System.Windows.RoutedEventArgs e)
    {
        var msg = "【全局常用快捷键指南】\n\n" +
                  "• 方案操作：\n" +
                  "   Ctrl + N : 新建方案\n" +
                  "   Ctrl + O : 打开方案\n" +
                  "   Ctrl + S : 保存方案\n" +
                  "   Ctrl + Shift + S : 方案另存为\n\n" +
                  "• 编辑与画布：\n" +
                  "   Ctrl + Z : 撤销\n" +
                  "   Ctrl + Y : 重做\n" +
                  "   Delete : 删除选中节点\n" +
                  "   Back : 删除选中连线\n" +
                  "   Ctrl + L : 自动拓扑重排（缩短连线）\n" +
                  "   Ctrl + + / - : 放大 / 缩小画布\n" +
                  "   Ctrl + 0 : 还原 100% 缩放\n\n" +
                  "• 流程与执行：\n" +
                  "   Ctrl + T : 新建工作流\n" +
                  "   F5 : 运行当前流程\n" +
                  "   F6 : 单步执行选中节点\n" +
                  "   F7 : 连续循环运行\n" +
                  "   Shift + F5 : 停止流程\n\n" +
                  "• AI 智能助手：\n" +
                  "   Ctrl + Space : 展开 / 收起 AI 抽屉";
        System.Windows.MessageBox.Show(msg, "快捷键指南 - Vision Studio", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    /// <summary>打开关于对话框。</summary>
    private void OnOpenAboutDialog(object sender, System.Windows.RoutedEventArgs e)
    {
        var msg = $"{AppVersion.ProductName} {AppVersion.Display}\n\n" +
                  "工业机器视觉低代码集成与智能分析平台\n" +
                  "• 核心架构：WPF + .NET 10 + MVVM\n" +
                  "• 视觉算子：Halcon + OpenCV 算子双引擎 & 插件化管理\n" +
                  "• 智能协同：Vision Copilot (OpenAI / DeepSeek / GLM / MCP Server)\n" +
                  "• 工业通讯：PLC (S7 / Modbus / MC) · 串口 · TCP/UDP\n" +
                  $"• 完整版本：{AppVersion.ProductVersion}\n\n" +
                  $"{AppVersion.Copyright}";
        System.Windows.MessageBox.Show(msg, $"关于 - {AppVersion.ProductName}", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

}
