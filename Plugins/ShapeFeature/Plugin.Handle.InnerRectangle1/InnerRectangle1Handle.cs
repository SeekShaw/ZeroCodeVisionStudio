using System;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.InnerRectangle1
{
    [Handle("最大内接矩形", "InnerRectangle1", "求解区域的水平最大内接矩形并输出矩形区域，支持尺寸标定换算。", Common.HandleType_ShapeFeature, OperatorBackend = OperatorBackend.OpenCV)]
    public class InnerRectangle1Handle : ParameterHandleBase<FeatureMeasureParameter>, IHandleDataSlots
    {
        public InnerRectangle1Handle(IDialogService dialogService) : base(dialogService)
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
                "InnerRectangle1");

            HOperatorSet.InnerRectangle1(region, out var row1, out var col1, out var row2, out var col2);
            HOperatorSet.GenRectangle1(out var rect, row1, col1, row2, col2);

            RunParameter.SetOutput(RunOutputKeys.InnerRectangle1Row1, row1);
            RunParameter.SetOutput(RunOutputKeys.InnerRectangle1Column1, col1);
            RunParameter.SetOutput(RunOutputKeys.InnerRectangle1Row2, row2);
            RunParameter.SetOutput(RunOutputKeys.InnerRectangle1Column2, col2);
            RunParameter.HOutput = rect;
            RunParameter.SetOutput(RunOutputKeys.Output, rect);
            RunParameter.SetOutput(RunOutputKeys.Region, rect);

            var param = Parameter ?? new FeatureMeasureParameter();
            double w = (col1.Length > 0 && col2.Length > 0) ? Math.Abs(col2[0].D - col1[0].D) : 0;
            double h = (row1.Length > 0 && row2.Length > 0) ? Math.Abs(row2[0].D - row1[0].D) : 0;
            RunParameter.ResultMessage = Math.Abs(param.CalibScale - 1.0) > 1e-6
                ? $"最大内接矩形 ({T(row1)},{T(col1)})-({T(row2)},{T(col2)}) W={param.FormatMeasure(w)}, H={param.FormatMeasure(h)}"
                : $"最大内接矩形 ({T(row1)},{T(col1)})-({T(row2)},{T(col2)}) W={w:0.##}px, H={h:0.##}px";

            RaiseImageDisplay(rect);
        }

        private static string T(HTuple t) => t == null ? "" : string.Join(", ", Enumerable.Range(0, t.Length).Select(i => t[i].D.ToString("0.###")));
    }
}
