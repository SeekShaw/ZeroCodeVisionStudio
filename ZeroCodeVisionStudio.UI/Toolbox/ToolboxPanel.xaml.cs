using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ZeroCodeVisionStudio.Shared;

namespace ZeroCodeVisionStudio.UI.Controls
{
    /// <summary>
    /// Operator toolbox panel.
    /// </summary>
    public partial class ToolboxPanel : UserControl
    {
        private const double RailWidth = 52;
        private const double ExpandedWidth = 340;

        private readonly Dictionary<string, ToolboxGroup> groupMap = new Dictionary<string, ToolboxGroup>();

        private readonly DispatcherTimer flyoutCloseTimer;
        private Point dragStartPoint;
        private INotifyCollectionChanged observedCollectionView;
        private INotifyCollectionChanged observedCollection;

        public ToolboxPanel()
        {
            InitializeComponent();
            flyoutCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            flyoutCloseTimer.Tick += FlyoutCloseTimer_Tick;
            BuildGroups();
            SelectGroup(Groups.FirstOrDefault());
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(IEnumerable),
                typeof(ToolboxPanel),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty AddNodeCommandProperty =
            DependencyProperty.Register(nameof(AddNodeCommand), typeof(ICommand), typeof(ToolboxPanel));

        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register(
                nameof(IsExpanded),
                typeof(bool),
                typeof(ToolboxPanel),
                new PropertyMetadata(true, OnIsExpandedChanged));

        public static readonly DependencyProperty PanelWidthProperty =
            DependencyProperty.Register(
                nameof(PanelWidth),
                typeof(double),
                typeof(ToolboxPanel),
                new PropertyMetadata(RailWidth));

        public static readonly DependencyProperty IsFlyoutOpenProperty =
            DependencyProperty.Register(
                nameof(IsFlyoutOpen),
                typeof(bool),
                typeof(ToolboxPanel),
                new PropertyMetadata(false));

        public static readonly DependencyProperty SearchTextProperty =
            DependencyProperty.Register(
                nameof(SearchText),
                typeof(string),
                typeof(ToolboxPanel),
                new PropertyMetadata(null, OnSearchTextChanged));

        /// <summary>工具箱搜索关键字：非空时 Groups 替换为跨分组的"搜索结果"伪分组。</summary>
        public string SearchText
        {
            get => (string)GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ToolboxPanel)d).ApplySearch();
        }

        /// <summary>搜索结果伪分组（跨全部分组按名称/键/分类/描述匹配）。</summary>
        private readonly ToolboxGroup searchGroup = new(
            "__search__", "搜索结果",
            Geometry.Parse("M8,2.5 A5.5,5.5 0 1 0 8,13.5 A5.5,5.5 0 1 0 8,2.5 M12.5,12.5 L15,15"),
            -1);

        /// <summary>非搜索态的正常分组快照（恢复用）。</summary>
        private readonly List<ToolboxGroup> normalGroups = new();

        private void ApplySearch()
        {
            var keyword = (SearchText ?? "").Trim();

            if (keyword.Length == 0)
            {
                if (!ReferenceEquals(Groups.FirstOrDefault(), searchGroup))
                {
                    return;
                }
                Groups.Clear();
                foreach (var group in normalGroups)
                {
                    Groups.Add(group);
                }
                SelectGroup(Groups.FirstOrDefault());
                return;
            }

            if (!ReferenceEquals(Groups.FirstOrDefault(), searchGroup))
            {
                normalGroups.Clear();
                normalGroups.AddRange(Groups);
            }

            searchGroup.Handles.Clear();
            foreach (var model in normalGroups.SelectMany(x => x.Handles))
            {
                if (Matches(model, keyword))
                {
                    searchGroup.Handles.Add(model);
                }
            }

            Groups.Clear();
            Groups.Add(searchGroup);
            SelectGroup(searchGroup);
        }

        private static bool Matches(IHandleRegisterModel model, string keyword)
        {
            return Contains(model.HandleName) || Contains(model.Key) || Contains(model.HandleType) ||
                   (model.HandleDescription != null && Contains(model.HandleDescription));

            bool Contains(string source) =>
                source != null && source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static readonly DependencyProperty SelectedGroupProperty =
            DependencyProperty.Register(
                nameof(SelectedGroup),
                typeof(ToolboxGroup),
                typeof(ToolboxPanel),
                new PropertyMetadata(null));

        public ObservableCollection<ToolboxGroup> Groups { get; } = new ObservableCollection<ToolboxGroup>();

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public ICommand AddNodeCommand
        {
            get => (ICommand)GetValue(AddNodeCommandProperty);
            set => SetValue(AddNodeCommandProperty, value);
        }

        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        public double PanelWidth
        {
            get => (double)GetValue(PanelWidthProperty);
            set => SetValue(PanelWidthProperty, value);
        }

        public bool IsFlyoutOpen
        {
            get => (bool)GetValue(IsFlyoutOpenProperty);
            set => SetValue(IsFlyoutOpenProperty, value);
        }

        public ToolboxGroup SelectedGroup
        {
            get => (ToolboxGroup)GetValue(SelectedGroupProperty);
            set => SetValue(SelectedGroupProperty, value);
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ToolboxPanel)d).AttachItemsSource(e.OldValue as IEnumerable, e.NewValue as IEnumerable);
        }

        private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var panel = (ToolboxPanel)d;
            panel.IsFlyoutOpen = false;
            panel.flyoutCloseTimer?.Stop();

            if ((bool)e.NewValue)
            {
                // 展开时自动展开第一个分组（而不是全折叠）
                foreach (var g in panel.Groups) g.IsExpanded = false;
                panel.Groups.FirstOrDefault()!.IsExpanded = true;
            }
        }

        /// <summary>
        /// 监听 ItemsSource 变化，确保模块注册、刷新后工具箱分组能同步更新。
        /// </summary>
        private void AttachItemsSource(IEnumerable oldItemsSource, IEnumerable newItemsSource)
        {
            if (observedCollectionView != null)
            {
                observedCollectionView.CollectionChanged -= ItemsSource_CollectionChanged;
                observedCollectionView = null;
            }

            if (observedCollection != null)
            {
                observedCollection.CollectionChanged -= ItemsSource_CollectionChanged;
                observedCollection = null;
            }

            if (newItemsSource is ICollectionView collectionView && collectionView is INotifyCollectionChanged notifyView)
            {
                observedCollectionView = notifyView;
                observedCollectionView.CollectionChanged += ItemsSource_CollectionChanged;
            }
            else if (newItemsSource is INotifyCollectionChanged notifyCollection)
            {
                observedCollection = notifyCollection;
                observedCollection.CollectionChanged += ItemsSource_CollectionChanged;
            }

            RefreshItems();
        }

        private void ItemsSource_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshItems();
        }

        /// <summary>
        /// 根据当前算子集合重建分组内容。
        /// 不包含算子的分组不会添加到 Groups，因此左侧不会出现空分组。
        /// </summary>
        private void RefreshItems()
        {
            foreach (var group in Groups)
            {
                group.Handles.Clear();
            }

            if (ItemsSource != null)
            {
                foreach (var model in ItemsSource.OfType<IHandleRegisterModel>().OrderBy(x => x.HandleName))
                {
                    var group = GetOrCreateGroup(model.HandleType);
                    group.Handles.Add(model);
                }
            }

            var selectedGroup = SelectedGroup;
            Groups.Clear();
            foreach (var group in groupMap.Values
                         .Where(x => x.Handles.Count > 0)
                         .OrderBy(x => x.SortIndex)
                         .ThenBy(x => x.DisplayName))
            {
                Groups.Add(group);
            }

            SelectGroup(Groups.FirstOrDefault(x => ReferenceEquals(x, selectedGroup)) ?? Groups.FirstOrDefault());

            // 注册/刷新发生在搜索态时：更新快照并重跑过滤
            normalGroups.Clear();
            if (!ReferenceEquals(Groups.FirstOrDefault(), searchGroup))
            {
                normalGroups.AddRange(Groups);
            }
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                ApplySearch();
            }
        }

        /// <summary>
        /// 注册工具箱已知分组。
        /// 分组名称使用中文显示，handleType 必须与算子注册时声明的 HandleType 保持一致。
        /// </summary>
        private void BuildGroups()
        {
            AddGroup(Common.HandleType_ImageInput, "图像源", "M4,7 L8,7 L10,5 L14,5 L16,7 L20,7 L20,18 L4,18 Z M12,10 A3,3 0 1 0 12,16 A3,3 0 1 0 12,10", 10);
            AddGroup(Common.HandleType_Preprocessing, "图像预处理", "M12,4 A8,8 0 1 0 12,20 M12,4 L12,8 M18,8 L15,10 M18,16 L15,14 M6,16 L9,14 M6,8 L9,10", 20);
            AddGroup(Common.HandleType_ImageProcessing, "图像滤波", "M4,5 L20,5 L20,19 L4,19 Z M4,9 L20,15 M4,13 L20,19 M9,5 L20,12", 30);
            AddGroup(Common.HandleType_Segmentation, "图像分割", "M4,5 L20,5 L20,19 L4,19 Z M4,9 L20,15 M4,13 L20,19 M9,5 L20,12", 40);
            AddGroup(Common.HandleType_RegionProcessing, "区域处理", "M5,12 C8,8 12,16 15,12 M5,18 C8,14 12,22 15,18 M15,12 L20,12 M15,18 L20,18 M10,6 L10,22", 50);
            AddGroup(Common.HandleType_Geometry, "几何生成", "M4,8 C9,8 9,16 14,16 M14,16 L20,16 M17,13 L20,16 L17,19 M4,16 C9,16 9,8 14,8 M14,8 L20,8 M17,5 L20,8 L17,11", 60);
            AddGroup(Common.HandleType_ShapeMatching, "模板匹配", "M7,4 L17,4 L17,8 L21,8 L21,20 L3,20 L3,8 L7,8 Z M8,12 L16,12 M8,16 L13,16", 70);
            AddGroup(Common.HandleType_Measurement, "测量", "M4,8 L4,4 L8,4 M16,4 L20,4 L20,8 M20,16 L20,20 L16,20 M8,20 L4,20 L4,16 M8,15 L16,15 M10,10 L14,10", 80);
            AddGroup(Common.HandleType_GrayFeature, "灰度特征", "M4,19 L20,19 M5,16 L5,11 M9,16 L9,8 M13,16 L13,5 M17,16 L17,9 M4,4 L20,4 M4,7 L20,7", 90);
            AddGroup(Common.HandleType_ShapeFeature, "形状特征", "M5,7 L11,5 L17,7 L19,13 L15,19 L9,19 L5,13 Z M8,12 A4,4 0 1 0 16,12 A4,4 0 1 0 8,12 M12,3 L12,6 M12,18 L12,21", 100);
            AddGroup("轮廓处理", "轮廓处理", "M4,15 C7,6 12,5 15,10 C17,13 14,16 10,15 C7,14 7,11 10,10 M6,18 C10,21 17,20 20,14 M5,15 L5.1,15 M10,10 L10.1,10 M15,10 L15.1,10 M20,14 L20.1,14", 110);
            AddGroup(Common.HandleType_ResultOutput, "结果输出", "M4,5 L20,5 L20,16 L4,16 Z M9,20 L15,20 M12,16 L12,20", 120);
            AddGroup("系统数据", "系统数据源", "M4,5 L20,5 L20,19 L4,19 Z M7,16 L11,12 L14,15 L16,13 L20,17 M8,9 L8.1,9", 130);
            AddGroup("ONNX 模型", "ONNX 通用模型", "M5,8 C5,5 8,3 12,3 C16,3 19,5 19,8 C21,9 22,11 22,14 C22,18 19,21 15,21 L9,21 C5,21 2,18 2,14 C2,11 3,9 5,8 Z M8,14 L12,17 L17,11", 140);
            AddGroup("ObjectRecognition", "对象识别模块", "M4,6 L20,6 L20,18 L4,18 Z M7,9 L17,9 M7,12 L14,12 M7,15 L18,15", 150);
            AddGroup("NetworkCommunication", "网络通讯模块", "M4,7 L20,7 L20,16 L4,16 Z M8,20 L16,20 M12,16 L12,20 M7,12 L10,12 M14,12 L17,12", 160);
            AddGroup("设备通讯", "设备通讯", "M4,7 L20,7 L20,16 L4,16 Z M8,20 L16,20 M12,16 L12,20 M7,12 L10,12 M14,12 L17,12 M6,4 L6,7 M18,4 L18,7", 170);
            AddGroup("Other", "其他模块", "M5,12 L5.1,12 M12,12 L12.1,12 M19,12 L19.1,12", 1000);
        }

        /// <summary>
        /// 新增一个已知分组定义。
        /// </summary>
        private void AddGroup(string handleType, string displayName, string iconData, int sortIndex)
        {
            groupMap[handleType] = new ToolboxGroup(handleType, displayName, Geometry.Parse(iconData), sortIndex);
        }

        /// <summary>
        /// 根据 HandleType 获取分组。
        /// 未知类型会落到兜底分组，避免算子因为分组未注册而无法显示。
        /// </summary>
        private ToolboxGroup GetOrCreateGroup(string handleType)
        {
            var normalizedType = string.IsNullOrWhiteSpace(handleType) ? "Other" : handleType;
            if (groupMap.TryGetValue(normalizedType, out var group))
            {
                return group;
            }

            group = new ToolboxGroup(normalizedType, normalizedType, Geometry.Parse("M5,12 L5.1,12 M12,12 L12.1,12 M19,12 L19.1,12"), 1000);
            groupMap[normalizedType] = group;
            return group;
        }

        /// <summary>
        /// 左下角按钮切换工具箱完整展开/窄栏模式。
        /// </summary>
        private void TogglePanel_Click(object sender, RoutedEventArgs e)
        {
            IsExpanded = !IsExpanded;
        }

        /// <summary>窄栏顶部搜索按钮：展开工具箱并聚焦搜索框。</summary>
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            IsExpanded = true;
            SearchBox?.Focus();
            SearchBox?.CaretIndex = SearchBox?.Text?.Length ?? 0;
        }

        /// <summary>搜索框按 Esc 清空并还原分组。</summary>
        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SearchText = string.Empty;
                if (SearchBox != null) SearchBox.Text = string.Empty;
            }
        }

        /// <summary>
        /// 折叠窄栏中点击分组图标时打开对应浮层。
        /// </summary>
        private void GroupButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is ToolboxGroup group)
            {
                OpenGroupFlyout(group);
                e.Handled = true;
            }
        }

        /// <summary>
        /// 鼠标悬浮分组图标时自动展开该分组浮层。
        /// </summary>
        private void GroupButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is ToolboxGroup group)
            {
                OpenGroupFlyout(group);
            }
        }

        /// <summary>
        /// 展开模式下点击分组标题，选中分组并折叠/展开该分组的算子列表。
        /// </summary>
        private void GroupRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is ToolboxGroup group)
            {
                SelectGroup(group);
                IsFlyoutOpen = false;
                if (group.Handles.Count > 0)
                {
                    group.IsExpanded = !group.IsExpanded;
                }

                e.Handled = true;
            }
        }

        private void CollapsedRail_MouseEnter(object sender, MouseEventArgs e)
        {
            // 鼠标回到窄栏时取消关闭计时，保证浮层不会误关闭
            flyoutCloseTimer.Stop();
        }

        private void CollapsedRail_MouseLeave(object sender, MouseEventArgs e)
        {
            ScheduleFlyoutClose();
        }

        private void FlyoutPanel_MouseEnter(object sender, MouseEventArgs e)
        {
            // 鼠标进入浮层后继续保持打开
            flyoutCloseTimer.Stop();
        }

        private void FlyoutPanel_MouseLeave(object sender, MouseEventArgs e)
        {
            ScheduleFlyoutClose();
        }

        /// <summary>
        /// 在折叠模式下打开某个分组的浮层。
        /// 完整展开模式已经直接显示列表，因此不再额外打开浮层。
        /// </summary>
        private void OpenGroupFlyout(ToolboxGroup group)
        {
            SelectGroup(group);
            if (IsExpanded)
            {
                return;
            }

            flyoutCloseTimer.Stop();
            IsFlyoutOpen = group?.Handles.Count > 0;
        }

        /// <summary>
        /// 延迟关闭浮层，给鼠标从窄栏移动到浮层留出时间。
        /// </summary>
        private void ScheduleFlyoutClose()
        {
            if (!IsFlyoutOpen)
            {
                return;
            }

            flyoutCloseTimer.Stop();
            flyoutCloseTimer.Start();
        }

        /// <summary>
        /// 关闭计时到点后，再次检查鼠标是否仍在窄栏或浮层中。
        /// </summary>
        private void FlyoutCloseTimer_Tick(object sender, EventArgs e)
        {
            flyoutCloseTimer.Stop();
            if (IsExpanded ||
                (CollapsedRail?.IsMouseOver != true && FlyoutPanel?.IsMouseOver != true))
            {
                IsFlyoutOpen = false;
            }
        }

        /// <summary>
        /// 切换到完整展开模式时折叠所有分组，避免上次浮层状态影响展开后的布局。
        /// </summary>
        private void CollapseAllGroups()
        {
            foreach (var group in Groups)
            {
                group.IsExpanded = false;
            }
        }

        /// <summary>
        /// 更新当前选中分组，用于高亮窄栏图标和浮层标题。
        /// </summary>
        private void SelectGroup(ToolboxGroup group)
        {
            foreach (var item in Groups)
            {
                item.IsSelected = ReferenceEquals(item, group);
            }

            SelectedGroup = group;
        }

        /// <summary>
        /// 在算子项上拖动鼠标时启动 WPF 拖拽，目标是流程画布。
        /// </summary>
        private void ToolboxItem_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var current = e.GetPosition(this);
            if (Math.Abs(current.X - dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(current.Y - dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            if ((sender as FrameworkElement)?.DataContext is IHandleRegisterModel model)
            {
                var dragData = new DataObject();
                dragData.SetData(WorkflowDragDrop.ToolboxItemFormat, model);
                dragData.SetData(typeof(IHandleRegisterModel), model);
                DragDrop.DoDragDrop((DependencyObject)sender, dragData, DragDropEffects.Copy);
                IsFlyoutOpen = false;
                e.Handled = true;
            }
        }

        /// <summary>
        /// 记录拖拽起点；双击算子时直接添加到流程画布默认位置。
        /// </summary>
        private void ToolboxItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            dragStartPoint = e.GetPosition(this);
            if (e.ClickCount != 2 || (sender as FrameworkElement)?.DataContext is not IHandleRegisterModel model)
            {
                return;
            }

            var request = new WorkflowNodeCreateRequest(model, double.NaN, double.NaN);
            if (AddNodeCommand?.CanExecute(request) == true)
            {
                AddNodeCommand.Execute(request);
            }

            IsFlyoutOpen = false;
            e.Handled = true;
        }
    }
}

