using System;
using System.Collections.Generic;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;
using Plugin.Handle.DynThreshold.Views;

namespace Plugin.Handle.DynThreshold
{
    [Handle("动态阈值", "DynThreshold", "通过动态阈值分割局部亮缺陷或暗缺陷。", Common.HandleType_Segmentation, OperatorBackend = OperatorBackend.OpenCV)]
    public class DynThresholdHandle : HandleBase, IHandleDataSlots
    {
        private readonly DynThresholdParameter parameter;

        public DynThresholdHandle(IDialogService dialogService) : base(dialogService)
        {
            parameter = new DynThresholdParameter();
            HandleParameter = parameter;
            SetRunParameter(null);
        }

        protected override string DialogName => nameof(DynThresholdView);

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
            var sourceImage = HandleInputGuard.RequireImage(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.GrayImage) ?? RunParameter?.GrayImage ?? RunParameter?.HOutput,
                "DynThreshold");

            var maskWidth = Math.Max(1, parameter.MaskWidth);
            var maskHeight = Math.Max(1, parameter.MaskHeight);
            HOperatorSet.MeanImage(sourceImage, out var meanImage, maskWidth, maskHeight);
            try
            {
                HOperatorSet.DynThreshold(sourceImage, meanImage, out var defectRegion, parameter.Offset, parameter.LightDark.ToHalconString());
                RunParameter.HOutput = defectRegion;
                RunParameter.SetOutput(RunOutputKeys.Output, defectRegion);
                RunParameter.SetOutput(RunOutputKeys.Region, defectRegion);
                RunParameter.ResultMessage = $"动态阈值分割完成 (Offset: {parameter.Offset:0.#})";
                RaiseImageDisplay(defectRegion);
            }
            finally
            {
                meanImage.Dispose();
            }
        }
    }
}
