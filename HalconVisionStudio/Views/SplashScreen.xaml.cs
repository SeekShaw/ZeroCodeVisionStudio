using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace HalconVisionStudio.Views
{
    /// <summary>
    /// 现代化工业级机器视觉启动过渡窗口（SplashScreen）。
    /// <para>
    /// 提供光学镜头旋转、激光雷达扫描、对焦状态呼吸与平滑进度过渡等高科技感动画。
    /// </para>
    /// </summary>
    public partial class SplashScreen : Window
    {
        private readonly DateTime createdAt = DateTime.Now;
        private readonly Random random = new Random();
        private readonly int minimumVisibleMilliseconds;

        public SplashScreen()
        {
            InitializeComponent();
            minimumVisibleMilliseconds = random.Next(1600, 2400);
            StartAnimations();
        }

        /// <summary>
        /// 启动启动屏中所有矢量元素的旋转、扫描和平滑脉冲动画。
        /// </summary>
        private void StartAnimations()
        {
            // 1. 镜头外圆环：顺时针平稳旋转 (14 秒/圈)
            var outerAnim = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(14),
                RepeatBehavior = RepeatBehavior.Forever
            };
            OuterRingRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, outerAnim);

            // 2. 镜头内圆环：逆时针反向旋转 (9 秒/圈)
            var innerAnim = new DoubleAnimation
            {
                From = 360,
                To = 0,
                Duration = TimeSpan.FromSeconds(9),
                RepeatBehavior = RepeatBehavior.Forever
            };
            InnerRingRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, innerAnim);

            // 3. 激光扫描线：垂直往复移动 (-28 ~ +28)
            var scanAnim = new DoubleAnimation
            {
                From = -28,
                To = 28,
                Duration = TimeSpan.FromSeconds(2.0),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            ScannerTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, scanAnim);

            // 4. 对焦点脉冲缩放 (0.8 ~ 1.25)
            var pulseAnim = new DoubleAnimation
            {
                From = 0.8,
                To = 1.25,
                Duration = TimeSpan.FromSeconds(1.6),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            CenterPulseScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, pulseAnim);
            CenterPulseScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, pulseAnim);

            // 5. 右上角状态绿灯呼吸闪烁
            var dotAnim = new DoubleAnimation
            {
                From = 0.4,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(1.2),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            StatusDot.BeginAnimation(OpacityProperty, dotAnim);
        }

        /// <summary>
        /// 在 UI 线程上平滑更新进度条宽度、百分比和描述文字。
        /// 由 <see cref="App.UpdateSplash"/> 在模块注册过程中调用。
        /// </summary>
        /// <param name="progress">进度值，范围 0.0 ~ 1.0。</param>
        /// <param name="status">当前步骤状态描述。</param>
        public void UpdateProgress(double progress, string status)
        {
            Dispatcher.Invoke(() =>
            {
                var clamped = Math.Max(0.0, Math.Min(1.0, progress));

                // 进度条平滑动画过度
                var progressAnim = new DoubleAnimation
                {
                    To = clamped,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                ProgressScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, progressAnim);

                PercentText.Text = $"{(int)(clamped * 100)}%";
                StatusText.Text = status;
                LoadDetailText.Text = BuildDetailText(status);
            });
        }

        /// <summary>
        /// 在当前步骤与下一步骤之间短暂暂停，使加载动画自然流畅。
        /// </summary>
        public void PauseForNextStep()
        {
            WaitWithDispatcher(TimeSpan.FromMilliseconds(random.Next(60, 140)));
        }

        /// <summary>
        /// 计算剩余的最短可见等待时间，防止启动过快导致画面一闪而过。
        /// </summary>
        public Task DelayUntilMinimumVisibleAsync()
        {
            var elapsed = DateTime.Now - createdAt;
            var remaining = minimumVisibleMilliseconds - (int)elapsed.TotalMilliseconds;
            return Task.Delay(Math.Max(0, remaining));
        }

        /// <summary>
        /// 将英文状态字符串转换为可读的专业工业视觉中文描述。
        /// 涵盖解决方案中已合并重构的 12 个分类插件模块。
        /// </summary>
        private static string BuildDetailText(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return "正在初始化机器视觉系统...";

            if (status.Contains("ImageInput", StringComparison.OrdinalIgnoreCase))
                return "正在加载图像采集与多源输入插件...";
            if (status.Contains("Segmentation", StringComparison.OrdinalIgnoreCase))
                return "正在加载图像分割与阈值处理插件...";
            if (status.Contains("Preprocessing", StringComparison.OrdinalIgnoreCase))
                return "正在加载图像预处理与滤波增强插件...";
            if (status.Contains("Measurement", StringComparison.OrdinalIgnoreCase))
                return "正在加载几何测量与卡尺工具插件...";
            if (status.Contains("ShapeMatching", StringComparison.OrdinalIgnoreCase))
                return "正在加载高精度模板匹配与定位插件...";
            if (status.Contains("RegionProcessing", StringComparison.OrdinalIgnoreCase))
                return "正在加载区域运算与形态学分析插件...";
            if (status.Contains("ShapeFeature", StringComparison.OrdinalIgnoreCase))
                return "正在加载形状特征与几何提取插件...";
            if (status.Contains("GrayFeature", StringComparison.OrdinalIgnoreCase))
                return "正在加载灰度特征与统计分析插件...";
            if (status.Contains("Geometry", StringComparison.OrdinalIgnoreCase))
                return "正在加载几何构图与拟合工具插件...";
            if (status.Contains("ContourProcessing", StringComparison.OrdinalIgnoreCase))
                return "正在加载亚像素轮廓处理与提取插件...";
            if (status.Contains("DeviceCommunication", StringComparison.OrdinalIgnoreCase))
                return "正在加载工业通信与设备联动插件...";
            if (status.Contains("ResultOutput", StringComparison.OrdinalIgnoreCase))
                return "正在加载数据呈现与结果输出插件...";

            return status switch
            {
                "Loading modules..." => "正在扫描视觉处理算法模块...",
                "Registering services..." => "正在初始化应用核心服务与容器...",
                "Starting application..." => "正在构建工作流视口并启动主界面...",
                _ => status
            };
        }

        /// <summary>
        /// 使用 <see cref="DispatcherFrame"/> 在不阻塞 UI 消息泵的前提下等待指定时长。
        /// </summary>
        private static void WaitWithDispatcher(TimeSpan delay)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = delay
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                frame.Continue = false;
            };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }
    }
}
