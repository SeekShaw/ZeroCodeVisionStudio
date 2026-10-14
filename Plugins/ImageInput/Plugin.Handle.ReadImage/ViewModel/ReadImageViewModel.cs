using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using ZeroCodeVisionStudio.Shared;
using Microsoft.Win32;
using Plugin.Handle.ReadImage.Model;

namespace Plugin.Handle.ReadImage.ViewModel
{
    public class ReadImageViewModel : BindableBase, IDialogAware
    {
        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".bmp", ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".hobj", ".gif"
        };

        private ImageFileItem _selectedItem;
        private string _dispalyImage;
        private ImageReadMode _readMode = ImageReadMode.Cycle;
        private int _currentIndex = 0;

        public ReadImageViewModel()
        {
            AddFilesCommand = new DelegateCommand(OnAddFiles);
            AddFolderCommand = new DelegateCommand(OnAddFolder);
            RemoveItemCommand = new DelegateCommand<ImageFileItem>(OnRemoveItem);
            RemoveSelectedCommand = new DelegateCommand(OnRemoveSelected, () => SelectedItem != null);
            ClearAllCommand = new DelegateCommand(OnClearAll, () => Items.Count > 0);
            MoveUpCommand = new DelegateCommand(OnMoveUp, CanMoveUp);
            MoveDownCommand = new DelegateCommand(OnMoveDown, CanMoveDown);
            OpenInExplorerCommand = new DelegateCommand(OnOpenInExplorer, () => SelectedItem != null);
            CopyPathCommand = new DelegateCommand(OnCopyPath, () => SelectedItem != null);
            ResetIndexCommand = new DelegateCommand(OnResetIndex);
            SortByNameCommand = new DelegateCommand(OnSortByName);
        }

        public ObservableCollection<ImageFileItem> Items { get; } = new ObservableCollection<ImageFileItem>();

        public ReadImageParameter Parameter { get; private set; }

        public ImageFileItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    if (value != null && value.Exists)
                    {
                        DispalyImage = value.FilePath;
                    }
                    else if (value != null && !value.Exists)
                    {
                        DispalyImage = null;
                    }

                    RemoveSelectedCommand.RaiseCanExecuteChanged();
                    MoveUpCommand.RaiseCanExecuteChanged();
                    MoveDownCommand.RaiseCanExecuteChanged();
                    OpenInExplorerCommand.RaiseCanExecuteChanged();
                    CopyPathCommand.RaiseCanExecuteChanged();
                    RaisePropertyChanged(nameof(StatusSummaryText));
                }
            }
        }

        public string DispalyImage
        {
            get => _dispalyImage;
            set => SetProperty(ref _dispalyImage, value);
        }

        public ImageReadMode ReadMode
        {
            get => _readMode;
            set
            {
                if (SetProperty(ref _readMode, value) && Parameter != null)
                {
                    Parameter.ReadMode = value;
                }
            }
        }

        public int CurrentIndex
        {
            get => _currentIndex;
            set
            {
                if (SetProperty(ref _currentIndex, value) && Parameter != null)
                {
                    Parameter.CurrentIndex = value;
                }
            }
        }

        public string StatusSummaryText
        {
            get
            {
                if (Items.Count == 0) return "列表为空，请添加图像文件";
                int current = SelectedItem != null ? SelectedItem.Index : 1;
                return $"共 {Items.Count} 张图像 | 当前选中: 第 {current} 张";
            }
        }

        // ==================== 命令列表 ====================
        public DelegateCommand AddFilesCommand { get; }
        public DelegateCommand AddFolderCommand { get; }
        public DelegateCommand<ImageFileItem> RemoveItemCommand { get; }
        public DelegateCommand RemoveSelectedCommand { get; }
        public DelegateCommand ClearAllCommand { get; }
        public DelegateCommand MoveUpCommand { get; }
        public DelegateCommand MoveDownCommand { get; }
        public DelegateCommand OpenInExplorerCommand { get; }
        public DelegateCommand CopyPathCommand { get; }
        public DelegateCommand ResetIndexCommand { get; }
        public DelegateCommand SortByNameCommand { get; }

        public DialogCloseListener RequestClose { get; set; }

        public bool CanCloseDialog() => true;

        public void OnDialogOpened(IDialogParameters parameters)
        {
            Parameter = parameters.GetValue<ReadImageParameter>(Common.HandleParameter_ParamName);
            if (Parameter != null)
            {
                ReadMode = Parameter.ReadMode;
                CurrentIndex = Parameter.CurrentIndex;

                Items.Clear();
                if (Parameter.ImageFiles != null && Parameter.ImageFiles.Count > 0)
                {
                    int idx = 1;
                    foreach (var path in Parameter.ImageFiles)
                    {
                        Items.Add(ImageFileItem.FromPath(idx++, path));
                    }

                    int selectIdx = Math.Clamp(CurrentIndex, 0, Items.Count - 1);
                    SelectedItem = Items[selectIdx];
                }
            }

            UpdateCommandsState();
        }

        public void OnDialogClosed()
        {
            SyncParameter();
        }

        private void SyncParameter()
        {
            if (Parameter != null)
            {
                Parameter.ImageFiles = Items.Select(x => x.FilePath).ToList();
                Parameter.ReadMode = ReadMode;
                Parameter.CurrentIndex = SelectedItem != null ? Items.IndexOf(SelectedItem) : 0;
            }
        }

        private void OnAddFiles()
        {
            var ofd = new OpenFileDialog
            {
                Multiselect = true,
                Title = "选择图像文件",
                Filter = "常用图像文件 (*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.hobj)|*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.hobj|BMP 图像 (*.bmp)|*.bmp|PNG 图像 (*.png)|*.png|JPEG 图像 (*.jpg;*.jpeg)|*.jpg;*.jpeg|TIFF 图像 (*.tif;*.tiff)|*.tif;*.tiff|所有文件 (*.*)|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (ofd.ShowDialog() == true && ofd.FileNames.Length > 0)
            {
                AddFilePaths(ofd.FileNames);
            }
        }

        private void OnAddFolder()
        {
            var fbd = new OpenFolderDialog
            {
                Title = "选择包含图像的文件夹",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (fbd.ShowDialog() == true && !string.IsNullOrWhiteSpace(fbd.FolderName))
            {
                var folder = fbd.FolderName;
                try
                {
                    var foundFiles = Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)))
                        .OrderBy(f => f)
                        .ToArray();

                    if (foundFiles.Length > 0)
                    {
                        AddFilePaths(foundFiles);
                    }
                    else
                    {
                        MessageBox.Show("所选文件夹中未找到支持的图像文件格式！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"读取文件夹失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void HandleFileDrop(string[] paths)
        {
            if (paths == null || paths.Length == 0) return;

            var allFiles = new List<string>();
            foreach (var p in paths)
            {
                if (Directory.Exists(p))
                {
                    try
                    {
                        allFiles.AddRange(Directory.EnumerateFiles(p, "*.*", SearchOption.TopDirectoryOnly)
                            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f))));
                    }
                    catch { }
                }
                else if (File.Exists(p) && SupportedExtensions.Contains(Path.GetExtension(p)))
                {
                    allFiles.Add(p);
                }
            }

            if (allFiles.Count > 0)
            {
                AddFilePaths(allFiles);
            }
        }

        private void AddFilePaths(IEnumerable<string> paths)
        {
            var firstNewItem = (ImageFileItem)null;
            int startIndex = Items.Count + 1;

            foreach (var p in paths)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                // 允许同一张图多次读取测试，但如需查重可在这里拓展
                var item = ImageFileItem.FromPath(startIndex++, p);
                Items.Add(item);
                firstNewItem ??= item;
            }

            if (SelectedItem == null && firstNewItem != null)
            {
                SelectedItem = firstNewItem;
            }

            SyncParameter();
            UpdateCommandsState();
        }

        private void OnRemoveItem(ImageFileItem item)
        {
            if (item == null) return;

            int removedIdx = Items.IndexOf(item);
            Items.Remove(item);
            ReindexItems();

            if (SelectedItem == item)
            {
                if (Items.Count > 0)
                {
                    int nextIdx = Math.Clamp(removedIdx, 0, Items.Count - 1);
                    SelectedItem = Items[nextIdx];
                }
                else
                {
                    SelectedItem = null;
                    DispalyImage = null;
                }
            }

            SyncParameter();
            UpdateCommandsState();
        }

        private void OnRemoveSelected()
        {
            if (SelectedItem != null)
            {
                OnRemoveItem(SelectedItem);
            }
        }

        private void OnClearAll()
        {
            if (Items.Count == 0) return;

            if (MessageBox.Show("确定要清空图像文件列表吗？", "确认清空", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Items.Clear();
                SelectedItem = null;
                DispalyImage = null;
                CurrentIndex = 0;

                SyncParameter();
                UpdateCommandsState();
            }
        }

        private bool CanMoveUp()
        {
            if (SelectedItem == null) return false;
            return Items.IndexOf(SelectedItem) > 0;
        }

        private void OnMoveUp()
        {
            int idx = Items.IndexOf(SelectedItem);
            if (idx > 0)
            {
                Items.Move(idx, idx - 1);
                ReindexItems();
                UpdateCommandsState();
                SyncParameter();
            }
        }

        private bool CanMoveDown()
        {
            if (SelectedItem == null) return false;
            int idx = Items.IndexOf(SelectedItem);
            return idx >= 0 && idx < Items.Count - 1;
        }

        private void OnMoveDown()
        {
            int idx = Items.IndexOf(SelectedItem);
            if (idx >= 0 && idx < Items.Count - 1)
            {
                Items.Move(idx, idx + 1);
                ReindexItems();
                UpdateCommandsState();
                SyncParameter();
            }
        }

        private void OnOpenInExplorer()
        {
            if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.FilePath)) return;

            try
            {
                if (File.Exists(SelectedItem.FilePath))
                {
                    Process.Start("explorer.exe", $"/select,\"{SelectedItem.FilePath}\"");
                }
                else if (Directory.Exists(SelectedItem.DirectoryPath))
                {
                    Process.Start("explorer.exe", $"\"{SelectedItem.DirectoryPath}\"");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开文件所在目录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnCopyPath()
        {
            if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.FilePath)) return;

            try
            {
                Clipboard.SetText(SelectedItem.FilePath);
            }
            catch { }
        }

        private void OnResetIndex()
        {
            CurrentIndex = 0;
            if (Items.Count > 0)
            {
                SelectedItem = Items[0];
            }
        }

        private void OnSortByName()
        {
            if (Items.Count <= 1) return;

            var sorted = Items.OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase).ToList();
            Items.Clear();
            int idx = 1;
            foreach (var item in sorted)
            {
                item.Index = idx++;
                Items.Add(item);
            }

            if (Items.Count > 0)
            {
                SelectedItem = Items[0];
            }

            SyncParameter();
        }

        private void ReindexItems()
        {
            int idx = 1;
            foreach (var item in Items)
            {
                item.Index = idx++;
            }
            RaisePropertyChanged(nameof(StatusSummaryText));
        }

        private void UpdateCommandsState()
        {
            RemoveSelectedCommand.RaiseCanExecuteChanged();
            ClearAllCommand.RaiseCanExecuteChanged();
            MoveUpCommand.RaiseCanExecuteChanged();
            MoveDownCommand.RaiseCanExecuteChanged();
            OpenInExplorerCommand.RaiseCanExecuteChanged();
            CopyPathCommand.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(StatusSummaryText));
        }
    }
}
