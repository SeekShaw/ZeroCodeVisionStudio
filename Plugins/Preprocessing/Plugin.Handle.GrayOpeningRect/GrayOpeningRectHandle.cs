using System;
using System.Collections.Generic;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.GrayOpeningRect
{
    /// <summary>
    /// 灰度矩形开运算算子处理器。
    /// 调用 HALCON 算子 <see cref="HOperatorSet.GrayOpeningRect"/> 对灰度图像执行开运算。
    /// </summary>
    [Handle("灰度矩形开运算", "GrayOpeningRect", "使用矩形滤波器对灰度图像执行开运算。", Common.HandleType_Preprocessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class GrayOpeningRectHandle : ParameterHandleBase<GrayOpeningRectParameter>, IHandleDataSlots
    {
        public GrayOpeningRectHandle(IDialogService dialogService) : base(dialogService)
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
                "GrayOpeningRect");

            HOperatorSet.GrayOpeningRect(source, out var result, Math.Max(1, Parameter.Width), Math.Max(1, Parameter.Height));
            RunParameter.HOutput = result;
            RunParameter.GrayImage = result;
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, result);
            RunParameter.ResultMessage = $"灰度开运算完成 (W:{Parameter.Width}, H:{Parameter.Height})";
            RaiseImageDisplay(result);
        }
    }
}
