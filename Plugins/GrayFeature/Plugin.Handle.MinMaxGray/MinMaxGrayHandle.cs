using System;
using System.Collections.Generic;
using System.Linq;
using HalconDotNet;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.MinMaxGray
{
    [Handle("灰度最值", "MinMaxGray", "统计区域内的最小灰度值、最大灰度值和灰度范围（支持抗噪过滤百分比）。", Common.HandleType_GrayFeature)]
    public class MinMaxGrayHandle : ParameterHandleBase<MinMaxGrayParameter>, IHandleDataSlots
    {
        public MinMaxGrayHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.GrayImage, "灰度图像", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "输入区域", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "输出区域 ROI", typeof(HObject)),
            new RunSlot(RunOutputKeys.MinGray, "最小灰度", typeof(HTuple)),
            new RunSlot(RunOutputKeys.MaxGray, "最大灰度", typeof(HTuple)),
            new RunSlot(RunOutputKeys.RangeGray, "灰度跨度", typeof(HTuple))
        };

        public override void Handle()
        {
            var region = HandleInputGuard.RequireRegion(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Region) ?? RunParameter?.HOutput,
                "MinMaxGray");
            var image = HandleInputGuard.RequireImage(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.GrayImage) ?? RunParameter?.GrayImage ?? RunParameter?.RawImage,
                "MinMaxGray",
                "灰度图像");

            var percent = Math.Clamp(Parameter.Percent, 0.0, 50.0);
            HOperatorSet.MinMaxGray(region, image, percent, out var min, out var max, out var range);

            RunParameter.SetOutput(RunOutputKeys.MinGray, min);
            RunParameter.SetOutput(RunOutputKeys.MaxGray, max);
            RunParameter.SetOutput(RunOutputKeys.RangeGray, range);
            RunParameter.HOutput = region;
            RunParameter.SetOutput(RunOutputKeys.Output, region);
            RunParameter.SetOutput(RunOutputKeys.Region, region);
            RunParameter.ResultMessage = $"Min={T(min)}  Max={T(max)}  Range={T(range)} (Percent={percent:0.#}%)";
            RaiseImageDisplay(region);
        }

        private static string T(HTuple t) => t == null ? "" : string.Join(", ", Enumerable.Range(0, t.Length).Select(i => t[i].D.ToString("0.###")));
    }
}
