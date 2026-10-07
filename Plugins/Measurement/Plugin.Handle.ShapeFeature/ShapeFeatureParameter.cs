using System.ComponentModel;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.ShapeFeature
{
    /// <summary>
    /// 形状特征测量通用参数。
    /// 支持物理系数换算与单位定制。
    /// </summary>
    public class ShapeFeatureParameter : BaseMeasureParameter
    {
        public ShapeFeatureParameter()
        {
            UnitName = "";
            CalibScale = 1.0;
        }

        public override IHandleParameter Clone()
        {
            var p = new ShapeFeatureParameter();
            CopyBaseTo(p);
            return p;
        }

        public override void Reset()
        {
            ResetBase();
            UnitName = "";
            CalibScale = 1.0;
        }
    }
}
