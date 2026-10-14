using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;
using Microsoft.Win32;

namespace Plugin.Handle.CreateShapeModel.ViewModels
{
    public class CreateShapeModelViewModel : HandleDialogBase
    {
        private CreateShapeModelHandle handle;
        private CreateShapeModelParameter parameter;
        private IHandleRunModel runModel;
        private HWindow drawHWindow;
        private HObject compositeRegion;
        private HObject backgroundImage;
        private HObject currentDisplayContours;
        private bool isEraserMode;
        private double eraserRadius = 4;
        private string statusMessage;
        private string drawHint;

        public HSmartWindowControlWPF HWindow { get => field; private set => SetProperty(ref field, value); }

        public bool IsEraserMode
        {
            get => isEraserMode;
            set
            {
                if (SetProperty(ref isEraserMode, value))
                {
                    RaisePropertyChanged(nameof(IsDrawMode));
                }
            }
        }

        public bool IsDrawMode => !IsEraserMode;

        public double EraserRadius
        {
            get => eraserRadius;
            set => SetProperty(ref eraserRadius, Math.Max(1, value));
        }

        public string StatusMessage
        {
            get => statusMessage;
            private set => SetProperty(ref statusMessage, value);
        }

        public string DrawHint
        {
            get => drawHint;
            private set => SetProperty(ref drawHint, value);
        }

        public ObservableCollection<ParameterPropertyItem> Properties { get; } = new();

        public ICommand SetDrawModeCommand { get; private set; }
        public ICommand SetEraseModeCommand { get; private set; }
        public ICommand DrawCircleCommand { get; private set; }
        public ICommand DrawRect1Command { get; private set; }
        public ICommand DrawRect2Command { get; private set; }
        public ICommand ClearRegionCommand { get; private set; }
        public ICommand SaveFileCommand { get; private set; }

        public override void OnDialogOpened(IDialogParameters parameters)
        {
            HWindow = new HSmartWindowControlWPF
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            handle = parameters.GetValue<CreateShapeModelHandle>(Common.HandleParameter_HandleName);
            parameter = parameters.GetValue<CreateShapeModelParameter>(Common.HandleParameter_ParamName);
            runModel = parameters.GetValue<IHandleRunModel>(Common.HandleParameter_RunModel);

            if (handle != null)
            {
                handle.OnImageDisplay += OnImageDisplay;
            }

            HWindow.HInitWindow += (_, _) =>
            {
                drawHWindow = HWindow.HalconWindow;
                if (drawHWindow == null)
                {
                    return;
                }

                HOperatorSet.SetDraw(drawHWindow, "margin");
                HOperatorSet.SetColor(drawHWindow, "lime green");

                backgroundImage = GetUpstreamImage();
                if (backgroundImage == null && handle?.RunParameter != null)
                {
                    backgroundImage = handle.RunParameter.GrayImage
                        ?? handle.RunParameter.RawImage
                        ?? handle.RunParameter.HOutput;
                }

                if (handle?.TemplateRegion != null)
                {
                    compositeRegion = handle.TemplateRegion;
                }

                RedrawDisplay();
                HWindow.SetFullImagePart();
            };

            SetDrawModeCommand = new DelegateCommand(() => IsEraserMode = false);
            SetEraseModeCommand = new DelegateCommand(() => IsEraserMode = true);
            DrawCircleCommand = new DelegateCommand(() => OnDraw("circle"));
            DrawRect1Command = new DelegateCommand(() => OnDraw("rectangle1"));
            DrawRect2Command = new DelegateCommand(() => OnDraw("rectangle2"));
            ClearRegionCommand = new DelegateCommand(OnClearRegion);
            SaveFileCommand = new DelegateCommand<ParameterPropertyItem>(SaveFile);

            LoadProperties();
            DrawHint = "选择绘制形状添加模板区域，或开启橡皮擦擦除不需要的区域。";
        }

        public override void OnDialogClosed()
        {
            if (handle != null)
            {
                handle.OnImageDisplay -= OnImageDisplay;
            }

            if (!ReferenceEquals(handle?.TemplateRegion, compositeRegion))
            {
                compositeRegion?.Dispose();
            }

            currentDisplayContours?.Dispose();
            currentDisplayContours = null;
            compositeRegion = null;
            backgroundImage = null;
            drawHWindow = null;
            HWindow?.Dispose();
        }

        protected override void OnExecute()
        {
            if (handle != null)
            {
                handle.TemplateRegion = compositeRegion;
            }

            StatusMessage = DialogExecutionHelper.TryExecuteUpstreamThenSelf(handle);

            if (backgroundImage == null)
            {
                backgroundImage = GetUpstreamImage();
            }

            var resultMessage = handle?.RunParameter?.ResultMessage;
            if (!string.IsNullOrWhiteSpace(resultMessage))
            {
                StatusMessage = resultMessage;
            }

            RedrawDisplay();
        }

        protected override void OnReset()
        {
            parameter?.Reset();
            foreach (var item in Properties)
            {
                item.Refresh();
            }

            EraserRadius = 4;
            OnClearRegion();
            StatusMessage = "参数已重置。";
        }

        private async void OnDraw(string shapeType)
        {
            if (drawHWindow == null)
            {
                return;
            }

            DrawHint = $"绘制{ShapeName(shapeType)}：拖动左键，点击右键结束。";

            try
            {
                HObject drawnShape = null;
                var previousMoveContent = HWindow.HMoveContent;
                var previousZoomContent = HWindow.HZoomContent;
                var previousDoubleClickToFit = HWindow.HDoubleClickToFitContent;

                await System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        HWindow.Dispatcher.Invoke(() =>
                        {
                            HWindow.HMoveContent = false;
                            HWindow.HZoomContent = HSmartWindowControlWPF.ZoomContent.Off;
                            HWindow.HDoubleClickToFitContent = false;
                        });

                        HOperatorSet.SetDraw(drawHWindow, "fill");
                        HOperatorSet.SetColor(drawHWindow, IsEraserMode ? "red" : "cyan");

                        switch (shapeType)
                        {
                            case "circle":
                                HOperatorSet.DrawCircle(drawHWindow, out var row, out var column, out var radius);
                                HOperatorSet.GenCircle(out drawnShape, row, column, radius);
                                break;
                            case "rectangle1":
                                HOperatorSet.DrawRectangle1(drawHWindow, out var row1, out var column1, out var row2, out var column2);
                                HOperatorSet.GenRectangle1(out drawnShape, row1, column1, row2, column2);
                                break;
                            case "rectangle2":
                                HOperatorSet.DrawRectangle2(drawHWindow, out var rectRow, out var rectColumn, out var phi, out var length1, out var length2);
                                HOperatorSet.GenRectangle2(out drawnShape, rectRow, rectColumn, phi, length1, length2);
                                break;
                        }
                    }
                    finally
                    {
                        HWindow.Dispatcher.Invoke(() =>
                        {
                            HWindow.HMoveContent = previousMoveContent;
                            HWindow.HZoomContent = previousZoomContent;
                            HWindow.HDoubleClickToFitContent = previousDoubleClickToFit;
                        });
                    }
                });

                if (drawnShape == null)
                {
                    return;
                }

                ApplyDrawnShape(drawnShape, IsEraserMode);
                DrawHint = IsEraserMode ? "擦除完成。" : "区域已添加。";
            }
            catch (HOperatorException ex)
            {
                DrawHint = $"绘制取消：{ex.Message}";
                RedrawDisplay();
            }
            catch (Exception ex)
            {
                DrawHint = $"错误：{ex.Message}";
                RedrawDisplay();
            }
        }

        public void EraseAtImagePoint(double row, double column)
        {
            if (!IsEraserMode || compositeRegion == null)
            {
                return;
            }

            HOperatorSet.GenCircle(out var eraser, row, column, Math.Max(1, EraserRadius));
            ApplyDrawnShape(eraser, true);
        }

        public void ImageToWindow(double row, double column, out double windowRow, out double windowColumn)
        {
            windowRow = row;
            windowColumn = column;

            if (drawHWindow == null)
            {
                return;
            }

            drawHWindow.ConvertCoordinatesImageToWindow(row, column, out windowRow, out windowColumn);
        }

        public double ImageRadiusToWindowRadius(double row, double column, double imageRadius)
        {
            if (drawHWindow == null)
            {
                return Math.Max(1, imageRadius);
            }

            drawHWindow.ConvertCoordinatesImageToWindow(row, column, out var centerWindowRow, out var centerWindowColumn);
            drawHWindow.ConvertCoordinatesImageToWindow(row, column + imageRadius, out var edgeWindowRow, out var edgeWindowColumn);
            var rowDelta = edgeWindowRow - centerWindowRow;
            var columnDelta = edgeWindowColumn - centerWindowColumn;
            return Math.Max(1, Math.Sqrt(rowDelta * rowDelta + columnDelta * columnDelta));
        }

        public void WindowToImage(double windowRow, double windowColumn, out double row, out double column)
        {
            row = windowRow;
            column = windowColumn;

            if (drawHWindow == null)
            {
                return;
            }

            drawHWindow.ConvertCoordinatesWindowToImage(windowRow, windowColumn, out row, out column);
        }

        private void ApplyDrawnShape(HObject drawnShape, bool erase)
        {
            if (erase)
            {
                if (compositeRegion != null)
                {
                    HOperatorSet.Difference(compositeRegion, drawnShape, out var erased);
                    if (!ReferenceEquals(handle?.TemplateRegion, compositeRegion))
                    {
                        compositeRegion.Dispose();
                    }
                    compositeRegion = erased;
                    if (handle != null)
                    {
                        handle.TemplateRegion = compositeRegion;
                    }
                }

                drawnShape.Dispose();
            }
            else if (compositeRegion == null)
            {
                compositeRegion = drawnShape;
                if (handle != null)
                {
                    handle.TemplateRegion = compositeRegion;
                }
            }
            else
            {
                HOperatorSet.Union2(compositeRegion, drawnShape, out var united);
                if (!ReferenceEquals(handle?.TemplateRegion, compositeRegion))
                {
                    compositeRegion.Dispose();
                }
                drawnShape.Dispose();
                compositeRegion = united;
                if (handle != null)
                {
                    handle.TemplateRegion = compositeRegion;
                }
            }

            RedrawDisplay();
        }

        private void OnClearRegion()
        {
            if (!ReferenceEquals(handle?.TemplateRegion, compositeRegion))
            {
                compositeRegion?.Dispose();
            }

            compositeRegion = null;
            currentDisplayContours?.Dispose();
            currentDisplayContours = null;
            if (handle != null)
            {
                handle.TemplateRegion = null;
            }

            RedrawDisplay();
            DrawHint = "已清除所有绘制区域。";
        }

        private void OnImageDisplay(object sender, ImageDisplayEventArgs e)
        {
            if (e?.Image == null)
            {
                return;
            }

            var cloned = e.Image.Clone();
            HWindow?.Dispatcher.Invoke(() =>
            {
                currentDisplayContours?.Dispose();
                currentDisplayContours = cloned;
                RedrawDisplay();
            });
        }

        private void RedrawDisplay()
        {
            HWindow?.Dispatcher.Invoke(() =>
            {
                var hwin = HWindow?.HalconWindow;
                if (hwin == null)
                {
                    return;
                }

                HOperatorSet.ClearWindow(hwin);

                if (HalconDisplayHelper.IsValidObject(backgroundImage))
                {
                    HalconDisplayHelper.SafeDispObj(backgroundImage, hwin);
                }

                if (HalconDisplayHelper.IsValidObject(compositeRegion))
                {
                    HOperatorSet.SetDraw(hwin, "margin");
                    HOperatorSet.SetColor(hwin, "lime green");
                    HalconDisplayHelper.SafeDispObj(compositeRegion, hwin);
                }

                DisplayCurrentContours(hwin);
            });
        }

        private void DisplayCurrentContours(HWindow hwin)
        {
            if (currentDisplayContours == null)
            {
                return;
            }

            HObject visibleContours = currentDisplayContours;
            var ownsVisibleContours = false;

            if (compositeRegion != null)
            {
                try
                {
                    HOperatorSet.GenRegionContourXld(currentDisplayContours, out var contourRegion, "margin");
                    HOperatorSet.Intersection(contourRegion, compositeRegion, out var clippedRegion);
                    contourRegion.Dispose();
                    visibleContours = clippedRegion;
                    ownsVisibleContours = true;
                }
                catch
                {
                    visibleContours = currentDisplayContours;
                    ownsVisibleContours = false;
                }
            }

            if (HalconDisplayHelper.IsValidObject(visibleContours))
            {
                HOperatorSet.SetDraw(hwin, "margin");
                HOperatorSet.SetColor(hwin, "red");
                HalconDisplayHelper.SafeDispObj(visibleContours, hwin);
            }

            if (ownsVisibleContours)
            {
                visibleContours.Dispose();
            }
        }

        private HObject GetUpstreamImage()
        {
            var upstreamImg = HalconDisplayHelper.FindUpstreamImage(handle);
            if (HalconDisplayHelper.IsValidObject(upstreamImg))
            {
                return upstreamImg;
            }

            if (runModel?.InputBindings == null)
            {
                return null;
            }

            foreach (var binding in runModel.InputBindings)
            {
                var source = binding.SourceNode?.Handle?.RunParameter;
                if (source == null)
                {
                    continue;
                }

                return source.GrayImage ?? source.RawImage ?? source.HOutput;
            }

            return null;
        }

        private void LoadProperties()
        {
            Properties.Clear();
            if (parameter == null)
            {
                return;
            }

            foreach (var prop in parameter.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0))
            {
                var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                if (type.IsEnum ||
                    type == typeof(string) ||
                    type == typeof(byte) ||
                    type == typeof(sbyte) ||
                    type == typeof(short) ||
                    type == typeof(ushort) ||
                    type == typeof(int) ||
                    type == typeof(uint) ||
                    type == typeof(long) ||
                    type == typeof(float) ||
                    type == typeof(double))
                {
                    Properties.Add(new ParameterPropertyItem(parameter, prop));
                }
            }
        }

        private static string ShapeName(string shapeType) => shapeType switch
        {
            "circle" => "圆形",
            "rectangle1" => "矩形",
            "rectangle2" => "旋转矩形",
            _ => "形状"
        };

        /// <summary>
        /// 保存文件对话框，选择保存路径。
        /// </summary>
        private void SaveFile(ParameterPropertyItem item)
        {
            if (item == null) return;

            var dialog = new SaveFileDialog
            {
                Title = "保存文件",
                Filter = item.FileFilter ?? "Shape Model (*.shm)|*.shm|All files (*.*)|*.*",
                DefaultExt = item.FileDefaultExtension ?? ".shm",
                AddExtension = true,
                RestoreDirectory = true,
                CheckFileExists = false,
                ValidateNames = true
            };

            if (!string.IsNullOrWhiteSpace(item.Value?.ToString()))
            {
                try
                {
                    var dir = System.IO.Path.GetDirectoryName(item.Value.ToString());
                    if (!string.IsNullOrWhiteSpace(dir) && System.IO.Directory.Exists(dir))
                    {
                        dialog.InitialDirectory = dir;
                    }
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(item.Value.ToString());
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    dialog.FileName = string.IsNullOrWhiteSpace(fileName) 
                        ? $"model_{timestamp}" 
                        : $"{fileName}_{timestamp}";
                }
                catch
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    dialog.FileName = $"model_{timestamp}";
                }
            }
            else
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                dialog.FileName = $"model_{timestamp}";
            }

            if (dialog.ShowDialog() == true)
            {
                item.Value = dialog.FileName;
            }
        }
    }
}
