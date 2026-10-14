using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.GenCircle
{
    public class GenCircleParameter : BindableBase, IHandleParameter
    {
        private double row = 200;
        private double column = 200;
        private double radius = 100;

        [DisplayName("中心行")]
        [Description("圆心的行坐标")]
        public double Row { get => row; set => SetProperty(ref row, value); }

        [DisplayName("中心列")]
        [Description("圆心的列坐标")]
        public double Column { get => column; set => SetProperty(ref column, value); }

        [DisplayName("半径")]
        [Description("圆的半径（像素）")]
        public double Radius { get => radius; set => SetProperty(ref radius, value); }

        public IHandleParameter Clone() => new GenCircleParameter { Row = Row, Column = Column, Radius = Radius };

        public void Reset()
        {
            Row = 200;
            Column = 200;
            Radius = 100;
        }
    }
}
