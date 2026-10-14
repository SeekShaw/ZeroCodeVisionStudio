using System;
using System.Collections.Generic;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.Threshold
{
    [Handle("阈值分割", "Threshold", "按灰度范围分割图像像素。", Common.HandleType_Segmentation, OperatorBackend = OperatorBackend.OpenCV)]
    public class ThresholdHandle : ParameterHandleBase<ThresholdParameter>, IHandleDataSlots
    {
        public ThresholdHandle(IDialogService dialogService) : base(dialogService)
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
                "Threshold");

            var min = Math.Min(Parameter.MinGray, Parameter.MaxGray);
            var max = Math.Max(Parameter.MinGray, Parameter.MaxGray);
            HOperatorSet.Threshold(source, out var region, min, max);

            RunParameter.HOutput = region;      
            RunParameter.SetOutput(RunOutputKeys.Output, region);
            RunParameter.SetOutput(RunOutputKeys.Region, region);
            RunParameter.ResultMessage = $"阈值分割完成，灰度范围 [{min}, {max}]";
            RaiseImageDisplay(region);
        }
    }
}
