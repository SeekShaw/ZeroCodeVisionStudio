using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.GenEllipse
{
    public class GenEllipseParameter : BindableBase, IHandleParameter
    {
        private double row = 200;
        private double column = 200;
        private double phi = 0;
        private double radius1 = 120;
        private double radius2 = 60;

        [DisplayName("中心行")]
        [Description("椭圆中心的行坐标")]
        public double Row { get => row; set => SetProperty(ref row, value); }

        [DisplayName("中心列")]
        [Description("椭圆中心的列坐标")]
        public double Column { get => column; set => SetProperty(ref column, value); }

        [DisplayName("旋转角")]
        [Description("椭圆主轴旋转角（弧度）")]
        public double Phi { get => phi; set => SetProperty(ref phi, value); }

        [DisplayName("长轴半径")]
        [Description("椭圆长半轴半径（Radius1）")]
        public double Radius1 { get => radius1; set => SetProperty(ref radius1, value); }

        [DisplayName("短轴半径")]
        [Description("椭圆短半轴半径（Radius2）")]
        public double Radius2 { get => radius2; set => SetProperty(ref radius2, value); }

        public IHandleParameter Clone() => new GenEllipseParameter
        {
            Row = Row, Column = Column, Phi = Phi, Radius1 = Radius1, Radius2 = Radius2
        };

        public void Reset()
        {
            Row = 200;
            Column = 200;
            Phi = 0;
            Radius1 = 120;
            Radius2 = 60;
        }
    }
}
