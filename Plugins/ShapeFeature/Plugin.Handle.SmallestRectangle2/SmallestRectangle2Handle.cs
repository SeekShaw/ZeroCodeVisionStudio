using System;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.SmallestRectangle2
{
    [Handle("最小方向矩形", "SmallestRectangle2", "求解区域的有方向最小外接矩形并输出矩形区域，支持半长标定换算。", Common.HandleType_ShapeFeature, OperatorBackend = OperatorBackend.OpenCV)]
    public class SmallestRectangle2Handle : ParameterHandleBase<FeatureMeasureParameter>, IHandleDataSlots
    {
        public SmallestRectangle2Handle(IDialogService dialogService) : base(dialogService)
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
                "SmallestRectangle2");

            HOperatorSet.SmallestRectangle2(region, out var row, out var col, out var phi, out var len1, out var len2);
            HOperatorSet.GenRectangle2(out var rect, row, col, phi, len1, len2);

            RunParameter.SetOutput(RunOutputKeys.SmallestRectangle2Row, row);
            RunParameter.SetOutput(RunOutputKeys.SmallestRectangle2Column, col);
            RunParameter.SetOutput(RunOutputKeys.SmallestRectangle2Phi, phi);
            RunParameter.SetOutput(RunOutputKeys.SmallestRectangle2Length1, len1);
            RunParameter.SetOutput(RunOutputKeys.SmallestRectangle2Length2, len2);
            RunParameter.HOutput = rect;
            RunParameter.SetOutput(RunOutputKeys.Output, rect);
            RunParameter.SetOutput(RunOutputKeys.Region, rect);

            var param = Parameter ?? new FeatureMeasureParameter();
            double l1 = len1.Length > 0 ? len1[0].D : 0;
            double l2 = len2.Length > 0 ? len2[0].D : 0;
            RunParameter.ResultMessage = Math.Abs(param.CalibScale - 1.0) > 1e-6
                ? $"最小方向矩形 Row={T(row)}, Phi={T(phi)}, L1={param.FormatMeasure(l1)}, L2={param.FormatMeasure(l2)}"
                : $"最小方向矩形 Row={T(row)}, Phi={T(phi)}, L1={T(len1)}, L2={T(len2)}";

            RaiseImageDisplay(rect);
        }

        private static string T(HTuple t) => t == null ? "" : string.Join(", ", Enumerable.Range(0, t.Length).Select(i => t[i].D.ToString("0.###")));
    }
}
