using System;
using System.Collections.Generic;
using HalconDotNet;
using HalconVisionStudio.Shared;
using Plugin.Handle.SelectShape.Views;

namespace Plugin.Handle.SelectShape
{
    [Handle("形状筛选", "SelectShape", "根据形状特征筛选连通缺陷候选区域。", Common.HandleType_RegionProcessing)]
    public class SelectShapeHandle : HandleBase, IHandleDataSlots
    {
        private readonly SelectShapeParameter parameter;

        public SelectShapeHandle(IDialogService dialogService) : base(dialogService)
        {
            parameter = new SelectShapeParameter();
            HandleParameter = parameter;
            SetRunParameter(null);
        }

        protected override string DialogName => nameof(SelectShapeView);

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Region, "输入区域", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "筛选区域", typeof(HObject))
        };

        public override void Handle()
        {
            var region = HandleInputGuard.RequireRegion(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Region) ?? RunParameter?.HOutput,
                "SelectShape");

            var min = Math.Min(parameter.Min, parameter.Max);
            var max = Math.Max(parameter.Min, parameter.Max);
            HOperatorSet.SelectShape(
                region,
                out var selectedRegions,
                parameter.Feature.ToHalconString(),
                parameter.Operation.ToHalconString(),
                min,
                max);

            RunParameter.HOutput = selectedRegions;
            RunParameter.SetOutput(RunOutputKeys.Output, selectedRegions);
            RunParameter.SetOutput(RunOutputKeys.Region, selectedRegions);
            RunParameter.ResultMessage = $"形状筛选完成 ({parameter.Feature} [{min:0.##}, {max:0.##}])";
            RaiseImageDisplay(selectedRegions);
        }
    }
}
