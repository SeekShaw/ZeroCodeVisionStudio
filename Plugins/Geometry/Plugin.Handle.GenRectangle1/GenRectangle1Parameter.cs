using System.ComponentModel;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.GenRectangle1
{
    public class GenRectangle1Parameter : BindableBase, IHandleParameter
    {
        private double row1 = 100;
        private double column1 = 100;
        private double row2 = 300;
        private double column2 = 300;

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

        public IHandleParameter Clone() => new GenRectangle1Parameter { Row1 = Row1, Column1 = Column1, Row2 = Row2, Column2 = Column2 };

        public void Reset()
        {
            Row1 = 100;
            Column1 = 100;
            Row2 = 300;
            Column2 = 300;
        }
    }
}
