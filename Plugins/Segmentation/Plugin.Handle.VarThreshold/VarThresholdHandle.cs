using System;
using System.Collections.Generic;
using HalconDotNet;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.VarThreshold
{
    [Handle("局部方差阈值", "VarThreshold", "根据局部均值和偏差分割缺陷。", Common.HandleType_Segmentation)]
    public class VarThresholdHandle : ParameterHandleBase<VarThresholdParameter>, IHandleDataSlots
    {
        public VarThresholdHandle(IDialogService dialogService) : base(dialogService)
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
                "VarThreshold");

            HOperatorSet.VarThreshold(
                source,
                out var region,
                Math.Max(1, Parameter.MaskWidth),
                Math.Max(1, Parameter.MaskHeight),
                Parameter.StdDevScale,
                Parameter.AbsThreshold,
                Parameter.LightDark.ToHalconString());

            RunParameter.HOutput = region;
            RunParameter.SetOutput(RunOutputKeys.Output, region);
            RunParameter.SetOutput(RunOutputKeys.Region, region);
            RunParameter.ResultMessage = "局部方差阈值分割完成。";
            RaiseImageDisplay(region);
        }
    }
}
