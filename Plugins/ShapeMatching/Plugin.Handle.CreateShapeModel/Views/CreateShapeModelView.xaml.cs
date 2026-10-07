using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HalconDotNet;
using Plugin.Handle.CreateShapeModel.ViewModels;

namespace Plugin.Handle.CreateShapeModel.Views
{
    public partial class CreateShapeModelView : UserControl
    {
        private bool isErasing;

        public CreateShapeModelView()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                if (DataContext is CreateShapeModelViewModel viewModel && viewModel.HWindow != null)
                {
                    viewModel.HWindow.HMouseDown += HWindow_HMouseDown;
                    viewModel.HWindow.HMouseMove += HWindow_HMouseMove;
                    viewModel.HWindow.HMouseUp += HWindow_HMouseUp;
                    viewModel.HWindow.HMouseWheel += HWindow_HMouseWheel;
                    viewModel.HWindow.HMouseDoubleClick += HWindow_HMouseDoubleClick;
                }
            };

            Unloaded += (_, _) =>
            {
                if (DataContext is CreateShapeModelViewModel viewModel && viewModel.HWindow != null)
                {
                    viewModel.HWindow.HMouseDown -= HWindow_HMouseDown;
                    viewModel.HWindow.HMouseMove -= HWindow_HMouseMove;
                    viewModel.HWindow.HMouseUp -= HWindow_HMouseUp;
                    viewModel.HWindow.HMouseWheel -= HWindow_HMouseWheel;
                    viewModel.HWindow.HMouseDoubleClick -= HWindow_HMouseDoubleClick;
                }
            };
        }

        private void EraserCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isErasing = true;
            EraserCanvas.CaptureMouse();
            var point = e.GetPosition(EraserCanvas);
            UpdateCursor(point);
            EraseAt(point);
            e.Handled = true;
        }

        private void EraserCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isErasing = false;
            EraserCanvas.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void EraserCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var point = e.GetPosition(EraserCanvas);
            UpdateCursor(point);

            if (isErasing && e.LeftButton == MouseButtonState.Pressed)
            {
                EraseAt(point);
            }
        }

        private void EraserCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            EraserCursorEllipse.Visibility = Visibility.Collapsed;
            isErasing = false;
            EraserCanvas.ReleaseMouseCapture();
        }

        private void UpdateCursor(Point point)
        {
            if (DataContext is not CreateShapeModelViewModel viewModel || !viewModel.IsEraserMode)
            {
                EraserCursorEllipse.Visibility = Visibility.Collapsed;
                return;
            }

            viewModel.WindowToImage(point.Y, point.X, out var row, out var column);
            var windowRadius = viewModel.ImageRadiusToWindowRadius(row, column, viewModel.EraserRadius);
            var diameter = Math.Max(2, windowRadius * 2);
            EraserCursorEllipse.Width = diameter;
            EraserCursorEllipse.Height = diameter;
            Canvas.SetLeft(EraserCursorEllipse, point.X - diameter / 2);
            Canvas.SetTop(EraserCursorEllipse, point.Y - diameter / 2);
            EraserCursorEllipse.Visibility = Visibility.Visible;
        }

        private void HWindow_HMouseDown(object sender, HSmartWindowControlWPF.HMouseEventArgsWPF e)
        {
            if (!IsEraserContext() || e.Button != MouseButton.Left)
            {
                return;
            }

            isErasing = true;
            EraseAt(e.Row, e.Column);
            UpdateCursorFromImage(e.Row, e.Column);
        }

        private void HWindow_HMouseMove(object sender, HSmartWindowControlWPF.HMouseEventArgsWPF e)
        {
            if (!IsEraserContext())
            {
                return;
            }

            UpdateCursorFromImage(e.Row, e.Column);
            if (isErasing)
            {
                EraseAt(e.Row, e.Column);
            }
        }

        private void HWindow_HMouseUp(object sender, HSmartWindowControlWPF.HMouseEventArgsWPF e)
        {
            isErasing = false;
        }

        private void HWindow_HMouseWheel(object sender, HSmartWindowControlWPF.HMouseEventArgsWPF e)
        {
        }

        private void HWindow_HMouseDoubleClick(object sender, HSmartWindowControlWPF.HMouseEventArgsWPF e)
        {
        }

        private bool IsEraserContext()
        {
            return DataContext is CreateShapeModelViewModel viewModel &&
                viewModel.IsEraserMode &&
                viewModel.HWindow?.HalconWindow != null;
        }

        private void UpdateCursorFromImage(double row, double column)
        {
            if (DataContext is not CreateShapeModelViewModel viewModel)
            {
                return;
            }

            viewModel.ImageToWindow(row, column, out var windowRow, out var windowColumn);
            UpdateCursor(new Point(windowColumn, windowRow));
        }

        private void EraseAt(double row, double column)
        {
            if (DataContext is not CreateShapeModelViewModel viewModel || viewModel.HWindow?.HalconWindow == null)
            {
                return;
            }

            viewModel.EraseAtImagePoint(row, column);
        }

        private void EraseAt(Point point)
        {
            if (DataContext is not CreateShapeModelViewModel viewModel || viewModel.HWindow?.HalconWindow == null)
            {
                return;
            }

            viewModel.WindowToImage(point.Y, point.X, out var row, out var column);
            viewModel.EraseAtImagePoint(row, column);
        }
    }
}
