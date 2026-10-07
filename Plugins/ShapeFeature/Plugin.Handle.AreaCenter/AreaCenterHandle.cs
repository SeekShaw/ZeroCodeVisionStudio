using HalconDotNet;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.AreaCenter
{
    [Handle("区域面积与重心", "AreaCenter", "计算区域的像素面积和几何重心坐标，支持标定换算，输出透传原始区域。", Common.HandleType_ShapeFeature)]
    public class AreaCenterHandle : ParameterHandleBase<FeatureMeasureParameter>, IHandleDataSlots
    {
        public AreaCenterHandle(IDialogService dialogService) : base(dialogService)
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
            new RunSlot(RunOutputKeys.AreaCenterCount, "目标数量", typeof(int)),
            new RunSlot(RunOutputKeys.AreaCenterArea, "区域面积", typeof(HTuple)),
            new RunSlot(RunOutputKeys.AreaCenterRow, "重心行坐标", typeof(HTuple)),
            new RunSlot(RunOutputKeys.AreaCenterColumn, "重心列坐标", typeof(HTuple))
        };

        public override void Handle()
        {
            var region = HandleInputGuard.RequireRegion(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Region) ?? RunParameter?.HOutput,
                "AreaCenter");

            HOperatorSet.AreaCenter(region, out var area, out var row, out var col);

            int count = area != null ? area.Length : 0;
            RunParameter.SetOutput(RunOutputKeys.AreaCenterCount, count);
            RunParameter.SetOutput(RunOutputKeys.AreaCenterArea, area);
            RunParameter.SetOutput(RunOutputKeys.AreaCenterRow, row);
            RunParameter.SetOutput(RunOutputKeys.AreaCenterColumn, col);
            RunParameter.HOutput = region;
            RunParameter.SetOutput(RunOutputKeys.Output, region);
            RunParameter.SetOutput(RunOutputKeys.Region, region);

            var param = Parameter ?? new FeatureMeasureParameter();
            double areaVal = area.Length > 0 ? area[0].D : 0;
            double areaPhys = areaVal * param.CalibScale * param.CalibScale;
            string unit = string.IsNullOrWhiteSpace(param.UnitName) ? "px" : param.UnitName;
            RunParameter.ResultMessage = Math.Abs(param.CalibScale - 1.0) > 1e-6
                ? $"面积={areaVal:0.##} px² ({areaPhys:0.###} {unit}²) 重心({T(row)}, {T(col)})"
                : $"面积={T(area)} px² 重心({T(row)}, {T(col)})";

            RaiseImageDisplay(region);
        }

        private static string T(HTuple t) => t == null ? "" : string.Join(", ", Enumerable.Range(0, t.Length).Select(i => t[i].D.ToString("0.###")));
    }
}
