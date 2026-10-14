using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.ReduceDomain
{
    public class ReduceDomainParameter : BindableBase, IHandleParameter
    {
        private EShapeType shapeType = EShapeType.undefined;
        private double row1;
        private double column1;
        private double row2 = 100;
        private double column2 = 100;
        private double row = 50;
        private double column = 50;
        private double phi;
        private double length1 = 50;
        private double length2 = 25;
        private double radius = 50;

        [DisplayName("形状类型")]
        [Description("ROI 形状类型：未定义 / 轴对齐矩形 / 任意角矩形 / 圆形")]
        public EShapeType ShapeType { get => shapeType; set => SetProperty(ref shapeType, value); }

        [DisplayName("起点行")]
        [Description("轴对齐矩形左上角行坐标")]
        public double Row1 { get => row1; set => SetProperty(ref row1, value); }

        [DisplayName("起点列")]
        [Description("轴对齐矩形左上角列坐标")]
        public double Column1 { get => column1; set => SetProperty(ref column1, value); }

        [DisplayName("终点行")]
        [Description("轴对齐矩形右下角行坐标")]
        public double Row2 { get => row2; set => SetProperty(ref row2, value); }

        [DisplayName("终点列")]
        [Description("轴对齐矩形右下角列坐标")]
        public double Column2 { get => column2; set => SetProperty(ref column2, value); }

        [DisplayName("中心行")]
        [Description("圆形或任意角矩形的中心行坐标")]
        public double Row { get => row; set => SetProperty(ref row, value); }

        [DisplayName("中心列")]
        [Description("圆形或任意角矩形的中心列坐标")]
        public double Column { get => column; set => SetProperty(ref column, value); }

        [DisplayName("旋转角")]
        [Description("任意角矩形的旋转角（弧度）")]
        public double Phi { get => phi; set => SetProperty(ref phi, value); }

        [DisplayName("半长轴")]
        [Description("任意角矩形的半长（Length1）")]
        public double Length1 { get => length1; set => SetProperty(ref length1, value); }

        [DisplayName("半短轴")]
        [Description("任意角矩形的半宽（Length2）")]
        public double Length2 { get => length2; set => SetProperty(ref length2, value); }

        [DisplayName("半径")]
        [Description("圆形 ROI 的半径")]
        public double Radius { get => radius; set => SetProperty(ref radius, value); }

        public IHandleParameter Clone()
        {
            return new ReduceDomainParameter
            {
                ShapeType = ShapeType,
                Row1 = Row1,
                Column1 = Column1,
                Row2 = Row2,
                Column2 = Column2,
                Row = Row,
                Column = Column,
                Phi = Phi,
                Length1 = Length1,
                Length2 = Length2,
                Radius = Radius
            };
        }

        public void Reset()
        {
            ShapeType = EShapeType.undefined;
            Row1 = 0;
            Column1 = 0;
            Row2 = 100;
            Column2 = 100;
            Row = 50;
            Column = 50;
            Phi = 0;
            Length1 = 50;
            Length2 = 25;
            Radius = 50;
        }
    }
}
