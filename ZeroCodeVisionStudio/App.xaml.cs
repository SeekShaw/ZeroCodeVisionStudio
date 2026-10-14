using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ZeroCodeVisionStudio.Bootstrap;
using ZeroCodeVisionStudio.Core;
using ZeroCodeVisionStudio.UI.Dialogs;
using ZeroCodeVisionStudio.Shared;
using ZeroCodeVisionStudio.Views;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Mvvm;

namespace ZeroCodeVisionStudio
{
    /// <summary>
    /// 应用程序入口类，继承自 Prism 的 <see cref="PrismApplication"/>。
    /// </summary>
    public partial class App : PrismApplication
    {
        private Views.SplashScreen splashScreen;

        // UI 异常弹框限流状态（同类异常 10 秒窗口）
        private string lastUiExceptionKey;
        private DateTime lastUiExceptionAt = DateTime.MinValue;
        private int suppressedUiExceptions;

        /// <summary>
        /// 更新启动屏的进度条和状态文字，并短暂暂停以让动画可见。
        /// </summary>
        private void UpdateSplash(double progress, string status)
        {
            splashScreen?.UpdateProgress(progress, status);
            splashScreen?.PauseForNextStep();
        }

        /// <inheritdoc/>
        protected override void OnStartup(StartupEventArgs e)
        {
            // Halcon 运行时可用性主动探测（避免首次使用 Halcon 算子时才触发探测延迟）。
            // 探测本身已 try-catch 保护，不会因 Halcon 缺失导致启动崩溃。
            try { HalconRuntimeProbe.EnsureProbed(); } catch { }

            // 初始化主题管理器
            AppThemeManager.Initialize();
            AppWorkspaceSettings.Initialize();

            splashScreen = new Views.SplashScreen();
            splashScreen.Show();
            base.OnStartup(e);
        }

        /// <inheritdoc/>
        protected override void ConfigureViewModelLocator()
        {
            base.ConfigureViewModelLocator();
            ViewModelLocationProvider.Register<MainWindow, MainViewModel>();
        }

        /// <inheritdoc/>
        protected override Window CreateShell()
        {
            Application.Current.DispatcherUnhandledException += (s, e) =>
            {
                try
                {
                    Shared.Diagnostics.ExecutionLogService.App(
                        $"UI 未处理异常: {e.Exception.Message}", Shared.Diagnostics.ExecLogLevel.Error, e.Exception.ToString());
                    Shared.Diagnostics.ExecutionLogService.Flush();
                }
                catch { }

                // 限流：同类异常 10 秒窗口内只弹一次（第 2 次起计数，窗口结束时合并提示），
                // 避免重复异常（集合错位/触发风暴）导致弹框洪水卡死应用。
                var now = DateTime.UtcNow;
                var key = e.Exception?.GetType().FullName ?? "unknown";
                if (key == lastUiExceptionKey && now - lastUiExceptionAt < TimeSpan.FromSeconds(10))
                {
                    suppressedUiExceptions++;
                    e.Handled = true;
                    return;
                }

                if (suppressedUiExceptions > 0)
                {
                    var fullError0 = GetFullExceptionMessage(e.Exception) +
                        $"{Environment.NewLine}{Environment.NewLine}（此前 {suppressedUiExceptions} 个同类异常已自动抑制）";
                    MessageBox.Show(fullError0, "应用程序异常", MessageBoxButton.OK, MessageBoxImage.Error);
                    suppressedUiExceptions = 0;
                }
                else
                {
                    var fullError = GetFullExceptionMessage(e.Exception);
                    MessageBox.Show(fullError, "应用程序异常", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                lastUiExceptionKey = key;
                lastUiExceptionAt = now;
                e.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    var fullError = GetFullExceptionMessage(ex);
                    MessageBox.Show(fullError, "未处理异常", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                if (e.Exception is Exception ex)
                {
                    var fullError = GetFullExceptionMessage(ex);
                    MessageBox.Show(fullError, "未观察到的任务异常", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            try
            {
                return Container.Resolve<MainWindow>();
            }
            catch (Exception ex)
            {
                var fullError = GetFullExceptionMessage(ex);
                MessageBox.Show(fullError, "初始化主窗口失败", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        /// <inheritdoc/>
        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            UpdateSplash(0.1, "Loading modules...");
            base.ConfigureModuleCatalog(moduleCatalog);
            PluginModuleRegistry.RegisterModules(moduleCatalog, UpdateSplash);
        }

        /// <inheritdoc/>
        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            UpdateSplash(0.95, "Registering services...");

            containerRegistry.RegisterSingleton<IHandleControl, ControlCenter>();
            containerRegistry.RegisterSingleton<MainViewModel>();
            containerRegistry.RegisterSingleton<MainWindow>();

            containerRegistry.RegisterDialogWindow<MetroDialogWindow>();
            containerRegistry.RegisterDialog<NoParameterPreviewDialogView, NoParameterPreviewDialogViewModel>(Common.NoParameterPreviewDialogName);
            containerRegistry.RegisterDialog<ParameterDialogView, ParameterDialogViewModel>(Common.ParameterDialogName);
            containerRegistry.RegisterDialog<ShapeDrawDialogView, ShapeDrawDialogViewModel>(Common.ShapeDrawDialogName);
            containerRegistry.RegisterForNavigation<MainWindow, MainViewModel>();
        }

        /// <inheritdoc/>
        protected override void InitializeShell(Window shell)
        {
            base.InitializeShell(shell);
            splashScreen?.UpdateProgress(1.0, "Starting application...");
            Task.Run(async () =>
            {
                if (splashScreen != null)
                {
                    await splashScreen.DelayUntilMinimumVisibleAsync();
                }

                Dispatcher.Invoke(() =>
                {
                    splashScreen?.Close();
                    shell.Show();
                });
            });
        }

        private static string GetFullExceptionMessage(Exception ex)
        {
            if (ex == null) return "未知异常";
            var sb = new StringBuilder();
            var current = ex;
            while (current != null)
            {
                sb.AppendLine($"[{current.GetType().Name}] {current.Message}");
                if (!string.IsNullOrWhiteSpace(current.StackTrace))
                {
                    sb.AppendLine(current.StackTrace);
                }
                current = current.InnerException;
                if (current != null)
                {
                    sb.AppendLine("--- 内部原因 (Inner Exception) ---");
                }
            }
            return sb.ToString();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Shared.Diagnostics.ExecutionLogService.App("应用退出");
            Shared.Diagnostics.ExecutionLogService.Flush();
            // 断开全部通讯驱动（TCP/UDP/串口长连接）。
            Shared.Communication.CommunicationCenter.ShutdownAll();
            base.OnExit(e);
        }
    }
}
