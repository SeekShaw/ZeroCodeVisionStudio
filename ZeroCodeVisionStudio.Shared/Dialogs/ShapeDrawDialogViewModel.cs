using System;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using HalconDotNet;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 交互式形状绘制对话框 ViewModel。
    /// <para>
    /// 使用 HALCON 内置的 <c>DrawCircle</c> / <c>DrawRectangle1</c> / <c>DrawRectangle2</c> / <c>DrawEllipse</c>
    /// 进行交互式绘制，并将绘制结果自动写回算子参数属性。
    /// </para>
    /// <remarks>
    /// 对话框由 <see cref="IDialogParameters"/> 中的以下键值初始化：
    /// <list type="bullet">
    ///   <item><see cref="Common.HandleParameter_ParamName"/>：算子参数实例。</item>
    ///   <item><see cref="Common.HandleParameter_HandleName"/>：算子实例。</item>
    ///   <item><see cref="Common.HandleParameter_RunModel"/>：运行模型（用于获取上游图像）。</item>
    ///   <item><c>ShapeType</c>：形状类型字符串（"circle" / "rectangle1" / "rectangle2" / "ellipse"）。</item>
    /// </list>
    /// </remarks>
    /// </summary>
    public class ShapeDrawDialogViewModel : HandleDialogBase
    {
        private IHandle handle;
        private IHandleParameter parameter;
        private IHandleRunModel runModel;
        private HWindow drawHWindow;
        private HObject drawnShape;
        private string shapeType; // circle / rectangle1 / rectangle2 / ellipse / point2 / line / line2 / pointLine
        private string drawHint;
        private string message;

        /// <summary>通过反射自动加载的参数属性项列表，显示坐标、半径等数值参数。</summary>
        public ObservableCollection<ParameterPropertyItem> Properties { get; } = new();

        /// <summary>嵌入对话框的 HALCON 智能窗口控件，用于显示背景图像和绘制结果。</summary>
        public HSmartWindowControlWPF HWindow
        {
            get => field;
            private set => SetProperty(ref field, value);
        }

        /// <summary>绘制操作的交互提示文本，例如"按住左键拖动，右键结束"。</summary>
        public string DrawHint
        {
            get => drawHint;
            private set => SetProperty(ref drawHint, value);
        }

        /// <summary>执行结果或错误消息，绑定到对话框底部的状态文本框。</summary>
        public string Message
        {
            get => message;
            private set => SetProperty(ref message, value);
        }

        /// <summary>
        /// "绘制"按钮命令，点击后进入 HALCON 交互式绘制模式。
        /// 由于 HALCON Draw* 方法为同步阻塞调用，内部使用后台线程执行以避免冻结 UI。
        /// </summary>
        public ICommand DrawCommand { get; private set; }

        /// <inheritdoc/>
        /// <summary>
        /// 对话框打开时，初始化图像窗口、获取参数和算子引用、订阅图像事件，并加载属性列表。
        /// </summary>
        public override void OnDialogOpened(IDialogParameters parameters)
        {
            if (HalconRuntimeProbe.IsAvailable)
            {
                HWindow = new HSmartWindowControlWPF
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
            }

            parameter = parameters.GetValue<IHandleParameter>(Common.HandleParameter_ParamName);
            handle = parameters.GetValue<IHandle>(Common.HandleParameter_HandleName);
            runModel = parameters.GetValue<IHandleRunModel>(Common.HandleParameter_RunModel);
            shapeType = parameters.GetValue<string>(Common.ShapeDrawModeKey) ?? "rectangle1";

            if (handle != null)
                handle.OnImageDisplay += OnImageDisplay;

            if (HWindow != null)
            {
                HWindow.HInitWindow += (s, e) =>
                {
                    drawHWindow = HWindow.HalconWindow;
                    if (drawHWindow != null)
                    {
                        drawHWindow.SetDraw("margin");
                        drawHWindow.SetColor("lime green");

                        // 优先显示上游节点的输出图像作为绘制背景。
                        var upstreamImage = GetUpstreamImage();
                        if (HalconDisplayHelper.IsValidObject(upstreamImage))
                        {
                            HalconDisplayHelper.SafeDispObj(upstreamImage, drawHWindow);
                        }
                        else if (HalconDisplayHelper.IsValidObject(handle?.RunParameter?.HOutput))
                        {
                            HalconDisplayHelper.SafeDispObj(handle.RunParameter.HOutput, drawHWindow);
                        }
                    }
                };
            }

            DrawCommand = new DelegateCommand(OnDraw);
            LoadProperties();

            DrawHint = $"Draw {GetShapeName()} in the left window. Click Draw to start.";
        }

        /// <summary>
        /// 从上游绑定节点中获取第一张有效的背景图像（HOutput > GrayImage > RawImage）。
        /// </summary>
        /// <returns>可用的背景 <see cref="HObject"/> 图像；若无则返回 null。</returns>
        private HObject GetUpstreamImage()
        {
            if (runModel?.InputBindings == null)
                return null;

            foreach (var binding in runModel.InputBindings)
            {
                if (binding.SourceNode == null)
                    continue;

                var sourceParam = binding.SourceNode.Handle?.RunParameter;
                if (sourceParam == null)
                    continue;

                if (sourceParam.HOutput != null)
                    return sourceParam.HOutput;
                if (sourceParam.GrayImage != null)
                    return sourceParam.GrayImage;
                if (sourceParam.RawImage != null)
                    return sourceParam.RawImage;
            }

            return null;
        }

        /// <inheritdoc/>
        /// <summary>
        /// 对话框关闭时，取消图像事件订阅、释放绘制的形状对象和图像窗口资源。
        /// </summary>
        public override void OnDialogClosed()
        {
            if (handle != null)
                handle.OnImageDisplay -= OnImageDisplay;

            DisposeShape();
            HWindow?.Dispose();
        }

        /// <summary>
        /// 点击"绘制"按钮后，调用 HALCON 原生 Draw* 方法进行交互式绘制。
        /// <para>
        /// HALCON Draw* 方法为同步阻塞调用，放在后台线程执行以避免 UI 卡顿。
        /// 绘制完成后，将结果坐标/尺寸写入参数属性，并在窗口中显示绘制结果。
        /// 用户右键单击或按 Esc 可取消绘制，此时会捕获 <see cref="HOperatorException"/> 并给出提示。
        /// </para>
        /// </summary>
        private async void OnDraw()
        {
            DrawHint = $"Drawing {GetShapeName()}... Drag with the left mouse button, right-click to finish.";
            Message = "";

            try
            {
                // 清除之前的预览形状。
                DisposeShape();

                // HALCON Draw* 是同步阻塞方法，放到后台线程以避免冻结 UI。
                await System.Threading.Tasks.Task.Run(() =>
                {
                    HTuple r, c, phi, l1, l2;

                    switch (shapeType)
                    {
                        case "circle":
                            HOperatorSet.SetDraw(drawHWindow, "margin");
                            HOperatorSet.SetColor(drawHWindow, "cyan");
                            HOperatorSet.DrawCircle(drawHWindow, out r, out c, out l1);
                            HOperatorSet.GenCircle(out drawnShape, r, c, l1);
                            SetParamDouble("Row", r.D);
                            SetParamDouble("Column", c.D);
                            SetParamDouble("Radius", l1.D);
                            break;

                        case "rectangle1":
                            HOperatorSet.SetDraw(drawHWindow, "margin");
                            HOperatorSet.SetColor(drawHWindow, "cyan");
                            HOperatorSet.DrawRectangle1(drawHWindow, out var r1, out var c1, out var r2, out var c2);
                            HOperatorSet.GenRectangle1(out drawnShape, r1, c1, r2, c2);
                            SetParamDouble("Row1", r1.D);
                            SetParamDouble("Column1", c1.D);
                            SetParamDouble("Row2", r2.D);
                            SetParamDouble("Column2", c2.D);
                            break;

                        case "rectangle2":
                            HOperatorSet.SetDraw(drawHWindow, "margin");
                            HOperatorSet.SetColor(drawHWindow, "cyan");
                            HOperatorSet.DrawRectangle2(drawHWindow, out r, out c, out phi, out l1, out l2);
                            HOperatorSet.GenRectangle2(out drawnShape, r, c, phi, l1, l2);
                            SetParamDouble("Row", r.D);
                            SetParamDouble("Column", c.D);
                            SetParamDouble("Phi", phi.D);
                            SetParamDouble("Length1", l1.D);
                            SetParamDouble("Length2", l2.D);
                            break;

                        case "ellipse":
                            HOperatorSet.SetDraw(drawHWindow, "margin");
                            HOperatorSet.SetColor(drawHWindow, "cyan");
                            HOperatorSet.DrawEllipse(drawHWindow, out r, out c, out phi, out l1, out l2);
                            HOperatorSet.GenEllipse(out drawnShape, r, c, phi, l1, l2);
                            SetParamDouble("Row", r.D);
                            SetParamDouble("Column", c.D);
                            SetParamDouble("Phi", phi.D);
                            SetParamDouble("Radius1", l1.D);
                            SetParamDouble("Radius2", l2.D);
                            break;

                        case "point2":
                            DrawPoint("P1", "Row1", "Col1", out var point1);
                            DrawPoint("P2", "Row2", "Col2", out var point2);
                            HOperatorSet.ConcatObj(point1, point2, out drawnShape);
                            point1.Dispose();
                            point2.Dispose();
                            break;

                        case "line":
                            DrawLine("Row1", "Col1", "Row2", "Col2", out drawnShape);
                            break;

                        case "line2":
                            DrawLine("Line1Row1", "Line1Col1", "Line1Row2", "Line1Col2", out var line1);
                            DrawLine("Line2Row1", "Line2Col1", "Line2Row2", "Line2Col2", out var line2);
                            HOperatorSet.ConcatObj(line1, line2, out drawnShape);
                            line1.Dispose();
                            line2.Dispose();
                            break;

                        case "pointLine":
                            DrawPoint("P", "PointRow", "PointCol", out var point);
                            DrawLine("LineRow1", "LineCol1", "LineRow2", "LineCol2", out var line);
                            HOperatorSet.ConcatObj(point, line, out drawnShape);
                            point.Dispose();
                            line.Dispose();
                            break;
                    }
                });

                RefreshProperties();

                if (HalconDisplayHelper.IsValidObject(drawnShape) && drawHWindow != null)
                {
                    HWindow.Dispatcher.Invoke(() =>
                    {
                        drawHWindow.SetDraw("margin");
                        drawHWindow.SetColor("lime green");
                        HalconDisplayHelper.SafeDispObj(drawnShape, drawHWindow);
                    });
                }

                DrawHint = $"Drawing completed: {GetShapeName()} was generated and parameters were updated.";
            }
            catch (HalconDotNet.HOperatorException ex)
            {
                DrawHint = $"绘制取消或出错：{ex.Message}";
            }
            catch (Exception ex)
            {
                DrawHint = $"Error: {ex.Message}";
            }
        }

        private void DrawPoint(string label, string rowProperty, string columnProperty, out HObject pointCross)
        {
            HOperatorSet.SetColor(drawHWindow, "cyan");
            HOperatorSet.DrawPoint(drawHWindow, out var row, out var column);
            HOperatorSet.GenCrossContourXld(out pointCross, row, column, 16, Math.PI / 4.0);
            SetParamDouble(rowProperty, row.D);
            SetParamDouble(columnProperty, column.D);
            HOperatorSet.SetColor(drawHWindow, "yellow");
            HOperatorSet.DispText(drawHWindow, label, "image", row + 8, column + 8, "yellow", "box", "false");
        }

        private void DrawLine(string row1Property, string column1Property, string row2Property, string column2Property, out HObject lineContour)
        {
            HOperatorSet.SetColor(drawHWindow, "cyan");
            HOperatorSet.DrawLine(drawHWindow, out var row1, out var column1, out var row2, out var column2);
            HOperatorSet.GenContourPolygonXld(out lineContour, new HTuple(row1, row2), new HTuple(column1, column2));
            SetParamDouble(row1Property, row1.D);
            SetParamDouble(column1Property, column1.D);
            SetParamDouble(row2Property, row2.D);
            SetParamDouble(column2Property, column2.D);
        }

        /// <inheritdoc/>
        /// <summary>
        /// 点击"执行"时，通过 <see cref="DialogExecutionHelper"/> 自动补充上游执行后再运行当前算子。
        /// </summary>
        protected override void OnExecute()
        {
            Message = DialogExecutionHelper.TryExecuteUpstreamThenSelf(handle);
        }

        /// <inheritdoc/>
        /// <summary>
        /// 点击"重置"时，还原参数、刷新属性列表并清除已绘制的形状预览。
        /// </summary>
        protected override void OnReset()
        {
            parameter?.Reset();
            RefreshProperties();
            DisposeShape();
            Message = "Parameters have been reset.";
        }

        /// <summary>
        /// 通过反射加载参数中类型为 <see langword="double"/>、<see langword="int"/>、
        /// <see langword="float"/> 或 <see langword="string"/> 的可读写属性，填充 <see cref="Properties"/> 集合。
        /// </summary>
        private void LoadProperties()
        {
            Properties.Clear();
            if (parameter == null) return;

            foreach (var prop in parameter.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0))
            {
                var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                if (type == typeof(string) ||
                    type == typeof(byte) ||
                    type == typeof(sbyte) ||
                    type == typeof(short) ||
                    type == typeof(ushort) ||
                    type == typeof(int) ||
                    type == typeof(uint) ||
                    type == typeof(long) ||
                    type == typeof(float) ||
                    type == typeof(double))
                    Properties.Add(new ParameterPropertyItem(parameter, prop));
            }
        }

        /// <summary>
        /// 刷新 <see cref="Properties"/> 集合中每个属性项，使 UI 显示最新的参数值。
        /// </summary>
        private void RefreshProperties()
        {
            foreach (var item in Properties)
                item.Refresh();
        }

        /// <summary>
        /// 通过反射将 <see langword="double"/> 值写入参数对象的指定属性（支持可空类型）。
        /// </summary>
        /// <param name="propName">属性名称，如 "Row"、"Column"、"Radius" 等。</param>
        /// <param name="value">要写入的浮点值。</param>
        private void SetParamDouble(string propName, double value)
        {
            if (parameter == null) return;
            var prop = parameter.GetType().GetProperty(propName);
            if (prop != null && prop.CanWrite && (prop.PropertyType == typeof(double) || prop.PropertyType == typeof(double?)))
                prop.SetValue(parameter, value);
        }

        /// <summary>
        /// 释放当前绘制的形状 <see cref="HObject"/> 资源，防止 HALCON 内存泄漏。
        /// </summary>
        private void DisposeShape()
        {
            if (drawnShape != null)
            {
                drawnShape.Dispose();
                drawnShape = null;
            }
        }

        /// <summary>
        /// 将形状类型字符串转换为中文显示名称，用于交互提示文本。
        /// </summary>
        /// <returns>可读的形状中文名称，如"圆形"、"矩形"等。</returns>
        private string GetShapeName() => shapeType switch
        {
            "circle" => "圆形",
            "rectangle1" => "矩形",
            "rectangle2" => "旋转矩形",
            "ellipse" => "椭圆",
            "point2" => "两点",
            "line" => "直线",
            "line2" => "两条直线",
            "pointLine" => "点和直线",
            _ => "形状"
        };

        /// <summary>
        /// 算子触发 <see cref="IHandle.OnImageDisplay"/> 事件时的回调。
        /// 先清窗口，显示背景图像，再叠加算子结果。
        /// 直接通过 HWindow.HalconWindow 获取句柄，避免 drawHWindow 时序问题。
        /// </summary>
        private void OnImageDisplay(object sender, ImageDisplayEventArgs e)
        {
            if (HWindow == null || e?.Image == null) return;
            HWindow.Dispatcher.Invoke(() =>
            {
                var hwin = HWindow.HalconWindow;
                if (hwin == null) return;

                HOperatorSet.ClearWindow(hwin);

                var bgImage = GetUpstreamImage() ?? handle?.RunParameter?.HOutput;
                if (HalconDisplayHelper.IsValidObject(bgImage))
                    HalconDisplayHelper.SafeDispObj(bgImage, hwin);

                if (HalconDisplayHelper.IsValidObject(e?.Image) && !ReferenceEquals(e.Image, bgImage))
                    HalconDisplayHelper.SafeDispObj(e.Image, hwin);
                HWindow.SetFullImagePart();
            });
        }
    }
}
