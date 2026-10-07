using System.ComponentModel;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.SetShapeModelOrigin
{
    public class SetShapeModelOriginParameter : BindableBase, IHandleParameter
    {
        private double originRow;
        private double originColumn;

        [DisplayName("原点行坐标")]
        [Description("形状模板原点的行坐标（相对模板坐标系）。")]
        public double OriginRow { get => originRow; set => SetProperty(ref originRow, value); }

        [DisplayName("原点列坐标")]
        [Description("形状模板原点的列坐标（相对模板坐标系）。")]
        public double OriginColumn { get => originColumn; set => SetProperty(ref originColumn, value); }

        public IHandleParameter Clone()
        {
            return new SetShapeModelOriginParameter
            {
                OriginRow = OriginRow,
                OriginColumn = OriginColumn
            };
        }

        public void Reset()
        {
            OriginRow = 0;
            OriginColumn = 0;
        }
    }
}
