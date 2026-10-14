using System;
using System.Collections.Generic;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.ClosingRectangle
{
    [Handle("矩形闭运算", "ClosingRectangle", "使用矩形结构元素对区域执行闭运算。", Common.HandleType_RegionProcessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class ClosingRectangleHandle : ParameterHandleBase<ClosingRectangleParameter>, IHandleDataSlots
    {
        public ClosingRectangleHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Region, "输入区域", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "闭运算区域", typeof(HObject))
        };

        public override void Handle()
        {
            var region = HandleInputGuard.RequireRegion(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Region) ?? RunParameter?.HOutput,
                "ClosingRectangle");

            HOperatorSet.ClosingRectangle1(region, out var result, Math.Max(1, Parameter.Width), Math.Max(1, Parameter.Height));
            RunParameter.HOutput = result;
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.Region, result);
            RunParameter.ResultMessage = $"矩形闭运算完成 (W:{Parameter.Width}, H:{Parameter.Height})";
            RaiseImageDisplay(result);
        }
    }
}
