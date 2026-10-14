using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.AngleMeasure
{
    /// <summary>
    /// 线线夹角测量参数。
    /// </summary>
    public class AngleLLParameter : BaseMeasureParameter
    {
        private double line1Row1 = 100;
        private double line1Col1 = 100;
        private double line1Row2 = 200;
        private double line1Col2 = 100;
        private double line2Row1 = 150;
        private double line2Col1 = 100;
        private double line2Row2 = 150;
        private double line2Col2 = 200;

        public AngleLLParameter()
        {
            UnitName = "°";
        }

        [DisplayName("线1 起点行 (Row1)")]
        public double Line1Row1 { get => line1Row1; set => SetProperty(ref line1Row1, value); }

        [DisplayName("线1 起点列 (Col1)")]
        public double Line1Col1 { get => line1Col1; set => SetProperty(ref line1Col1, value); }

        [DisplayName("线1 终点行 (Row2)")]
        public double Line1Row2 { get => line1Row2; set => SetProperty(ref line1Row2, value); }

        [DisplayName("线1 终点列 (Col2)")]
        public double Line1Col2 { get => line1Col2; set => SetProperty(ref line1Col2, value); }

        [DisplayName("线2 起点行 (Row1)")]
        public double Line2Row1 { get => line2Row1; set => SetProperty(ref line2Row1, value); }

        [DisplayName("线2 起点列 (Col1)")]
        public double Line2Col1 { get => line2Col1; set => SetProperty(ref line2Col1, value); }

        [DisplayName("线2 终点行 (Row2)")]
        public double Line2Row2 { get => line2Row2; set => SetProperty(ref line2Row2, value); }

        [DisplayName("线2 终点列 (Col2)")]
        public double Line2Col2 { get => line2Col2; set => SetProperty(ref line2Col2, value); }

        public override IHandleParameter Clone()
        {
            var p = new AngleLLParameter
            {
                Line1Row1 = Line1Row1,
                Line1Col1 = Line1Col1,
                Line1Row2 = Line1Row2,
                Line1Col2 = Line1Col2,
                Line2Row1 = Line2Row1,
                Line2Col1 = Line2Col1,
                Line2Row2 = Line2Row2,
                Line2Col2 = Line2Col2
            };
            CopyBaseTo(p);
            return p;
        }

        public override void Reset()
        {
            ResetBase();
            UnitName = "°";
            Line1Row1 = 100;
            Line1Col1 = 100;
            Line1Row2 = 200;
            Line1Col2 = 100;
            Line2Row1 = 150;
            Line2Col1 = 100;
            Line2Row2 = 150;
            Line2Col2 = 200;
        }
    }

    /// <summary>
    /// 线与X轴夹角测量参数。
    /// </summary>
    public class AngleLXParameter : BaseMeasureParameter
    {
        private double row1 = 100;
        private double col1 = 100;
        private double row2 = 200;
        private double col2 = 200;

        public AngleLXParameter()
        {
            UnitName = "°";
        }

        [DisplayName("起点行 (Row1)")]
        public double Row1 { get => row1; set => SetProperty(ref row1, value); }

        [DisplayName("起点列 (Col1)")]
        public double Col1 { get => col1; set => SetProperty(ref col1, value); }

        [DisplayName("终点行 (Row2)")]
        public double Row2 { get => row2; set => SetProperty(ref row2, value); }

        [DisplayName("终点列 (Col2)")]
        public double Col2 { get => col2; set => SetProperty(ref col2, value); }

        public override IHandleParameter Clone()
        {
            var p = new AngleLXParameter
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
            UnitName = "°";
            Row1 = 100;
            Col1 = 100;
            Row2 = 200;
            Col2 = 200;
        }
    }
}
