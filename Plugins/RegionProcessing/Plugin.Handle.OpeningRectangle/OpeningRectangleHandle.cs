using System;
using System.Collections.Generic;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.OpeningRectangle
{
    [Handle("矩形开运算", "OpeningRectangle", "使用矩形结构元素对区域执行开运算。", Common.HandleType_RegionProcessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class OpeningRectangleHandle : ParameterHandleBase<OpeningRectangleParameter>, IHandleDataSlots
    {
        public OpeningRectangleHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Region, "输入区域", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "开运算区域", typeof(HObject))
        };

        public override void Handle()
        {
            var region = HandleInputGuard.RequireRegion(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Region) ?? RunParameter?.HOutput,
                "OpeningRectangle");

            HOperatorSet.OpeningRectangle1(region, out var result, Math.Max(1, Parameter.Width), Math.Max(1, Parameter.Height));
            RunParameter.HOutput = result;
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.Region, result);
            RunParameter.ResultMessage = $"矩形开运算完成 (W:{Parameter.Width}, H:{Parameter.Height})";
            RaiseImageDisplay(result);
        }
    }
}
