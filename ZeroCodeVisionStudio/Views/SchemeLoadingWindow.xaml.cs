using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media;

namespace ZeroCodeVisionStudio.Views
{
    public partial class SchemeLoadingWindow : Window
    {
        private Storyboard rotationStoryboard;
        private Storyboard pulseStoryboard;

        public SchemeLoadingWindow(string message)
        {
            InitializeComponent();
            UpdateMessage(message);
            Loaded += (_, __) => StartAnimations();
            Unloaded += (_, __) => StopAnimations();
        }

        public void UpdateMessage(string message)
        {
            MessageText.Text = string.IsNullOrWhiteSpace(message) ? "正在加载方案..." : message;
        }

        private void StartAnimations()
        {
            // 旋转动画
            rotationStoryboard = new Storyboard();
            var rotationAnimation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(1.0),
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(rotationAnimation, LoadingRotate);
            Storyboard.SetTargetProperty(rotationAnimation, new PropertyPath(RotateTransform.AngleProperty));
            rotationStoryboard.Children.Add(rotationAnimation);
            rotationStoryboard.Begin();

            // 脉冲动画
            pulseStoryboard = new Storyboard();
            var pulseAnimation = new DoubleAnimation
            {
                From = 0.3,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(0.5),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(pulseAnimation, LoadingPulse);
            Storyboard.SetTargetProperty(pulseAnimation, new PropertyPath(OpacityProperty));
            pulseStoryboard.Children.Add(pulseAnimation);
            pulseStoryboard.Begin();
        }

        private void StopAnimations()
        {
            rotationStoryboard?.Stop();
            pulseStoryboard?.Stop();
        }
    }
}
