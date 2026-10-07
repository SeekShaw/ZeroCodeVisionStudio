using System;
using HalconDotNet;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.SmallestCircle
{
    [Handle("最小外接圆", "SmallestCircle", "求解区域的最小外接圆并输出圆形区域，支持标定半径换算。", Common.HandleType_ShapeFeature)]
    public class SmallestCircleHandle : ParameterHandleBase<FeatureMeasureParameter>, IHandleDataSlots
    {
        public SmallestCircleHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Region, "输入区域", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "输出区域 ROI", typeof(HObject))
        };

        public override void Handle()
        {
            var region = HandleInputGuard.RequireRegion(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Region) ?? RunParameter?.HOutput,
                "SmallestCircle");

            HOperatorSet.SmallestCircle(region, out var row, out var column, out var radius);
            HOperatorSet.GenCircle(out var circle, row, column, radius);

            RunParameter.SetOutput(RunOutputKeys.SmallestCircleRow, row);
            RunParameter.SetOutput(RunOutputKeys.SmallestCircleColumn, column);
            RunParameter.SetOutput(RunOutputKeys.SmallestCircleRadius, radius);
            RunParameter.HOutput = circle;
            RunParameter.SetOutput(RunOutputKeys.Output, circle);
            RunParameter.SetOutput(RunOutputKeys.Region, circle);

            var param = Parameter ?? new FeatureMeasureParameter();
            double rVal = radius.Length > 0 ? radius[0].D : 0;
            RunParameter.ResultMessage = Math.Abs(param.CalibScale - 1.0) > 1e-6
                ? $"最小外接圆 Row={T(row)}, Column={T(column)}, Radius={param.FormatMeasure(rVal)}"
                : $"最小外接圆 Row={T(row)}, Column={T(column)}, Radius={T(radius)}";

            RaiseImageDisplay(circle);
        }

        private static string T(HTuple t) => t == null ? "" : string.Join(", ", Enumerable.Range(0, t.Length).Select(i => t[i].D.ToString("0.###")));
    }
}
