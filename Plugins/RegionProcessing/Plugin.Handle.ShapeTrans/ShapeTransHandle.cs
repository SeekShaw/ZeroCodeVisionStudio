using System;
using System.Collections.Generic;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.ShapeTrans
{
    [Handle("区域形状转换", "ShapeTrans", "将区域转换为凸包、矩形或圆形近似。", Common.HandleType_RegionProcessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class ShapeTransHandle : ParameterHandleBase<ShapeTransParameter>, IHandleDataSlots
    {
        public ShapeTransHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Region, "输入区域", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "转换区域", typeof(HObject))
        };

        public override void Handle()
        {
            var region = HandleInputGuard.RequireRegion(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Region) ?? RunParameter?.HOutput,
                "ShapeTrans");

            HOperatorSet.ShapeTrans(region, out var result, Parameter.Mode.ToHalconString());
            RunParameter.HOutput = result;
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.Region, result);
            RunParameter.ResultMessage = $"形状变换完成 ({Parameter.Mode})";
            RaiseImageDisplay(result);
        }
    }
}
