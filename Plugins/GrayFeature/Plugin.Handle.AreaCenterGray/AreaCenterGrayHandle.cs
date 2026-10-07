using System;
using System.Collections.Generic;
using System.Linq;
using HalconDotNet;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.AreaCenterGray
{
    [Handle("灰度加权重心", "AreaCenterGray", "以灰度值为权重计算区域的重心坐标，支持标定换算，适用于斑点定位。", Common.HandleType_GrayFeature)]
    public class AreaCenterGrayHandle : ParameterHandleBase<FeatureMeasureParameter>, IHandleDataSlots
    {
        public AreaCenterGrayHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.GrayImage, "灰度图像", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "输入区域", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "输出区域 ROI", typeof(HObject)),
            new RunSlot(RunOutputKeys.AreaCenterGrayArea, "灰度加权面积", typeof(HTuple)),
            new RunSlot(RunOutputKeys.AreaCenterGrayRow, "灰度重心 Row", typeof(HTuple)),
            new RunSlot(RunOutputKeys.AreaCenterGrayColumn, "灰度重心 Column", typeof(HTuple))
        };

        public override void Handle()
        {
            var region = HandleInputGuard.RequireRegion(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Region) ?? RunParameter?.HOutput,
                "AreaCenterGray");
            var image = HandleInputGuard.RequireImage(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.GrayImage) ?? RunParameter?.GrayImage ?? RunParameter?.RawImage,
                "AreaCenterGray",
                "灰度图像");

            HOperatorSet.AreaCenterGray(region, image, out var area, out var row, out var col);

            RunParameter.SetOutput(RunOutputKeys.AreaCenterGrayArea, area);
            RunParameter.SetOutput(RunOutputKeys.AreaCenterGrayRow, row);
            RunParameter.SetOutput(RunOutputKeys.AreaCenterGrayColumn, col);
            RunParameter.HOutput = region;
            RunParameter.SetOutput(RunOutputKeys.Output, region);
            RunParameter.SetOutput(RunOutputKeys.Region, region);

            var param = Parameter ?? new FeatureMeasureParameter();
            double areaVal = area.Length > 0 ? area[0].D : 0;
            double areaPhys = areaVal * param.CalibScale * param.CalibScale;
            string unit = string.IsNullOrWhiteSpace(param.UnitName) ? "px" : param.UnitName;
            RunParameter.ResultMessage = Math.Abs(param.CalibScale - 1.0) > 1e-6
                ? $"Area={areaVal:0.##} px² ({areaPhys:0.###} {unit}²)  Row={T(row)}  Column={T(col)}"
                : $"Area={T(area)}  Row={T(row)}  Column={T(col)}";

            RaiseImageDisplay(region);
        }

        private static string T(HTuple t) => t == null ? "" : string.Join(", ", Enumerable.Range(0, t.Length).Select(i => t[i].D.ToString("0.###")));
    }
}
