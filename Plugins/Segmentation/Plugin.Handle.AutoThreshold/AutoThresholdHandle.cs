using System;
using System.Collections.Generic;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.AutoThreshold
{
    [Handle("自动阈值", "AutoThreshold", "根据灰度直方图自动完成图像分割。", Common.HandleType_Segmentation, OperatorBackend = OperatorBackend.OpenCV)]
    public class AutoThresholdHandle : ParameterHandleBase<AutoThresholdParameter>, IHandleDataSlots
    {
        public AutoThresholdHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.GrayImage, "输入灰度图像", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "分割区域 ROI", typeof(HObject))
        };

        public override void Handle()
        {
            var source = HandleInputGuard.RequireImage(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.GrayImage) ?? RunParameter?.GrayImage ?? RunParameter?.HOutput,
                "AutoThreshold");

            HOperatorSet.AutoThreshold(source, out var region, Math.Max(0.1, Parameter.Sigma));

            RunParameter.HOutput = region;
            RunParameter.SetOutput(RunOutputKeys.Output, region);
            RunParameter.SetOutput(RunOutputKeys.Region, region);
            RunParameter.ResultMessage = $"自动阈值分割完成 (Sigma: {Parameter.Sigma:0.##})";
            RaiseImageDisplay(region);
        }
    }
}
