using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ZeroCodeVisionStudio.UI.Controls
{
    /// <summary>
    /// DataGrid 自动滚动到底部与选中项可视辅助附加行为。
    /// </summary>
    public static class DataGridAutoScrollHelper
    {
        public static readonly DependencyProperty AutoScrollToBottomProperty =
            DependencyProperty.RegisterAttached(
                "AutoScrollToBottom",
                typeof(bool),
                typeof(DataGridAutoScrollHelper),
                new PropertyMetadata(false, OnAutoScrollToBottomChanged));

        public static bool GetAutoScrollToBottom(DependencyObject obj)
        {
            return (bool)obj.GetValue(AutoScrollToBottomProperty);
        }

        public static void SetAutoScrollToBottom(DependencyObject obj, bool value)
        {
            obj.SetValue(AutoScrollToBottomProperty, value);
        }

        public static readonly DependencyProperty ScrollToSelectedItemProperty =
            DependencyProperty.RegisterAttached(
                "ScrollToSelectedItem",
                typeof(bool),
                typeof(DataGridAutoScrollHelper),
                new PropertyMetadata(false, OnScrollToSelectedItemChanged));

        public static bool GetScrollToSelectedItem(DependencyObject obj)
        {
            return (bool)obj.GetValue(ScrollToSelectedItemProperty);
        }

        public static void SetScrollToSelectedItem(DependencyObject obj, bool value)
        {
            obj.SetValue(ScrollToSelectedItemProperty, value);
        }

        private static readonly DependencyProperty CollectionHandlerProperty =
            DependencyProperty.RegisterAttached(
                "CollectionHandler",
                typeof(NotifyCollectionChangedEventHandler),
                typeof(DataGridAutoScrollHelper),
                new PropertyMetadata(null));

        private static void OnAutoScrollToBottomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid dataGrid) return;

            if ((bool)e.NewValue)
            {
                dataGrid.Loaded += DataGrid_Loaded;
                dataGrid.Unloaded += DataGrid_Unloaded;
                HookItemsSource(dataGrid);
            }
            else
            {
                dataGrid.Loaded -= DataGrid_Loaded;
                dataGrid.Unloaded -= DataGrid_Unloaded;
                UnhookItemsSource(dataGrid);
            }
        }

        private static void OnScrollToSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid dataGrid) return;

            if ((bool)e.NewValue)
            {
                dataGrid.SelectionChanged += DataGrid_SelectionChanged;
            }
            else
            {
                dataGrid.SelectionChanged -= DataGrid_SelectionChanged;
            }
        }

        private static void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not DataGrid dataGrid || dataGrid.SelectedItem == null) return;

            dataGrid.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    if (dataGrid.SelectedItem != null)
                    {
                        dataGrid.ScrollIntoView(dataGrid.SelectedItem);
                    }
                }
                catch
                {
                    // 忽略虚拟化或瞬时布局异常
                }
            }));
        }

        private static void DataGrid_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is DataGrid dataGrid)
            {
                HookItemsSource(dataGrid);
            }
        }

        private static void DataGrid_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is DataGrid dataGrid)
            {
                UnhookItemsSource(dataGrid);
            }
        }

        private static void HookItemsSource(DataGrid dataGrid)
        {
            UnhookItemsSource(dataGrid);

            if (dataGrid.ItemsSource is INotifyCollectionChanged notifyCollection)
            {
                NotifyCollectionChangedEventHandler handler = (s, e) =>
                {
                    if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null && e.NewItems.Count > 0)
                    {
                        var lastItem = e.NewItems[e.NewItems.Count - 1];
                        dataGrid.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                        {
                            try
                            {
                                dataGrid.ScrollIntoView(lastItem);
                            }
                            catch
                            {
                                // 忽略可能由于布局或虚拟化引起的瞬时异常
                            }
                        }));
                    }
                };

                dataGrid.SetValue(CollectionHandlerProperty, handler);
                notifyCollection.CollectionChanged += handler;
            }

            var dpd = DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(DataGrid));
            dpd?.RemoveValueChanged(dataGrid, OnItemsSourceChanged);
            dpd?.AddValueChanged(dataGrid, OnItemsSourceChanged);
        }

        private static void OnItemsSourceChanged(object sender, EventArgs e)
        {
            if (sender is DataGrid dg && GetAutoScrollToBottom(dg))
            {
                HookItemsSource(dg);
            }
        }

        private static void UnhookItemsSource(DataGrid dataGrid)
        {
            if (dataGrid.GetValue(CollectionHandlerProperty) is NotifyCollectionChangedEventHandler handler)
            {
                if (dataGrid.ItemsSource is INotifyCollectionChanged notifyCollection)
                {
                    notifyCollection.CollectionChanged -= handler;
                }
                dataGrid.ClearValue(CollectionHandlerProperty);
            }
        }
    }
}
