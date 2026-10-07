using System.ComponentModel;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.ContourMeasure
{
    /// <summary>
    /// 轮廓测量算子通用参数。
    /// 支持轮廓平滑去噪预处理与物理尺寸标定。
    /// </summary>
    public class ContourMeasureParameter : BaseMeasureParameter
    {
        private bool smoothContours = false;
        private int smoothNumPoints = 5;

        public ContourMeasureParameter()
        {
            UnitName = "px";
        }

        [DisplayName("平滑轮廓预处理")]
        [Description("是否在测量前对 XLD 轮廓进行多边形平滑滤波以去除噪点抖动")]
        public bool SmoothContours
        {
            get => smoothContours;
            set => SetProperty(ref smoothContours, value);
        }

        [DisplayName("平滑滤波窗口大小")]
        [Description("平滑滤波的采样点数 (通常为奇数，如 3, 5, 7, 9)")]
        public int SmoothNumPoints
        {
            get => smoothNumPoints;
            set => SetProperty(ref smoothNumPoints, value < 3 ? 3 : value);
        }

        public override IHandleParameter Clone()
        {
            var p = new ContourMeasureParameter
            {
                SmoothContours = SmoothContours,
                SmoothNumPoints = SmoothNumPoints
            };
            CopyBaseTo(p);
            return p;
        }

        public override void Reset()
        {
            ResetBase();
            UnitName = "px";
            SmoothContours = false;
            SmoothNumPoints = 5;
        }
    }
}
