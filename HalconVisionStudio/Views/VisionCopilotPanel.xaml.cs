using System.Collections.Specialized;
using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using HalconVisionStudio.ViewModels;

namespace HalconVisionStudio.Views
{
    public partial class VisionCopilotPanel : UserControl
    {
        public VisionCopilotPanel()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is VisionCopilotViewModel oldVm)
            {
                oldVm.Messages.CollectionChanged -= OnMessagesCollectionChanged;
                oldVm.RequestFocusApiKey -= FocusApiKeyBox;
            }

            if (e.NewValue is VisionCopilotViewModel newVm)
            {
                newVm.Messages.CollectionChanged += OnMessagesCollectionChanged;
                newVm.RequestFocusApiKey += FocusApiKeyBox;
                newVm.RequestRefreshApiKey += RefreshApiKeyBox;
                // DataContext 就绪后把已保存的 API Key 装载进 PasswordBox（非明文显示）。
                ApiKeyBox.Password = newVm.Settings?.ApiKey ?? "";
            }
        }

        /// <summary>切换服务商自动换载密钥后，同步 PasswordBox / 明文框显示。</summary>
        private void RefreshApiKeyBox()
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (DataContext is not VisionCopilotViewModel vm || vm.Settings == null) return;
                var key = vm.Settings.ApiKey ?? "";
                if (ApiKeyBox.Password != key) ApiKeyBox.Password = key;
                if (ApiKeyPlainText.Text != key) ApiKeyPlainText.Text = key;
            });
        }

        /// <summary>缺密钥导致无法获取模型列表时，聚焦密钥输入框引导用户。</summary>
        private void FocusApiKeyBox()
        {
            Dispatcher.BeginInvoke(() =>
            {
                ApiKeyBox.Visibility = System.Windows.Visibility.Visible;
                ApiKeyBox.Focus();
                System.Windows.Input.Keyboard.Focus(ApiKeyBox);
            });
        }

        // ==================== API Key PasswordBox 同步 ====================

        private void ApiKeyBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is VisionCopilotViewModel vm && vm.Settings != null)
            {
                vm.Settings.ApiKey = ApiKeyBox.Password;
                ApiKeyPlainText.Text = ApiKeyBox.Password;
                // 密钥填入后自动拉取一次真实模型列表。
                vm.NotifyApiKeyChanged();
            }
        }

        private void ApiKeyToggle_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            bool showPlain = ApiKeyToggle.IsChecked == true;
            ApiKeyPlainText.Text = ApiKeyBox.Password;
            ApiKeyPlainText.Visibility = showPlain ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            ApiKeyBox.Visibility = showPlain ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        }

        private bool _autoScroll = true;

        private void OnMessagesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                _autoScroll = true;
                Dispatcher.InvokeAsync(() =>
                {
                    ChatScrollViewer?.ScrollToBottom();
                });
            }
        }

        private void ChatScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (ChatScrollViewer == null) return;

            // 当 ExtentHeightChange == 0 时说明是用户在手动滚动；检测是否处于底部区域
            if (e.ExtentHeightChange == 0)
            {
                _autoScroll = ChatScrollViewer.VerticalOffset >= ChatScrollViewer.ScrollableHeight - 50;
            }

            // 仅在处于自动跟随状态且内容增长时滚动到底部
            if (_autoScroll && e.ExtentHeightChange > 0)
            {
                ChatScrollViewer.ScrollToBottom();
            }
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
            {
                e.Handled = true;
                if (DataContext is VisionCopilotViewModel vm && vm.SendCommand.CanExecute())
                {
                    vm.SendCommand.Execute();
                }
            }
        }

        // ==================== 图片输入（📎 选择 / Ctrl+V 粘贴 / 拖放） ====================

        private void OnAttachImage(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "附加图片",
                Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
                Multiselect = true
            };
            if (dialog.ShowDialog() != true) return;
            if (DataContext is VisionCopilotViewModel vm)
            {
                foreach (var file in dialog.FileNames) vm.AddPendingImage(file);
            }
        }

        /// <summary>Ctrl+V：剪贴板是图片时拦截并附加（文本粘贴不受影响）。</summary>
        private void InputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control && Clipboard.ContainsImage())
            {
                e.Handled = true;
                var path = SaveClipboardImageToFile();
                if (path != null && DataContext is VisionCopilotViewModel vm)
                {
                    vm.AddPendingImage(path);
                }
            }
        }

        private void OnImageDragOver(object sender, DragEventArgs e)
        {
            e.Effects = HasImageDrop(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void OnImageDrop(object sender, DragEventArgs e)
        {
            if (!HasImageDrop(e.Data) || DataContext is not VisionCopilotViewModel vm) return;
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                foreach (var file in files) vm.AddPendingImage(file);
            }
            e.Handled = true;
        }

        private static bool HasImageDrop(IDataObject data)
        {
            if (!data.GetDataPresent(DataFormats.FileDrop)) return false;
            return data.GetData(DataFormats.FileDrop) is string[] files && files.Any(f =>
                Path.GetExtension(f).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp");
        }

        /// <summary>剪贴板位图保存为临时 PNG（文件路径进入多模态消息管线）。</summary>
        private static string SaveClipboardImageToFile()
        {
            try
            {
                if (Clipboard.GetImage() is not BitmapSource source) return null;
                var path = Path.Combine(Path.GetTempPath(), $"hvs_paste_{DateTime.Now:HHmmss_fff}.png");
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));
                using (var stream = File.Create(path))
                {
                    encoder.Save(stream);
                }
                return path;
            }
            catch
            {
                return null;
            }
        }
    }
}
