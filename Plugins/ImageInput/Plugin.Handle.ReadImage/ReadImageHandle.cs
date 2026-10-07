using System;
using System.Collections.Generic;
using System.IO;
using HalconVisionStudio.Shared;
using HalconDotNet;
using Plugin.Handle.ReadImage.View;

namespace Plugin.Handle.ReadImage
{
    [Handle("读取图像", "ReadImage", "从本地文件读取图像，支持单张、批量循环与随机抽取。", Common.HandleType_ImageInput)]
    public class ReadImageHandle : HandleBase, IHandleDataSlots
    {
        private readonly ReadImageParameter readParameter;
        private int _runtimeIndex = 0;

        public ReadImageHandle(IDialogService dialogService) : base(dialogService)
        {
            readParameter = new ReadImageParameter();
            HandleParameter = readParameter;
            SetRunParameter(null);
        }

        protected override string DialogName => nameof(ReadImageView);

        public IReadOnlyList<RunSlot> InputSlots { get; } = Array.Empty<RunSlot>();

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出图像", typeof(HObject)),
            new RunSlot(RunOutputKeys.RawImage, "原始图像", typeof(HObject))
        };

        public override void Handle()
        {
            if (readParameter.ImageFiles == null || readParameter.ImageFiles.Count == 0)
                throw new HandleException(ExceptionType.E_FILE_NOT_FOUND, "未设置图像文件路径，请在参数配置中添加图像文件");

            int count = readParameter.ImageFiles.Count;

            // 确定本次读取的索引
            int currentIndex;
            switch (readParameter.ReadMode)
            {
                case ImageReadMode.Single:
                    currentIndex = Math.Clamp(readParameter.CurrentIndex, 0, count - 1);
                    break;

                case ImageReadMode.Random:
                    currentIndex = Random.Shared.Next(count);
                    break;

                case ImageReadMode.Cycle:
                default:
                    if (_runtimeIndex < 0 || _runtimeIndex >= count)
                    {
                        _runtimeIndex = Math.Clamp(readParameter.CurrentIndex, 0, count - 1);
                    }
                    currentIndex = _runtimeIndex;
                    _runtimeIndex = (_runtimeIndex + 1) % count;
                    break;
            }

            var filePath = readParameter.ImageFiles[currentIndex];
            if (!File.Exists(filePath))
                throw new HandleException(ExceptionType.E_FILE_NOT_FOUND, $"图像文件不存在: {filePath}");

            HOperatorSet.ReadImage(out var img, filePath);
            HOperatorSet.GetImageSize(img, out var width, out var height);

            RunParameter.RawImage = img;
            RunParameter.HOutput = img;
            RunParameter.SetOutput(RunOutputKeys.Output, img);
            RunParameter.SetOutput(RunOutputKeys.RawImage, img);

            var fileName = Path.GetFileName(filePath);
            RunParameter.ResultMessage = $"[{currentIndex + 1}/{count}] {fileName} ({width.I}×{height.I})";

            // 写入物理输出数据
            RunParameter.Outputs["CurrentIndex"] = currentIndex + 1;
            RunParameter.Outputs["TotalCount"] = count;
            RunParameter.Outputs["FileName"] = fileName;
            RunParameter.Outputs["FilePath"] = filePath;
            RunParameter.Outputs["Width"] = width.I;
            RunParameter.Outputs["Height"] = height.I;

            RaiseImageDisplay(img);
        }
    }
}
