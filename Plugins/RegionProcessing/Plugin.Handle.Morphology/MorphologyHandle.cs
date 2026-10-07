using System;
using System.Collections.Generic;
using HalconDotNet;
using HalconVisionStudio.Shared;
using Plugin.Handle.Morphology.Views;

namespace Plugin.Handle.Morphology
{
    [Handle("形态学处理", "Morphology", "使用圆形结构元素清理缺陷区域。", Common.HandleType_RegionProcessing)]
    public class MorphologyHandle : HandleBase, IHandleDataSlots
    {
        private readonly MorphologyParameter parameter;

        public MorphologyHandle(IDialogService dialogService) : base(dialogService)
        {
            parameter = new MorphologyParameter();
            HandleParameter = parameter;
            SetRunParameter(null);
        }

        protected override string DialogName => nameof(MorphologyView);

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Region, "输入区域", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "形态学区域", typeof(HObject))
        };

        public override void Handle()
        {
            var region = HandleInputGuard.RequireRegion(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Region) ?? RunParameter?.HOutput,
                "Morphology");

            var radius = Math.Max(0.5, parameter.Radius);
            switch (parameter.Operation)
            {
                case MorphologyOperation.OpeningCircle:
                    HOperatorSet.OpeningCircle(region, out var opening, radius);
                    RunParameter.HOutput = opening;
                    break;
                case MorphologyOperation.DilationCircle:
                    HOperatorSet.DilationCircle(region, out var dilation, radius);
                    RunParameter.HOutput = dilation;
                    break;
                case MorphologyOperation.ErosionCircle:
                    HOperatorSet.ErosionCircle(region, out var erosion, radius);
                    RunParameter.HOutput = erosion;
                    break;
                default:
                    HOperatorSet.ClosingCircle(region, out var closing, radius);
                    RunParameter.HOutput = closing;
                    break;
            }

            RunParameter.SetOutput(RunOutputKeys.Output, RunParameter.HOutput);
            RunParameter.SetOutput(RunOutputKeys.Region, RunParameter.HOutput);
            RunParameter.ResultMessage = $"形态学处理完成 ({parameter.Operation} 半径: {radius:0.##})";
            RaiseImageDisplay(RunParameter.HOutput);
        }
    }
}
