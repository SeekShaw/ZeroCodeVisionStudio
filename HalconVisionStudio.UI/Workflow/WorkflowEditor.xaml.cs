using System;
using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using HalconVisionStudio.Shared;

namespace HalconVisionStudio.UI.Controls
{
    /// <summary>
    /// Workflow canvas control.
    /// </summary>
    public partial class WorkflowEditor : UserControl
    {
        private const double MinWorkflowScale = 0.4;
        private const double MaxWorkflowScale = 2.5;
        private const double ZoomFactor = 1.1;

        private Point dragStartPoint;
        private Point nodeStartPoint;
        private Point panStartPoint;
        private double panStartHorizontalOffset;
        private double panStartVerticalOffset;

        private IHandleRunModel draggedNode;
        private IHandleRunModel pendingConnectionSource;
        private bool isPanning;
        private bool isSelecting;
        private Point selectionStartPoint;

        public WorkflowEditor()
        {
            InitializeComponent();
        }

        // ==================== 画布动态扩展 ====================
        // 画布尺寸不再固定为 2400x1400：随节点分布自动扩展（保留最小尺寸），
        // 节点拖出原边界后依然可达。

        private const double MinCanvasWidth = 2400;
        private const double MinCanvasHeight = 1400;
        private const double CanvasMarginRight = 360;
        private const double CanvasMarginBottom = 240;

        private System.Collections.Specialized.INotifyCollectionChanged observedNodeCollection;
        private readonly List<System.ComponentModel.INotifyPropertyChanged> observedNodes = new();

        private static void OnNodesSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var editor = (WorkflowEditor)d;
            editor.DetachNodeObservers();

            if (e.NewValue is System.Collections.Specialized.INotifyCollectionChanged collection)
            {
                editor.observedNodeCollection = collection;
                collection.CollectionChanged += editor.OnNodesCollectionChanged;
                editor.AttachNodeObservers(editor.Nodes);
            }

            editor.UpdateCanvasExtent();
        }

        private void OnNodesCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            AttachNodeObservers(e.NewItems?.OfType<System.ComponentModel.INotifyPropertyChanged>());
            if (e.OldItems != null)
            {
                foreach (var node in e.OldItems.OfType<System.ComponentModel.INotifyPropertyChanged>())
                {
                    node.PropertyChanged -= OnNodePositionChanged;
                    observedNodes.Remove(node);
                }
            }

            UpdateCanvasExtent();
        }

        private void AttachNodeObservers(System.Collections.IEnumerable nodes)
        {
            if (nodes == null) return;
            foreach (var node in nodes.OfType<System.ComponentModel.INotifyPropertyChanged>())
            {
                if (node == null || observedNodes.Contains(node)) continue;
                node.PropertyChanged += OnNodePositionChanged;
                observedNodes.Add(node);
            }
        }

        private void DetachNodeObservers()
        {
            if (observedNodeCollection != null)
            {
                observedNodeCollection.CollectionChanged -= OnNodesCollectionChanged;
                observedNodeCollection = null;
            }

            foreach (var node in observedNodes)
            {
                node.PropertyChanged -= OnNodePositionChanged;
            }
            observedNodes.Clear();
        }

        private void OnNodePositionChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IWorkflowNodePosition.X) || e.PropertyName == nameof(IWorkflowNodePosition.Y))
            {
                UpdateCanvasExtent();
            }
        }

        /// <summary>按节点最大坐标重算画布与两层 ItemsControl 的尺寸（不小于默认 2400x1400）。</summary>
        private void UpdateCanvasExtent()
        {
            if (WorkflowCanvas == null) return;

            double width = MinCanvasWidth;
            double height = MinCanvasHeight;

            if (Nodes is System.Collections.IEnumerable nodes)
            {
                foreach (var item in nodes.OfType<IWorkflowNodePosition>())
                {
                    width = Math.Max(width, item.X + WorkflowConnection.NodeWidth + CanvasMarginRight);
                    height = Math.Max(height, item.Y + WorkflowConnection.NodeHeight + CanvasMarginBottom);
                }
            }

            WorkflowCanvas.Width = width;
            WorkflowCanvas.Height = height;
            ConnectionsLayer.Width = width;
            ConnectionsLayer.Height = height;
            NodesLayer.Width = width;
            NodesLayer.Height = height;
        }

        public static readonly DependencyProperty NodesProperty =
            DependencyProperty.Register(
                nameof(Nodes),
                typeof(IEnumerable),
                typeof(WorkflowEditor),
                new FrameworkPropertyMetadata(OnNodesSourceChanged));

        public static readonly DependencyProperty ConnectionsProperty =
            DependencyProperty.Register(nameof(Connections), typeof(IEnumerable), typeof(WorkflowEditor));

        public static readonly DependencyProperty WorkflowScaleProperty =
            DependencyProperty.Register(
                nameof(WorkflowScale),
                typeof(double),
                typeof(WorkflowEditor),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty AddNodeCommandProperty =
            DependencyProperty.Register(nameof(AddNodeCommand), typeof(ICommand), typeof(WorkflowEditor));

        public static readonly DependencyProperty MoveNodeCommandProperty =
            DependencyProperty.Register(nameof(MoveNodeCommand), typeof(ICommand), typeof(WorkflowEditor));

        public static readonly DependencyProperty ConnectNodesCommandProperty =
            DependencyProperty.Register(nameof(ConnectNodesCommand), typeof(ICommand), typeof(WorkflowEditor));

        public static readonly DependencyProperty SelectConnectionCommandProperty =
            DependencyProperty.Register(nameof(SelectConnectionCommand), typeof(ICommand), typeof(WorkflowEditor));

        public static readonly DependencyProperty DeleteSelectedConnectionCommandProperty =
            DependencyProperty.Register(nameof(DeleteSelectedConnectionCommand), typeof(ICommand), typeof(WorkflowEditor));

        public static readonly DependencyProperty DeleteConnectionCommandProperty =
            DependencyProperty.Register(nameof(DeleteConnectionCommand), typeof(ICommand), typeof(WorkflowEditor));

        public static readonly DependencyProperty SelectNodeCommandProperty =
            DependencyProperty.Register(nameof(SelectNodeCommand), typeof(ICommand), typeof(WorkflowEditor));

        public static readonly DependencyProperty NodeEditCommandProperty =
            DependencyProperty.Register(nameof(NodeEditCommand), typeof(ICommand), typeof(WorkflowEditor));

        public static readonly DependencyProperty NodeExecuteCommandProperty =
            DependencyProperty.Register(nameof(NodeExecuteCommand), typeof(ICommand), typeof(WorkflowEditor));

        public static readonly DependencyProperty NodeDeleteCommandProperty =
            DependencyProperty.Register(nameof(NodeDeleteCommand), typeof(ICommand), typeof(WorkflowEditor));

        public static readonly DependencyProperty NodeConditionCommandProperty =
            DependencyProperty.Register(nameof(NodeConditionCommand), typeof(ICommand), typeof(WorkflowEditor));

        public static readonly DependencyProperty SetInputBindingCommandProperty =
            DependencyProperty.Register(nameof(SetInputBindingCommand), typeof(ICommand), typeof(WorkflowEditor));

        public static readonly DependencyProperty UndoCommandProperty =
            DependencyProperty.Register(nameof(UndoCommand), typeof(ICommand), typeof(WorkflowEditor));

        public static readonly DependencyProperty RedoCommandProperty =
            DependencyProperty.Register(nameof(RedoCommand), typeof(ICommand), typeof(WorkflowEditor));

        public static readonly DependencyProperty StopExecutionCommandProperty =
            DependencyProperty.Register(nameof(StopExecutionCommand), typeof(ICommand), typeof(WorkflowEditor));

        public static readonly DependencyProperty AutoLayoutCommandProperty =
            DependencyProperty.Register(nameof(AutoLayoutCommand), typeof(ICommand), typeof(WorkflowEditor));

        public IEnumerable Nodes
        {
            get => (IEnumerable)GetValue(NodesProperty);
            set => SetValue(NodesProperty, value);
        }

        public IEnumerable Connections
        {
            get => (IEnumerable)GetValue(ConnectionsProperty);
            set => SetValue(ConnectionsProperty, value);
        }

        public double WorkflowScale
        {
            get => (double)GetValue(WorkflowScaleProperty);
            set => SetValue(WorkflowScaleProperty, value);
        }

        public ICommand AddNodeCommand
        {
            get => (ICommand)GetValue(AddNodeCommandProperty);
            set => SetValue(AddNodeCommandProperty, value);
        }

        public ICommand MoveNodeCommand
        {
            get => (ICommand)GetValue(MoveNodeCommandProperty);
            set => SetValue(MoveNodeCommandProperty, value);
        }

        public ICommand ConnectNodesCommand
        {
            get => (ICommand)GetValue(ConnectNodesCommandProperty);
            set => SetValue(ConnectNodesCommandProperty, value);
        }

        public ICommand SelectConnectionCommand
        {
            get => (ICommand)GetValue(SelectConnectionCommandProperty);
            set => SetValue(SelectConnectionCommandProperty, value);
        }

        public ICommand DeleteSelectedConnectionCommand
        {
            get => (ICommand)GetValue(DeleteSelectedConnectionCommandProperty);
            set => SetValue(DeleteSelectedConnectionCommandProperty, value);
        }

        public ICommand DeleteConnectionCommand
        {
            get => (ICommand)GetValue(DeleteConnectionCommandProperty);
            set => SetValue(DeleteConnectionCommandProperty, value);
        }

        public ICommand SelectNodeCommand
        {
            get => (ICommand)GetValue(SelectNodeCommandProperty);
            set => SetValue(SelectNodeCommandProperty, value);
        }

        public ICommand NodeEditCommand
        {
            get => (ICommand)GetValue(NodeEditCommandProperty);
            set => SetValue(NodeEditCommandProperty, value);
        }

        public ICommand NodeExecuteCommand
        {
            get => (ICommand)GetValue(NodeExecuteCommandProperty);
            set => SetValue(NodeExecuteCommandProperty, value);
        }

        public ICommand NodeDeleteCommand
        {
            get => (ICommand)GetValue(NodeDeleteCommandProperty);
            set => SetValue(NodeDeleteCommandProperty, value);
        }

        public ICommand NodeConditionCommand
        {
            get => (ICommand)GetValue(NodeConditionCommandProperty);
            set => SetValue(NodeConditionCommandProperty, value);
        }

        public ICommand UndoCommand
        {
            get => (ICommand)GetValue(UndoCommandProperty);
            set => SetValue(UndoCommandProperty, value);
        }

        public ICommand RedoCommand
        {
            get => (ICommand)GetValue(RedoCommandProperty);
            set => SetValue(RedoCommandProperty, value);
        }

        public ICommand StopExecutionCommand
        {
            get => (ICommand)GetValue(StopExecutionCommandProperty);
            set => SetValue(StopExecutionCommandProperty, value);
        }

        public ICommand AutoLayoutCommand
        {
            get => (ICommand)GetValue(AutoLayoutCommandProperty);
            set => SetValue(AutoLayoutCommandProperty, value);
        }

        public ICommand SetInputBindingCommand
        {
            get => (ICommand)GetValue(SetInputBindingCommandProperty);
            set => SetValue(SetInputBindingCommandProperty, value);
        }

        /// <summary>
        /// 工具箱算子拖到画布上方时，根据拖拽数据格式判断是否允许投放。
        /// </summary>
        private void WorkflowCanvas_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = GetDraggedHandle(e.Data) != null ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        /// <summary>
        /// 工具箱算子投放到画布时，转换为新增节点请求。
        /// 节点坐标以鼠标位置为中心做偏移，使节点出现在用户投放的位置附近。
        /// </summary>
        private void WorkflowCanvas_Drop(object sender, DragEventArgs e)
        {
            var model = GetDraggedHandle(e.Data);
            if (model == null)
            {
                return;
            }

            var point = e.GetPosition(WorkflowCanvas);
            var x = Math.Max(0, point.X - WorkflowConnection.NodeWidth / 2);
            var y = Math.Max(0, point.Y - WorkflowConnection.NodeHeight / 2);
            Execute(AddNodeCommand, new WorkflowNodeCreateRequest(model, x, y));
            e.Handled = true;
        }

        /// <summary>
        /// 开始拖动节点，同时选中该节点并记录拖动起点。
        /// 支持 Ctrl 键增量选中，非 Ctrl 单击时保留多选状态。
        /// </summary>
        private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            if (IsLeftAltPressed())
            {
                Execute(SelectConnectionCommand, null);
                StartSelection(e);
                e.Handled = true;
                return;
            }

            if ((sender as FrameworkElement)?.DataContext is IHandleRunModel node)
            {
                draggedNode = node;
                bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                if (isCtrl)
                {
                    node.IsSelected = !node.IsSelected;
                    Execute(SelectConnectionCommand, null);
                    if (node.IsSelected)
                    {
                        Execute(SelectNodeCommand, node);
                    }
                }
                else
                {
                    if (!node.IsSelected)
                    {
                        ClearNodeSelection();
                        node.IsSelected = true;
                    }
                    Execute(SelectConnectionCommand, null);
                    Execute(SelectNodeCommand, node);
                }

                dragStartPoint = e.GetPosition(WorkflowCanvas);
                nodeStartPoint = new Point(node.X, node.Y);
                Mouse.Capture(sender as IInputElement);
                e.Handled = true;
            }
        }

        /// <summary>
        /// 节点拖动中持续发送移动请求。
        /// 实际坐标写入和连线刷新由 ViewModel 处理。
        /// </summary>
        private void Node_MouseMove(object sender, MouseEventArgs e)
        {
            if (draggedNode == null || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var current = e.GetPosition(WorkflowCanvas);
            var x = Math.Max(0, nodeStartPoint.X + current.X - dragStartPoint.X);
            var y = Math.Max(0, nodeStartPoint.Y + current.Y - dragStartPoint.Y);
            Execute(MoveNodeCommand, new WorkflowNodeMoveRequest(draggedNode, x, y));
        }

        /// <summary>
        /// 结束节点拖动并释放鼠标捕获。
        /// </summary>
        private void Node_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (draggedNode != null)
            {
                Mouse.Capture(null);
                draggedNode = null;
                e.Handled = true;
            }
        }

        /// <summary>
        /// 从节点底部输出端开始创建临时连线预览。
        /// </summary>
        private void OutputPort_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            pendingConnectionSource = (sender as FrameworkElement)?.DataContext as IHandleRunModel;
            if (pendingConnectionSource != null)
            {
                Mouse.Capture(WorkflowCanvas, CaptureMode.SubTree);
                UpdateConnectionPreview(e.GetPosition(WorkflowCanvas));
            }

            e.Handled = true;
        }

        private void InputPort_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 输入端只用于接收连线，按下时阻止事件冒泡到节点拖动逻辑。
        }

        /// <summary>
        /// 鼠标在输入端释放时，如果存在临时连线来源，则创建正式连线。
        /// </summary>
        private void InputPort_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (pendingConnectionSource != null && (sender as FrameworkElement)?.DataContext is IHandleRunModel target)
            {
                Execute(ConnectNodesCommand, new WorkflowConnectionRequest(pendingConnectionSource, target));
            }

            CancelConnectionPreview();
            e.Handled = true;
        }

        /// <summary>
        /// 单击连线时选中连线，并清除所有节点的选中状态，后续可通过 Delete 或右键菜单删除。
        /// </summary>
        private void Connection_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            if ((sender as FrameworkElement)?.DataContext is WorkflowConnection connection)
            {
                ClearNodeSelection();
                Execute(SelectNodeCommand, null);
                Execute(SelectConnectionCommand, connection);
                e.Handled = true;
            }
        }

        /// <summary>
        /// 打开节点右键菜单时动态生成“输入来源”子菜单。
        /// 候选项基于当前所有节点输出实时构建，节点执行后新增的运行时输出也能出现在菜单中。
        /// </summary>
        private void NodeContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu menu || menu.PlacementTarget is not FrameworkElement target || target.DataContext is not IHandleRunModel node)
            {
                return;
            }

            Execute(SelectNodeCommand, node);
            var bindingMenu = menu.Items
                .OfType<MenuItem>()
                .FirstOrDefault(x => Equals(x.Name, "InputBindingsMenuItem"));
            if (bindingMenu == null)
            {
                return;
            }

            bindingMenu.Items.Clear();
            if (node.InputBindings == null || node.InputBindings.Count == 0)
            {
                bindingMenu.IsEnabled = false;
                bindingMenu.Items.Add(new MenuItem { Header = "当前节点没有可绑定输入", IsEnabled = false });
                return;
            }

            bindingMenu.IsEnabled = true;
            var nodes = Nodes?.Cast<IHandleRunModel>() ?? Enumerable.Empty<IHandleRunModel>();
            foreach (var binding in node.InputBindings)
            {
                var slotMenu = new MenuItem { Header = binding.TargetInputName };
                var options = WorkflowBindingHelper.BuildOutputOptions(nodes, node, binding).ToList();
                foreach (var option in options)
                {
                    // Bind the selected output directly to the input binding.
                    var request = new WorkflowInputBindingRequest(binding, option);
                    var optionItem = new MenuItem
                    {
                        Header = option.DisplayName,
                        Command = SetInputBindingCommand,
                        CommandParameter = request,
                        IsCheckable = true,
                        IsChecked = ReferenceEquals(option.SourceNode, binding.SourceNode) && option.OutputKey == binding.SourceOutputKey
                    };
                    slotMenu.Items.Add(optionItem);
                }

                bindingMenu.Items.Add(slotMenu);
            }
        }

        /// <summary>
        /// 画布空白处按下鼠标左键：
        /// 默认开始矩形框选多选；若按住 Space 或 Alt 键，则开始画布平移。
        /// </summary>
        private void WorkflowCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            if (pendingConnectionSource != null)
            {
                CancelConnectionPreview();
                e.Handled = true;
                return;
            }

            Execute(SelectConnectionCommand, null);
            if (Keyboard.IsKeyDown(Key.Space) || IsLeftAltPressed())
            {
                StartCanvasPan(e);
                e.Handled = true;
                return;
            }

            StartSelection(e);
            e.Handled = true;
        }

        private void WorkflowCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                Focus();
                StartCanvasPan(e);
                e.Handled = true;
            }
        }

        private void WorkflowCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && isPanning)
            {
                StopCanvasPan();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 根据当前交互状态更新框选范围、画布平移或临时连线路径。
        /// </summary>
        private void WorkflowCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isSelecting)
            {
                UpdateSelection(e);
                e.Handled = true;
                return;
            }

            if (isPanning)
            {
                UpdateCanvasPan(e);
                e.Handled = true;
                return;
            }

            if (pendingConnectionSource == null)
            {
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                CancelConnectionPreview();
                return;
            }

            UpdateConnectionPreview(e.GetPosition(WorkflowCanvas));
            e.Handled = true;
        }

        /// <summary>
        /// 画布释放鼠标时完成框选、结束平移或取消未落在输入端的临时连线。
        /// </summary>
        private void WorkflowCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isSelecting)
            {
                CompleteSelection(e);
                e.Handled = true;
                return;
            }

            if (isPanning)
            {
                StopCanvasPan();
                e.Handled = true;
                return;
            }

            if (pendingConnectionSource == null)
            {
                return;
            }

            CancelConnectionPreview();
            e.Handled = true;
        }

        /// <summary>
        /// 鼠标滚轮缩放画布，并保持鼠标指向的画布位置尽量不漂移。
        /// </summary>
        private void WorkflowScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var oldScale = Math.Max(0.1, WorkflowScale);
            var newScale = e.Delta > 0
                ? Math.Min(MaxWorkflowScale, oldScale * ZoomFactor)
                : Math.Max(MinWorkflowScale, oldScale / ZoomFactor);

            if (Math.Abs(newScale - oldScale) < 0.001)
            {
                e.Handled = true;
                return;
            }

            var viewportPoint = e.GetPosition(WorkflowScrollViewer);
            var canvasPoint = e.GetPosition(WorkflowCanvas);
            SetCurrentValue(WorkflowScaleProperty, newScale);
            GetBindingExpression(WorkflowScaleProperty)?.UpdateSource();

            WorkflowScrollViewer.UpdateLayout();
            WorkflowScrollViewer.ScrollToHorizontalOffset(canvasPoint.X * newScale - viewportPoint.X);
            WorkflowScrollViewer.ScrollToVerticalOffset(canvasPoint.Y * newScale - viewportPoint.Y);
            e.Handled = true;
        }

        private void WorkflowEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (pendingConnectionSource != null)
                {
                    CancelConnectionPreview();
                }

                if (isSelecting)
                {
                    CancelSelection();
                }

                // 没有连线/框选可取消时，Esc 用于停止正在执行的流程。
                if (pendingConnectionSource == null && !isSelecting)
                {
                    Execute(StopExecutionCommand, null);
                }

                e.Handled = true;
                return;
            }

            // Ctrl+Z 撤销 / Ctrl+Y 或 Ctrl+Shift+Z 重做。
            if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Execute(UndoCommand, null);
                e.Handled = true;
                return;
            }

            if ((e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control) ||
                (e.Key == Key.Z && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)))
            {
                Execute(RedoCommand, null);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete)
            {
                DeleteSelectedItems();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                var node = GetSelectedNodes().FirstOrDefault();
                if (node != null)
                {
                    Execute(NodeExecuteCommand, node);
                }

                e.Handled = true;
            }
        }

        private void StartSelection(MouseButtonEventArgs e)
        {
            isSelecting = true;
            selectionStartPoint = e.GetPosition(WorkflowCanvas);
            Canvas.SetLeft(SelectionRectangle, selectionStartPoint.X);
            Canvas.SetTop(SelectionRectangle, selectionStartPoint.Y);
            SelectionRectangle.Width = 0;
            SelectionRectangle.Height = 0;
            SelectionRectangle.Visibility = Visibility.Visible;
            WorkflowCanvas.Cursor = Cursors.Cross;
            Mouse.Capture(WorkflowCanvas);
        }

        private void UpdateSelection(MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                CompleteSelection(e);
                return;
            }

            var bounds = GetSelectionBounds(e.GetPosition(WorkflowCanvas));
            SetSelectionRectangle(bounds);

            // 框选过程中动态实时高亮所有与选框相交的节点
            if (Nodes != null)
            {
                foreach (var node in Nodes.Cast<IHandleRunModel>())
                {
                    var nodeRect = new Rect(node.X, node.Y, WorkflowConnection.NodeWidth, WorkflowConnection.NodeHeight);
                    node.IsSelected = bounds.IntersectsWith(nodeRect);
                }
            }
        }

        private void CompleteSelection(MouseEventArgs e)
        {
            var bounds = GetSelectionBounds(e.GetPosition(WorkflowCanvas));
            SelectNodesInBounds(bounds);
            CancelSelection();
        }

        private void CancelSelection()
        {
            isSelecting = false;
            SelectionRectangle.Visibility = Visibility.Collapsed;
            WorkflowCanvas.Cursor = null;

            if (Mouse.Captured == WorkflowCanvas)
            {
                Mouse.Capture(null);
            }
        }

        private Rect GetSelectionBounds(Point current)
        {
            var x = Math.Min(selectionStartPoint.X, current.X);
            var y = Math.Min(selectionStartPoint.Y, current.Y);
            var width = Math.Abs(current.X - selectionStartPoint.X);
            var height = Math.Abs(current.Y - selectionStartPoint.Y);
            return new Rect(x, y, width, height);
        }

        private void SetSelectionRectangle(Rect bounds)
        {
            Canvas.SetLeft(SelectionRectangle, bounds.X);
            Canvas.SetTop(SelectionRectangle, bounds.Y);
            SelectionRectangle.Width = bounds.Width;
            SelectionRectangle.Height = bounds.Height;
        }

        private void SelectNodesInBounds(Rect bounds)
        {
            var allNodes = (Nodes?.Cast<IHandleRunModel>() ?? Enumerable.Empty<IHandleRunModel>()).ToList();
            var selected = allNodes
                .Where(node => bounds.IntersectsWith(new Rect(node.X, node.Y, WorkflowConnection.NodeWidth, WorkflowConnection.NodeHeight)))
                .ToList();

            foreach (var node in allNodes)
            {
                node.IsSelected = selected.Contains(node);
            }

            Execute(SelectConnectionCommand, null);
            if (selected.Count > 0)
            {
                Execute(SelectNodeCommand, selected.First());
                // 再次确保已框选的所有节点保持高亮选中状态
                foreach (var node in selected)
                {
                    node.IsSelected = true;
                }
            }
            else
            {
                Execute(SelectNodeCommand, null);
            }
        }

        private void ClearNodeSelection()
        {
            if (Nodes != null)
            {
                foreach (var node in Nodes.Cast<IHandleRunModel>())
                {
                    node.IsSelected = false;
                }
            }
        }

        private void DeleteSelectedItems()
        {
            // 1. 优先检查是否有选中的连线，若有则仅删除选中的连线，绝不触碰任何节点！
            var selectedConn = Connections?.Cast<WorkflowConnection>().FirstOrDefault(c => c.IsSelected);

            if (selectedConn != null)
            {
                Execute(DeleteSelectedConnectionCommand, null);
                return;
            }

            // 2. 否则处理选中的节点批量删除
            var selectedNodes = GetSelectedNodes().ToList();
            if (selectedNodes.Count == 0)
            {
                return;
            }

            var message = selectedNodes.Count == 1
                ? $"确定删除节点“{selectedNodes[0].DisplayName}”吗？"
                : $"确定删除选中的 {selectedNodes.Count} 个节点吗？";
            var result = MessageBox.Show(
                message,
                "删除节点",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.OK)
            {
                return;
            }

            foreach (var node in selectedNodes)
            {
                Execute(NodeDeleteCommand, node);
            }
        }

        private IEnumerable<IHandleRunModel> GetSelectedNodes()
        {
            return Nodes?.Cast<IHandleRunModel>().Where(node => node.IsSelected) ?? Enumerable.Empty<IHandleRunModel>();
        }

        private static bool IsLeftAltPressed()
        {
            return Keyboard.IsKeyDown(Key.LeftAlt);
        }

        /// <summary>
        /// 根据输出端和当前鼠标坐标刷新临时连线预览。
        /// </summary>
        private void UpdateConnectionPreview(Point current)
        {
            if (pendingConnectionSource == null)
            {
                return;
            }

            var startX = pendingConnectionSource.X + WorkflowConnection.NodeCenterX;
            var startY = pendingConnectionSource.Y + WorkflowConnection.NodeHeight;
            PreviewConnectionPath.Data = Geometry.Parse(BuildPreviewPath(startX, startY, current.X, current.Y));
            PreviewConnectionPath.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 清理临时连线状态并释放画布鼠标捕获。
        /// </summary>
        private void CancelConnectionPreview()
        {
            pendingConnectionSource = null;
            PreviewConnectionPath.Data = null;
            PreviewConnectionPath.Visibility = Visibility.Collapsed;

            if (Mouse.Captured == WorkflowCanvas)
            {
                Mouse.Capture(null);
            }
        }

        /// <summary>
        /// 开始画布平移，记录当前鼠标位置和滚动偏移。
        /// </summary>
        private void StartCanvasPan(MouseButtonEventArgs e)
        {
            isPanning = true;
            panStartPoint = e.GetPosition(WorkflowScrollViewer);
            panStartHorizontalOffset = WorkflowScrollViewer.HorizontalOffset;
            panStartVerticalOffset = WorkflowScrollViewer.VerticalOffset;
            WorkflowCanvas.Cursor = Cursors.ScrollAll;
            Mouse.Capture(WorkflowCanvas);
        }

        /// <summary>
        /// 根据鼠标移动距离更新 ScrollViewer 偏移，实现拖拽平移画布。
        /// </summary>
        private void UpdateCanvasPan(MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed && e.MiddleButton != MouseButtonState.Pressed)
            {
                StopCanvasPan();
                return;
            }

            var current = e.GetPosition(WorkflowScrollViewer);
            WorkflowScrollViewer.ScrollToHorizontalOffset(panStartHorizontalOffset - (current.X - panStartPoint.X));
            WorkflowScrollViewer.ScrollToVerticalOffset(panStartVerticalOffset - (current.Y - panStartPoint.Y));
        }

        /// <summary>
        /// 结束画布平移并恢复鼠标样式。
        /// </summary>
        private void StopCanvasPan()
        {
            isPanning = false;
            WorkflowCanvas.Cursor = null;

            if (Mouse.Captured == WorkflowCanvas)
            {
                Mouse.Capture(null);
            }
        }

        /// <summary>
        /// 构建临时连线的折线路径。
        /// 与正式连线保持类似绕行规则，避免预览线直接穿过节点主体。
        /// </summary>
        private static string BuildPreviewPath(double startX, double startY, double endX, double endY)
        {
            if (endY > startY + WorkflowConnection.RouteGap)
            {
                var midY = (startY + endY) / 2;
                return BuildPath(startX, startY, startX, midY, endX, midY, endX, endY);
            }

            var laneY = Math.Max(startY, endY) + WorkflowConnection.RouteGap;
            return BuildPath(startX, startY, startX, laneY, endX, laneY, endX, endY);
        }

        /// <summary>
        /// 将坐标点序列转换为 WPF Path 使用的折线数据。
        /// </summary>
        private static string BuildPath(params double[] coordinates)
        {
            if (coordinates.Length < 4 || coordinates.Length % 2 != 0)
            {
                return string.Empty;
            }

            var path = $"M {coordinates[0]:0.###},{coordinates[1]:0.###}";
            for (var i = 2; i < coordinates.Length; i += 2)
            {
                path += $" L {coordinates[i]:0.###},{coordinates[i + 1]:0.###}";
            }

            return path;
        }

        /// <summary>
        /// 从 WPF 拖拽数据中提取算子注册模型。
        /// 同时支持自定义格式和接口类型格式，兼容不同控件发起的拖拽。
        /// </summary>
        private static IHandleRegisterModel GetDraggedHandle(IDataObject data)
        {
            if (data == null)
            {
                return null;
            }

            if (data.GetDataPresent(WorkflowDragDrop.ToolboxItemFormat) &&
                data.GetData(WorkflowDragDrop.ToolboxItemFormat) is IHandleRegisterModel formattedModel)
            {
                return formattedModel;
            }

            if (data.GetDataPresent(typeof(IHandleRegisterModel)) &&
                data.GetData(typeof(IHandleRegisterModel)) is IHandleRegisterModel interfaceModel)
            {
                return interfaceModel;
            }

            foreach (var format in data.GetFormats())
            {
                if (data.GetData(format) is IHandleRegisterModel model)
                {
                    return model;
                }
            }

            return null;
        }

        /// <summary>
        /// 安全执行绑定命令，统一处理 CanExecute 检查。
        /// </summary>
        private static void Execute(ICommand command, object parameter)
        {
            if (command?.CanExecute(parameter) == true)
            {
                command.Execute(parameter);
            }
        }
    }
}

