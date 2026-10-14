using System.ComponentModel;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 通用特征测量参数。
    /// 适用于区域特征、轮廓几何特征和灰度特征测量算子，支持像素物理当量换算与工程单位配置。
    /// </summary>
    public class FeatureMeasureParameter : BaseMeasureParameter
    {
        public FeatureMeasureParameter()
        {
            ResetBase();
        }

        public override IHandleParameter Clone()
        {
            var p = new FeatureMeasureParameter();
            CopyBaseTo(p);
            return p;
        }

        public override void Reset()
        {
            ResetBase();
        }
    }
}
