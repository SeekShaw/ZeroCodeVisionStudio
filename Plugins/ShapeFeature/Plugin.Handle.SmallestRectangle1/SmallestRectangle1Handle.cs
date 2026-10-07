using System;
using HalconDotNet;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.SmallestRectangle1
{
    [Handle("最小外接矩形", "SmallestRectangle1", "求解区域的水平最小外接矩形并输出矩形区域，支持尺寸标定换算。", Common.HandleType_ShapeFeature)]
    public class SmallestRectangle1Handle : ParameterHandleBase<FeatureMeasureParameter>, IHandleDataSlots
    {
        public SmallestRectangle1Handle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Region, "输入区域", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "输出区域 ROI", typeof(HObject)),
            new RunSlot(RunOutputKeys.SmallestRectangle1Width, "矩形宽度", typeof(double)),
            new RunSlot(RunOutputKeys.SmallestRectangle1Height, "矩形高度", typeof(double)),
            new RunSlot(RunOutputKeys.SmallestRectangle1Row1, "左上行坐标", typeof(HTuple)),
            new RunSlot(RunOutputKeys.SmallestRectangle1Column1, "左上列坐标", typeof(HTuple)),
            new RunSlot(RunOutputKeys.SmallestRectangle1Row2, "右下行坐标", typeof(HTuple)),
            new RunSlot(RunOutputKeys.SmallestRectangle1Column2, "右下列坐标", typeof(HTuple))
        };

        public override void Handle()
        {
            var region = HandleInputGuard.RequireRegion(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Region) ?? RunParameter?.HOutput,
                "SmallestRectangle1");

            HOperatorSet.SmallestRectangle1(region, out var row1, out var col1, out var row2, out var col2);
            HOperatorSet.GenRectangle1(out var rect, row1, col1, row2, col2);

            RunParameter.SetOutput(RunOutputKeys.SmallestRectangle1Row1, row1);
            RunParameter.SetOutput(RunOutputKeys.SmallestRectangle1Column1, col1);
            RunParameter.SetOutput(RunOutputKeys.SmallestRectangle1Row2, row2);
            RunParameter.SetOutput(RunOutputKeys.SmallestRectangle1Column2, col2);
            RunParameter.HOutput = rect;
            RunParameter.SetOutput(RunOutputKeys.Output, rect);
            RunParameter.SetOutput(RunOutputKeys.Region, rect);

            var param = Parameter ?? new FeatureMeasureParameter();
            double w = (col1.Length > 0 && col2.Length > 0) ? Math.Abs(col2[0].D - col1[0].D) : 0;
            double h = (row1.Length > 0 && row2.Length > 0) ? Math.Abs(row2[0].D - row1[0].D) : 0;
            RunParameter.SetOutput(RunOutputKeys.SmallestRectangle1Width, w);
            RunParameter.SetOutput(RunOutputKeys.SmallestRectangle1Height, h);
            RunParameter.ResultMessage = Math.Abs(param.CalibScale - 1.0) > 1e-6
                ? $"最小外接矩形 ({T(row1)},{T(col1)})-({T(row2)},{T(col2)}) W={param.FormatMeasure(w)}, H={param.FormatMeasure(h)}"
                : $"最小外接矩形 ({T(row1)},{T(col1)})-({T(row2)},{T(col2)}) W={w:0.##}px, H={h:0.##}px";

            RaiseImageDisplay(rect);
        }

        private static string T(HTuple t) => t == null ? "" : string.Join(", ", Enumerable.Range(0, t.Length).Select(i => t[i].D.ToString("0.###")));
    }
}
