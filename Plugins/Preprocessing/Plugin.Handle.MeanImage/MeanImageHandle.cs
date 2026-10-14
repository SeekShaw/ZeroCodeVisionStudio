using System;
using System.Collections.Generic;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.MeanImage
{
    /// <summary>
    /// 均值滤波算子处理器。
    /// 调用 HALCON 算子 <see cref="HOperatorSet.MeanImage"/> 对图像进行均值滤波平滑处理。
    /// </summary>
    [Handle("均值滤波", "MeanImage", "使用均值滤波平滑图像。", Common.HandleType_Preprocessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class MeanImageHandle : ParameterHandleBase<MeanImageParameter>, IHandleDataSlots
    {
        public MeanImageHandle(IDialogService dialogService) : base(dialogService)
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
                "MeanImage");

            HOperatorSet.MeanImage(source, out var result, Math.Max(1, Parameter.MaskWidth), Math.Max(1, Parameter.MaskHeight));
            RunParameter.HOutput = result;
            RunParameter.GrayImage = result;
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, result);
            RunParameter.ResultMessage = $"均值滤波完成 (Mask: {Parameter.MaskWidth}x{Parameter.MaskHeight})";
            RaiseImageDisplay(result);
        }
    }
}
