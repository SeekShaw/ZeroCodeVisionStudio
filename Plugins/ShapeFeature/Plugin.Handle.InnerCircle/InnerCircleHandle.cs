using HalconDotNet;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.InnerCircle
{
    [Handle("最大内接圆", "InnerCircle", "求解区域的最大内接圆并输出圆形区域，支持标定半径换算。", Common.HandleType_ShapeFeature)]
    public class InnerCircleHandle : ParameterHandleBase<FeatureMeasureParameter>, IHandleDataSlots
    {
        public InnerCircleHandle(IDialogService dialogService) : base(dialogService)
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
                "InnerCircle");

            HOperatorSet.InnerCircle(region, out var row, out var col, out var radius);
            HOperatorSet.GenCircle(out var circle, row, col, radius);

            RunParameter.SetOutput(RunOutputKeys.InnerCircleRow, row);
            RunParameter.SetOutput(RunOutputKeys.InnerCircleColumn, col);
            RunParameter.SetOutput(RunOutputKeys.InnerCircleRadius, radius);
            RunParameter.HOutput = circle;
            RunParameter.SetOutput(RunOutputKeys.Output, circle);
            RunParameter.SetOutput(RunOutputKeys.Region, circle);

            var param = Parameter ?? new FeatureMeasureParameter();
            double rVal = radius.Length > 0 ? radius[0].D : 0;
            RunParameter.ResultMessage = Math.Abs(param.CalibScale - 1.0) > 1e-6
                ? $"最大内接圆 Row={T(row)}, Col={T(col)}, R={param.FormatMeasure(rVal)}"
                : $"最大内接圆 Row={T(row)}, Col={T(col)}, R={T(radius)}";

            RaiseImageDisplay(circle);
        }

        private static string T(HTuple t) => t == null ? "" : string.Join(", ", Enumerable.Range(0, t.Length).Select(i => t[i].D.ToString("0.###")));
    }
}
