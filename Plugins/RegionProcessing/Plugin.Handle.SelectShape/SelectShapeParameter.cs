using System.ComponentModel;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.SelectShape
{
    public class SelectShapeParameter : BindableBase, IHandleParameter
    {
        private SelectShapeFeature feature = SelectShapeFeature.Area;
        private SelectShapeOperation operation = SelectShapeOperation.And;
        private double min = 50;
        private double max = 99999999;

        [DisplayName("特征")]
        [Description("用于筛选的形状特征：面积、宽度、高度、圆形度、矩形度等")]
        public SelectShapeFeature Feature { get => feature; set => SetProperty(ref feature, value); }

        [DisplayName("逻辑运算")]
        [Description("多特征条件组合方式：And（同时满足）/ Or（满足其一）")]
        public SelectShapeOperation Operation { get => operation; set => SetProperty(ref operation, value); }

        [DisplayName("最小值")]
        [Description("特征允许的最小值")]
        public double Min { get => min; set => SetProperty(ref min, value); }

        [DisplayName("最大值")]
        [Description("特征允许的最大值")]
        public double Max { get => max; set => SetProperty(ref max, value); }

        public IHandleParameter Clone()
        {
            return new SelectShapeParameter
            {
                Feature = Feature,
                Operation = Operation,
                Min = Min,
                Max = Max
            };
        }

        public void Reset()
        {
            Feature = SelectShapeFeature.Area;
            Operation = SelectShapeOperation.And;
            Min = 50;
            Max = 99999999;
        }
    }
}
