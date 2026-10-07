using System;
using System.IO;
using System.Windows.Media.Imaging;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.ReadImage.Model
{
    public class ImageFileItem : BindableBase
    {
        private int _index;
        private string _filePath;
        private string _fileSizeText;
        private string _dimensions;
        private bool _exists;

        public int Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }

        public string FilePath
        {
            get => _filePath;
            set
            {
                if (SetProperty(ref _filePath, value))
                {
                    RaisePropertyChanged(nameof(FileName));
                    RaisePropertyChanged(nameof(DirectoryPath));
                    RaisePropertyChanged(nameof(Extension));
                }
            }
        }

        public string FileName => string.IsNullOrEmpty(FilePath) ? "" : Path.GetFileName(FilePath);
        public string DirectoryPath => string.IsNullOrEmpty(FilePath) ? "" : Path.GetDirectoryName(FilePath);
        public string Extension => string.IsNullOrEmpty(FilePath) ? "" : Path.GetExtension(FilePath)?.TrimStart('.')?.ToUpperInvariant();

        public string FileSizeText
        {
            get => _fileSizeText;
            set => SetProperty(ref _fileSizeText, value);
        }

        public string Dimensions
        {
            get => _dimensions;
            set => SetProperty(ref _dimensions, value);
        }

        public bool Exists
        {
            get => _exists;
            set => SetProperty(ref _exists, value);
        }

        public static ImageFileItem FromPath(int index, string path)
        {
            var item = new ImageFileItem
            {
                Index = index,
                FilePath = path,
                Exists = File.Exists(path)
            };

            if (item.Exists)
            {
                try
                {
                    var fi = new FileInfo(path);
                    item.FileSizeText = FormatBytes(fi.Length);

                    // 快速解码图像头元数据（不加载完整大图）
                    using var stream = File.OpenRead(path);
                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                    if (decoder.Frames.Count > 0)
                    {
                        var frame = decoder.Frames[0];
                        item.Dimensions = $"{frame.PixelWidth} × {frame.PixelHeight}";
                    }
                    else
                    {
                        item.Dimensions = "未知尺寸";
                    }
                }
                catch
                {
                    item.Dimensions = "无法解析尺寸";
                }
            }
            else
            {
                item.FileSizeText = "文件不存在";
                item.Dimensions = "无效路径";
            }

            return item;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        }
    }
}
