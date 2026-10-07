using System;
using System.Collections.Generic;
using HalconDotNet;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.UnionRegion
{
    [Handle("区域合并", "UnionRegion", "将所有选中区域合并为一个区域。", Common.HandleType_RegionProcessing)]
    public class UnionRegionHandle : HandleBase, IHandleDataSlots
    {
        public UnionRegionHandle(IDialogService dialogService) : base(dialogService)
        {
            HandleParameter = new EmptyParameter();
            SetRunParameter(null);
        }

        protected override string DialogName => Common.NoParameterPreviewDialogName;

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Region, "输入区域", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "合并区域", typeof(HObject))
        };

        public override void Handle()
        {
            var region = HandleInputGuard.RequireRegion(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Region) ?? RunParameter?.HOutput,
                "UnionRegion");

            HOperatorSet.Union1(region, out var result);
            RunParameter.HOutput = result;
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.Region, result);
            RunParameter.ResultMessage = "区域合并完成。";
            RaiseImageDisplay(result);
        }
    }
}
