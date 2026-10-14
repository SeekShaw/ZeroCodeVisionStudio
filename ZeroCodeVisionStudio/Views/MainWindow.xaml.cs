using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using MahApps.Metro.Controls;
using ZeroCodeVisionStudio.Shared;

namespace ZeroCodeVisionStudio
{
    /// <summary>
    /// 主窗口代码隐藏。
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private MainViewModel _vm;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            _vm = viewModel;
            Loaded += MainWindow_Loaded;

            // 订阅 ZoomFitRequested — Fit 需要用 Viewport 实际尺寸 + 图像实际尺寸计算
            _vm.ZoomFitRequested += OnZoomFitRequested;
        }

        /// <summary>
        /// 图像画布鼠标滚轮缩放：Ctrl+滚轮 = 精确缩放（以光标为中心），纯滚轮也支持。
        /// </summary>
        private void ImageViewportBorder_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_vm == null || !ImageViewportBorder.IsMouseDirectlyOver) return;

            if (e.Delta > 0)
            {
                _vm.DisplayScale = Math.Min(_vm.DisplayScale * 1.15, 16.0);
            }
            else
            {
                _vm.DisplayScale = Math.Max(_vm.DisplayScale / 1.15, 0.05);
            }
            e.Handled = true;
        }

        /// <summary>
        /// 自适应窗口：用 Viewport 尺寸和图像 Bitmap 尺寸计算最大 Fit Scale。
        /// </summary>
        private void OnZoomFitRequested(object sender, System.EventArgs e)
        {
            if (_vm == null || _vm.DisplayBitmap == null) { _vm.DisplayScale = 1.0; return; }

            var viewportSize = ImageViewportBorder.RenderSize;
            if (viewportSize.Width <= 0 || viewportSize.Height <= 0) { _vm.DisplayScale = 1.0; return; }

            var img = _vm.DisplayBitmap;
            double imgW = img.PixelWidth, imgH = img.PixelHeight;
            if (imgW <= 0 || imgH <= 0) { _vm.DisplayScale = 1.0; return; }

            // 减去一些 padding（Border Margin 10px + 内部 grid 大约留一点）
            double vw = Math.Max(viewportSize.Width - 20, 10);
            double vh = Math.Max(viewportSize.Height - 20, 10);

            double scaleW = vw / imgW;
            double scaleH = vh / imgH;
            double fit = Math.Min(scaleW, scaleH);

            _vm.DisplayScale = Math.Clamp(fit, 0.05, 16.0);
        }

        /// <summary>
        /// 应用完全启动后再 probe OpenCV — 避免启动早期 native DLL 路径未就绪导致 probe 失败。
        /// 同时订阅工具箱 IsExpanded 变化，联动调整 Col0 宽度，让收起后右侧区域能自适应占满。
        /// </summary>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // OpenCV probe 延迟到这里执行（UI 完全加载后，AppContext.BaseDirectory 已正确）。
            // 失败时不锁死 — HandleBase.Execute() 里会在算子第一次执行时再次 probe。
            if (!OpenCvRuntimeProbe.EnsureProbed())
            {
                Shared.Diagnostics.ExecutionLogService.App(
                    $"OpenCV 运行时不可用：{OpenCvRuntimeProbe.ProbeError}",
                    Shared.Diagnostics.ExecLogLevel.Warn);
            }

            // 订阅 ToolboxPanel.IsExpanded 变化 → 动态调整 Col0 宽度
            var dpd = DependencyPropertyDescriptor.FromProperty(
                ZeroCodeVisionStudio.UI.Controls.ToolboxPanel.IsExpandedProperty,
                typeof(ZeroCodeVisionStudio.UI.Controls.ToolboxPanel));
            dpd.AddValueChanged(MainToolboxPanel, OnToolboxExpandedChanged);

            // 应用持久化的工具箱启动状态（用户在系统设置里改的"启动时展开"）
            MainToolboxPanel.IsExpanded = AppWorkspaceSettings.ToolboxStartExpanded;
            ApplyToolboxColumnWidth(MainToolboxPanel.IsExpanded);

            // 注意：竖栏宽度现在由 DockPanel.Dock="Left" Width="52" 天然锁死在画布区域左边缘，
            // 不再需要 Timer 或 GridSplitter 参与。拖两个 Splitter 只影响 Star↔Star 列。

            Loaded -= MainWindow_Loaded;
        }

        private void OnToolboxExpandedChanged(object? sender, EventArgs e)
        {
            ApplyToolboxColumnWidth(MainToolboxPanel.IsExpanded);
        }

        private void ApplyToolboxColumnWidth(bool expanded)
        {
            if (expanded)
            {
                // 展开时固定宽度，避免 Star 占满全屏 → 以最小可完整显示的宽度呈现
                ToolboxColumn.Width = new GridLength(300, GridUnitType.Pixel);
                ToolboxColumn.MinWidth = 280;
            }
            else
            {
                ToolboxColumn.Width = new GridLength(52, GridUnitType.Pixel);
                ToolboxColumn.MinWidth = 52;
            }
        }

        /// <summary>
        /// 主题切换按钮点击事件。
        /// </summary>
        private void ToggleThemeButton_Click(object sender, RoutedEventArgs e)
        {
            AppThemeManager.ToggleTheme();
        }
    }
}
