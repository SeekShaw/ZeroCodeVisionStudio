using System.ComponentModel;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.DistanceMeasure
{
    /// <summary>
    /// 点到点距离测量参数。
    /// </summary>
    public class DistancePPParameter : BaseMeasureParameter
    {
        private double row1 = 100;
        private double col1 = 100;
        private double row2 = 200;
        private double col2 = 200;

        [DisplayName("点1 行坐标 (Row1)")]
        [Description("第一个测点的行坐标（Y）")]
        public double Row1 { get => row1; set => SetProperty(ref row1, value); }

        [DisplayName("点1 列坐标 (Col1)")]
        [Description("第一个测点的列坐标（X）")]
        public double Col1 { get => col1; set => SetProperty(ref col1, value); }

        [DisplayName("点2 行坐标 (Row2)")]
        [Description("第二个测点的行坐标（Y）")]
        public double Row2 { get => row2; set => SetProperty(ref row2, value); }

        [DisplayName("点2 列坐标 (Col2)")]
        [Description("第二个测点的列坐标（X）")]
        public double Col2 { get => col2; set => SetProperty(ref col2, value); }

        public override IHandleParameter Clone()
        {
            var p = new DistancePPParameter
            {
                Row1 = Row1,
                Col1 = Col1,
                Row2 = Row2,
                Col2 = Col2
            };
            CopyBaseTo(p);
            return p;
        }

        public override void Reset()
        {
            ResetBase();
            Row1 = 100;
            Col1 = 100;
            Row2 = 200;
            Col2 = 200;
        }
    }

    /// <summary>
    /// 点到线距离测量参数。
    /// </summary>
    public class DistancePLParameter : BaseMeasureParameter
    {
        private double pointRow = 150;
        private double pointCol = 150;
        private double lineRow1 = 100;
        private double lineCol1 = 100;
        private double lineRow2 = 200;
        private double lineCol2 = 200;

        [DisplayName("测量点 行坐标")]
        [Description("目标测点的行坐标（Y）")]
        public double PointRow { get => pointRow; set => SetProperty(ref pointRow, value); }

        [DisplayName("测量点 列坐标")]
        [Description("目标测点的列坐标（X）")]
        public double PointCol { get => pointCol; set => SetProperty(ref pointCol, value); }

        [DisplayName("基准线 起点行")]
        [Description("基准直线起点的行坐标（Y1）")]
        public double LineRow1 { get => lineRow1; set => SetProperty(ref lineRow1, value); }

        [DisplayName("基准线 起点列")]
        [Description("基准直线起点的列坐标（X1）")]
        public double LineCol1 { get => lineCol1; set => SetProperty(ref lineCol1, value); }

        [DisplayName("基准线 终点行")]
        [Description("基准直线终点的行坐标（Y2）")]
        public double LineRow2 { get => lineRow2; set => SetProperty(ref lineRow2, value); }

        [DisplayName("基准线 终点列")]
        [Description("基准直线终点的列坐标（X2）")]
        public double LineCol2 { get => lineCol2; set => SetProperty(ref lineCol2, value); }

        public override IHandleParameter Clone()
        {
            var p = new DistancePLParameter
            {
                PointRow = PointRow,
                PointCol = PointCol,
                LineRow1 = LineRow1,
                LineCol1 = LineCol1,
                LineRow2 = LineRow2,
                LineCol2 = LineCol2
            };
            CopyBaseTo(p);
            return p;
        }

        public override void Reset()
        {
            ResetBase();
            PointRow = 150;
            PointCol = 150;
            LineRow1 = 100;
            LineCol1 = 100;
            LineRow2 = 200;
            LineCol2 = 200;
        }
    }

    /// <summary>
    /// 轮廓间距离测量参数。
    /// </summary>
    public class DistanceContoursParameter : BaseMeasureParameter
    {
        private string mode = "point_to_point";

        [DisplayName("距离计算模式")]
        [Description("可选：point_to_point（欧几里得最近点距离）或 hausdorff（豪斯多夫最大最小距离）")]
        public string Mode { get => mode; set => SetProperty(ref mode, value); }

        public override IHandleParameter Clone()
        {
            var p = new DistanceContoursParameter
            {
                Mode = Mode
            };
            CopyBaseTo(p);
            return p;
        }

        public override void Reset()
        {
            ResetBase();
            Mode = "point_to_point";
        }
    }
}
