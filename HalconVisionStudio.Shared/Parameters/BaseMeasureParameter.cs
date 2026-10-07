using System;
using System.ComponentModel;
using Prism.Mvvm;

namespace HalconVisionStudio.Shared
{
    /// <summary>
    /// 测量类算子通用的基础参数。
    /// 包含像素物理标定当量与工程单位换算。
    /// </summary>
    public abstract class BaseMeasureParameter : BindableBase, IHandleParameter
    {
        private double calibScale = 1.0;
        private string unitName = "px";

        /// <summary>
        /// 像素当量（1 像素对应多少物理单位，例如 0.05 mm/px）。默认 1.0。
        /// </summary>
        [DisplayName("像素当量")]
        [Description("1 个像素对应的物理长度比例尺（如 0.05 mm/px，输入 1.0 即为像素值）")]
        public double CalibScale
        {
            get => calibScale;
            set => SetProperty(ref calibScale, value <= 0 ? 1.0 : value);
        }

        /// <summary>
        /// 物理显示单位（例如 "px", "mm", "um"）。默认 "px"。
        /// </summary>
        [DisplayName("测量单位")]
        [Description("测量结果显示的工程单位")]
        public string UnitName
        {
            get => unitName;
            set => SetProperty(ref unitName, string.IsNullOrWhiteSpace(value) ? "px" : value.Trim());
        }

        /// <summary>
        /// 将测量像素值换算为物理数值。
        /// </summary>
        public double ToPhysical(double pixelVal) => pixelVal * CalibScale;

        /// <summary>
        /// 格式化测量数值输出描述。
        /// </summary>
        public string FormatMeasure(double pixelVal)
        {
            var phys = ToPhysical(pixelVal);
            if (Math.Abs(CalibScale - 1.0) < 1e-9 && UnitName == "px")
            {
                return $"{pixelVal:0.###} px";
            }
            return $"{phys:0.###} {UnitName} (像素: {pixelVal:0.###} px)";
        }

        public abstract IHandleParameter Clone();
        public abstract void Reset();

        protected void CopyBaseTo(BaseMeasureParameter target)
        {
            target.CalibScale = CalibScale;
            target.UnitName = UnitName;
        }

        protected void ResetBase()
        {
            CalibScale = 1.0;
            UnitName = "px";
        }
    }
}
