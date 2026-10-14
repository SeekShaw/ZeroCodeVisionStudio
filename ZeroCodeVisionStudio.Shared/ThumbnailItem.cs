using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 缩略图条目 —— 画布下方"读取图像"节点图像列表里的每一项。
    /// 纯 WIC (BitmapImage) 生成，不依赖 Halcon / OpenCV 运行时。
    /// </summary>
    public class ThumbnailItem : INotifyPropertyChanged
    {
        public ThumbnailItem(BitmapSource thumbnail, string filePath, int index)
        {
            Thumbnail = thumbnail;
            FilePath = filePath;
            Index = index;
        }

        /// <summary>缩略图（DecodePixelWidth=160，内存友好）。</summary>
        public BitmapSource Thumbnail { get; }

        /// <summary>完整文件路径。</summary>
        public string FilePath { get; }

        /// <summary>在 ImageFiles 列表中的索引。</summary>
        public int Index { get; }

        private bool _isSelected;
        /// <summary>当前画布显示的图像是否就是这个。</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 从文件路径生成缩略图（DecodePixelWidth=160，不加载完整分辨率）。
        /// </summary>
        public static ThumbnailItem Create(string filePath, int index)
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.None;
                bi.DecodePixelWidth = 160;
                bi.UriSource = new System.Uri(filePath, System.UriKind.Absolute);
                bi.EndInit();
                bi.Freeze(); // 跨线程可用
                return new ThumbnailItem(bi, filePath, index);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从文件路径生成全尺寸 BitmapSource（点击缩略图时加载到画布用）。
        /// </summary>
        public static BitmapSource CreateFull(string filePath)
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.None;
                bi.UriSource = new System.Uri(filePath, System.UriKind.Absolute);
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch
            {
                return null;
            }
        }
    }
}
