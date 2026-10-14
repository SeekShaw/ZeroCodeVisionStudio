using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ZeroCodeVisionStudio.Shared;
using HalconDotNet;

namespace ZeroCodeVisionStudio.UI.Controls
{
    /// <summary>
    /// 嵌入 HALCON 窗口的复合自定义控件，提供圆形/矩形1/旋转矩形的交互式绘制功能。
    /// <para>
    /// 控件在 XAML 模板中包含以下命名部件：
    /// <list type="bullet">
    ///   <item><c>PART_HWINDOW</c>：<see cref="HSmartWindowControlWPF"/>，用于显示和绘制图像。</item>
    ///   <item><c>PART_CIRCLE</c>：绘制圆形的菜单项。</item>
    ///   <item><c>PART_RECTANGLE1</c>：绘制矩形1（水平框）的菜单项。</item>
    ///   <item><c>PART_RECTANGLE2</c>：绘制旋转矩形的菜单项。</item>
    ///   <item><c>PART_CLEAR</c>：清除当前形状的菜单项。</item>
    ///   <item><c>PART_MSG</c>：状态消息文本框。</item>
    /// </list>
    /// </para>
    /// <remarks>
    /// 绘制完成后，<see cref="DrawShapeType"/> 和 <see cref="DrawParameters"/> 依赖属性
    /// 会更新为对应形状类型和坐标参数，供外部 ViewModel 通过数据绑定读取。
    /// </remarks>
    /// </summary>
    public class HalOperateWindow : Control
    {
        private const string PART_CLEAR = nameof(PART_CLEAR);
        private const string PART_MSG = nameof(PART_MSG);
        private const string PART_CIRCLE = nameof(PART_CIRCLE);
        private const string PART_HWINDOW = nameof(PART_HWINDOW);
        private const string PART_RECTANGLE1 = nameof(PART_RECTANGLE1);
        private const string PART_RECTANGLE2 = nameof(PART_RECTANGLE2);
        private HSmartWindowControlWPF hwindowControl;
        private HWindow hWindow;

        /// <summary>绘制形状的坐标参数（只读依赖属性键）。</summary>
        public static readonly DependencyPropertyKey DrawParametersPropertyKey = DependencyProperty.RegisterReadOnly(nameof(DrawParameters), typeof(HTuple[]), typeof(HalOperateWindow), new PropertyMetadata(Array.Empty<HTuple>()));

        /// <summary>当前绘制的形状类型（只读依赖属性键）。</summary>
        public static readonly DependencyPropertyKey DrawShapeTypePropertyKey = DependencyProperty.RegisterReadOnly(nameof(DrawShapeType), typeof(EShapeType), typeof(HalOperateWindow), new PropertyMetadata(EShapeType.undefined));

        /// <summary>
        /// 要显示在 HALCON 窗口中的图像对象依赖属性。
        /// 属性值变更时，自动调用 <see cref="HOperatorSet.DispObj"/> 刷新显示。
        /// </summary>
        public static readonly DependencyProperty HImageProperty = DependencyProperty.Register(nameof(HImage), typeof(HObject), typeof(HalOperateWindow), new PropertyMetadata(OnImageChangedCallback));

        /// <summary>底层 HALCON 窗口句柄（只读依赖属性键），在模板应用后由 <c>HWindow_Loaded</c> 回调赋值。</summary>
        public static readonly DependencyPropertyKey HWindowPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HWindow), typeof(HWindow), typeof(HalOperateWindow), new PropertyMetadata(null));

        /// <summary><see cref="DrawParametersPropertyKey"/> 对应的公开只读依赖属性。</summary>
        public static readonly DependencyProperty DrawParametersProperty = DrawParametersPropertyKey.DependencyProperty;

        /// <summary><see cref="DrawShapeTypePropertyKey"/> 对应的公开只读依赖属性。</summary>
        public static readonly DependencyProperty DrawShapeTypeProperty = DrawShapeTypePropertyKey.DependencyProperty;

        /// <summary><see cref="HWindowPropertyKey"/> 对应的公开只读依赖属性。</summary>
        public static readonly DependencyProperty HWindowProperty = HWindowPropertyKey.DependencyProperty;

        private TextBlock txt_msg = null;

        /// <summary>
        /// 析构函数：取消订阅 <c>Loaded</c> 事件并释放 <see cref="HImage"/> 资源，防止 HALCON 内存泄漏。
        /// </summary>
        ~HalOperateWindow()
        {
            if (hwindowControl != null)
            {
                hwindowControl.Loaded -= Hwindow_Loaded;
            }
            HImage?.Dispose();
            HImage = null;
        }

        /// <summary>绘制完成后的形状坐标参数数组。绑定的视图可以读取此属性获取绘制结果。</summary>
        public HTuple[] DrawParameters
        {
            get { return (HTuple[])GetValue(DrawParametersProperty); }
            private set { SetValue(DrawParametersProperty, value); }
        }

        /// <summary>当前绘制的形状类型，绘制完成后更新。</summary>
        public EShapeType DrawShapeType
        {
            get { return (EShapeType)GetValue(DrawShapeTypeProperty); }
            private set { SetValue(DrawShapeTypeProperty, value); }
        }

        /// <summary>
        /// 要显示的 HALCON 图像/区域对象。
        /// 赋值时通过 <see cref="OnImageChangedCallback"/> 自动触发 HALCON 窗口刷新。
        /// </summary>
        public HObject HImage
        {
            get { return (HObject)GetValue(HImageProperty); }
            set { SetValue(HImageProperty, value); }
        }

        /// <summary>底层 HALCON 窗口句柄，仅在 HALCON 智能控件加载完成后可用。</summary>
        public HWindow HWindow
        {
            get { return (HWindow)GetValue(HWindowProperty); }
            private set { SetValue(HWindowProperty, value); }
        }

        /// <summary>
        /// <see cref="HImage"/> 属性变更时的回调，将新图像显示到 HALCON 窗口。
        /// </summary>
        private static void OnImageChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HalOperateWindow win && e.NewValue is HObject obj && HalconDisplayHelper.IsValidObject(obj) && win.HWindow != null)
            {
                HalconDisplayHelper.SafeDispObj(obj, win.HWindow);
            }
        }

        /// <summary>
        /// 绘制圆形按钮单击处理。
        /// 临时禁用缩放，在后台线程调用 <see cref="HOperatorSet.DrawCircle"/> 等待用户绘制，
        /// 完成后生成圆形区域并更新依赖属性。
        /// </summary>
        private async void bnCircle_Click(object sender, RoutedEventArgs e)
        {
            hwindowControl.HZoomContent = HSmartWindowControlWPF.ZoomContent.Off;
            HTuple row = default, col = default, radius = default;
            await Task.Run(() =>
            {
                HOperatorSet.DrawCircle(hWindow, out row, out col, out radius);
            });
            HOperatorSet.GenCircle(out var image, row, col, radius);
            SetValue(HImageProperty, image);
            SetValue(DrawShapeTypePropertyKey, EShapeType.rectangle2);
            SetValue(DrawParametersPropertyKey, new HTuple[] { row, col, radius });
            hwindowControl.HZoomContent = HSmartWindowControlWPF.ZoomContent.WheelForwardZoomsIn;
        }

        /// <summary>
        /// 绘制矩形1（水平轴对齐矩形）按钮单击处理。
        /// </summary>
        private async void bnRectangle1_Click(object sender, RoutedEventArgs e)
        {
            hwindowControl.HZoomContent = HSmartWindowControlWPF.ZoomContent.Off;
            HTuple row1 = default, col1 = default, row2 = default, col2 = default;
            await Task.Run(() =>
            {
                HOperatorSet.DrawRectangle1(hWindow, out row1, out col1, out row2, out col2);
            });
            HOperatorSet.GenRectangle1(out var image, row1, col1, row2, col2);
            SetValue(HImageProperty, image);
            SetValue(DrawShapeTypePropertyKey, EShapeType.rectangle1);
            SetValue(DrawParametersPropertyKey, new HTuple[] { row1, col1, row2, col2 });
            hwindowControl.HZoomContent = HSmartWindowControlWPF.ZoomContent.WheelForwardZoomsIn;
        }

        /// <summary>
        /// 绘制旋转矩形2（带角度的矩形）按钮单击处理。
        /// </summary>
        private async void bnRectangle2_Click(object sender, RoutedEventArgs e)
        {
            hwindowControl.HZoomContent = HSmartWindowControlWPF.ZoomContent.Off;
            HTuple row = default, col = default, phi = default, length1 = default, length2 = default;
            await Task.Run(() =>
            {
                HOperatorSet.DrawRectangle2(hWindow, out row, out col, out phi, out length1, out length2);
            });
            HOperatorSet.GenRectangle2(out var image, row, col, phi, length1, length2);
            SetValue(HImageProperty, image);
            SetValue(DrawShapeTypePropertyKey, EShapeType.rectangle2);
            SetValue(DrawParametersPropertyKey, new HTuple[] { row, col, phi, length1, length2 });
            hwindowControl.HZoomContent = HSmartWindowControlWPF.ZoomContent.WheelForwardZoomsIn;
        }

        /// <summary>
        /// 清除按钮单击处理：清空 HALCON 窗口内容，释放图像资源，重置形状类型和参数。
        /// </summary>
        private void bnClear_Click(object sender, RoutedEventArgs e)
        {
            HOperatorSet.ClearWindow(hWindow);
            HImage?.Dispose();
            HImage = null;
            SetValue(DrawParametersPropertyKey, Array.Empty<HTuple>());
            SetValue(DrawShapeTypePropertyKey, EShapeType.undefined);
        }

        /// <summary>
        /// HALCON 智能控件加载完成后，获取底层 <see cref="HWindow"/> 句柄并写入只读依赖属性。
        /// </summary>
        private void Hwindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetValue(HWindowPropertyKey, hwindowControl.HalconWindow);
            hWindow = hwindowControl.HalconWindow;
        }

        /// <inheritdoc/>
        /// <summary>
        /// 模板应用后，从模板中查找各命名部件并绑定对应事件处理方法。
        /// </summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            hwindowControl = GetTemplateChild(PART_HWINDOW) as HSmartWindowControlWPF;
            if (hwindowControl != null)
            {
                hwindowControl.Loaded += Hwindow_Loaded;
            }
            if (GetTemplateChild(PART_RECTANGLE1) is MenuItem item1)
            {
                item1.Click += bnRectangle1_Click;
            }
            if (GetTemplateChild(PART_RECTANGLE2) is MenuItem item2)
            {
                item2.Click += bnRectangle2_Click;
            }
            if (GetTemplateChild(PART_CIRCLE) is MenuItem item3)
            {
                item3.Click += bnCircle_Click;
            }
            if (GetTemplateChild(PART_CLEAR) is MenuItem item4)
            {
                item4.Click += bnClear_Click;
            }
            if (GetTemplateChild(PART_MSG) is TextBlock txt_msg)
            {
                this.txt_msg = txt_msg;
            }
        }
    }
}
