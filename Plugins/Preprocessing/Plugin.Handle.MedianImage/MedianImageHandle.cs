using System;
using System.Collections.Generic;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.MedianImage
{
    /// <summary>
    /// 中值滤波算子处理器。
    /// 调用 HALCON 算子 <see cref="HOperatorSet.MedianImage"/> 对图像进行中值滤波。
    /// </summary>
    [Handle("中值滤波", "MedianImage", "使用中值滤波抑制脉冲噪声。", Common.HandleType_Preprocessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class MedianImageHandle : ParameterHandleBase<MedianImageParameter>, IHandleDataSlots
    {
        public MedianImageHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.GrayImage, "输入灰度图像", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出图像", typeof(HObject)),
            new RunSlot(RunOutputKeys.GrayImage, "灰度图像", typeof(HObject))
        };

        public override void Handle()
        {
            var source = HandleInputGuard.RequireImage(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.GrayImage) ?? RunParameter?.GrayImage ?? RunParameter?.HOutput,
                "MedianImage");

            HOperatorSet.MedianImage(source, out var result, Parameter.MaskType.ToHalconString(), Math.Max(1, Parameter.Radius), "mirrored");
            RunParameter.HOutput = result;
            RunParameter.GrayImage = result;
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, result);
            RunParameter.ResultMessage = $"中值滤波完成 ({Parameter.MaskType} 半径: {Parameter.Radius})";
            RaiseImageDisplay(result);
        }
    }
}
