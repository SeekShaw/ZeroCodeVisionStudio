using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.GenRectangle2
{
    public class GenRectangle2Parameter : BindableBase, IHandleParameter
    {
        private double row = 200;
        private double column = 200;
        private double phi = 0;
        private double length1 = 150;
        private double length2 = 80;

        [DisplayName("中心行")]
        [Description("任意角矩形中心的行坐标")]
        public double Row { get => row; set => SetProperty(ref row, value); }

        [DisplayName("中心列")]
        [Description("任意角矩形中心的列坐标")]
        public double Column { get => column; set => SetProperty(ref column, value); }

        [DisplayName("旋转角")]
        [Description("任意角矩形的旋转角（弧度）")]
        public double Phi { get => phi; set => SetProperty(ref phi, value); }

        [DisplayName("半长轴")]
        [Description("任意角矩形半长（Length1）")]
        public double Length1 { get => length1; set => SetProperty(ref length1, value); }

        [DisplayName("半短轴")]
        [Description("任意角矩形半宽（Length2）")]
        public double Length2 { get => length2; set => SetProperty(ref length2, value); }

        public IHandleParameter Clone() => new GenRectangle2Parameter
        {
            Row = Row, Column = Column, Phi = Phi, Length1 = Length1, Length2 = Length2
        };

        public void Reset()
        {
            Row = 200;
            Column = 200;
            Phi = 0;
            Length1 = 150;
            Length2 = 80;
        }
    }
}
