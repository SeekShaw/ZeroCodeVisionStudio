using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Plugin.Handle.ReadImage.View
{
    /// <summary>
    /// ReadImageSettingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ReadImageView : UserControl
    {
        public ReadImageView()
        {
            InitializeComponent();
        }

        private void FileListBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void FileListBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    if (DataContext is ViewModel.ReadImageViewModel vm)
                    {
                        vm.HandleFileDrop(files);
                    }
                }
            }
        }
    }
}
