using System;
using System.Collections.Generic;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.EdgesSubPix
{
    [Handle("亚像素边缘", "EdgesSubPix", "提取亚像素精度的 XLD 边缘。", Common.HandleType_Segmentation, OperatorBackend = OperatorBackend.Halcon)]
    public class EdgesSubPixHandle : ParameterHandleBase<EdgesSubPixParameter>, IHandleDataSlots
    {
        public EdgesSubPixHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.GrayImage, "输入灰度图像", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出轮廓", typeof(HObject)),
            new RunSlot(RunOutputKeys.Xld, "XLD 边缘", typeof(HObject))
        };

        public override void Handle()
        {
            var source = HandleInputGuard.RequireImage(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.GrayImage) ?? RunParameter?.GrayImage ?? RunParameter?.HOutput,
                "EdgesSubPix");

            HOperatorSet.EdgesSubPix(source, out var edges, Parameter.Filter.ToHalconString(), Parameter.Alpha, Parameter.Low, Parameter.High);
            RunParameter.HOutput = edges;
            RunParameter.SetOutput(RunOutputKeys.Output, edges);
            RunParameter.SetOutput(RunOutputKeys.Xld, edges);
            RunParameter.ResultMessage = $"亚像素边缘提取完成 ({Parameter.Filter})";
            RaiseImageDisplay(edges);
        }
    }
}
