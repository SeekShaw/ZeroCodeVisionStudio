using System;
using System.Collections.Generic;
using System.ComponentModel;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.ReadImage
{
    public enum ImageReadMode
    {
        [Description("按序循环读取")]
        Cycle,
        [Description("单张固定读取")]
        Single,
        [Description("随机抽取读取")]
        Random
    }

    public class ReadImageParameter : IHandleParameter
    {
        public ReadImageParameter()
        {
        }

        public IHandleParameter Clone()
        {
            return new ReadImageParameter
            {
                ImageFiles = ImageFiles == null ? new List<string>() : new List<string>(ImageFiles),
                ReadMode = ReadMode,
                CurrentIndex = CurrentIndex
            };
        }

        public void Reset()
        {
            ImageFiles = new List<string>();
            CurrentIndex = 0;
            ReadMode = ImageReadMode.Cycle;
        }

        [DisplayName("图像路径列表")]
        [Description("待读取的图像文件路径列表")]
        public List<string> ImageFiles { get; set; } = new List<string>();

        [DisplayName("读取模式")]
        [Description("单张固定读取、按序循环读取或随机读取")]
        public ImageReadMode ReadMode { get; set; } = ImageReadMode.Cycle;

        [DisplayName("当前图像索引")]
        [Description("当前选中的图像文件索引（0 开始）")]
        public int CurrentIndex { get; set; } = 0;
    }
}
