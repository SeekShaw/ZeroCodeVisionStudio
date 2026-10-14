using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.ContourProcessing
{
    /// <summary>
    /// 轮廓平滑参数。
    /// </summary>
    public class SmoothContoursXldParameter : BindableBase, IHandleParameter
    {
        private int smoothingFactor = 5;

        [DisplayName("平滑因子")]
        [Description("平滑因子，值越大平滑效果越强")]
        public int SmoothingFactor { get => smoothingFactor; set => SetProperty(ref smoothingFactor, value); }

        public IHandleParameter Clone() => new SmoothContoursXldParameter { SmoothingFactor = SmoothingFactor };

        public void Reset()
        {
            SmoothingFactor = 5;
        }
    }

    /// <summary>
    /// 轮廓分割参数。
    /// </summary>
    public class SegmentContoursXldParameter : BindableBase, IHandleParameter
    {
        private string mode = "lines";
        private int maxLineWidth = 4;
        private int pointOrderThreshold = 7;

        [DisplayName("分割模式")]
        [Description("可选值：lines（直线）、lines_circles（直线和圆弧）、lines_ellipses（直线和椭圆）")]
        public string Mode { get => mode; set => SetProperty(ref mode, value); }

        [DisplayName("最大线宽")]
        [Description("分割时允许的最大线宽（像素）")]
        public int MaxLineWidth { get => maxLineWidth; set => SetProperty(ref maxLineWidth, value); }

        [DisplayName("点序阈值")]
        [Description("点序平滑阈值，值越大对噪声越不敏感")]
        public int PointOrderThreshold { get => pointOrderThreshold; set => SetProperty(ref pointOrderThreshold, value); }

        public IHandleParameter Clone() => new SegmentContoursXldParameter
        {
            Mode = Mode,
            MaxLineWidth = MaxLineWidth,
            PointOrderThreshold = PointOrderThreshold
        };

        public void Reset()
        {
            Mode = "lines";
            MaxLineWidth = 4;
            PointOrderThreshold = 7;
        }
    }

    /// <summary>
    /// 轮廓选择参数。
    /// </summary>
    public class SelectContoursXldParameter : BindableBase, IHandleParameter
    {
        private string feature = "contour_length";
        private double minValue = 20;
        private double maxValue = 99999;

        [DisplayName("特征名称")]
        [Description("可选值：contour_length、contour_length_closed、area、circularity、closed、open 等 HALCON select_contours_xld 特征")]
        public string Feature { get => feature; set => SetProperty(ref feature, value); }

        [DisplayName("最小值")]
        [Description("特征允许的最小值")]
        public double MinValue { get => minValue; set => SetProperty(ref minValue, value); }

        [DisplayName("最大值")]
        [Description("特征允许的最大值")]
        public double MaxValue { get => maxValue; set => SetProperty(ref maxValue, value); }

        public IHandleParameter Clone() => new SelectContoursXldParameter
        {
            Feature = Feature,
            MinValue = MinValue,
            MaxValue = MaxValue
        };

        public void Reset()
        {
            Feature = "contour_length";
            MinValue = 20;
            MaxValue = 99999;
        }
    }

    /// <summary>
    /// 轮廓拟合参数。
    /// </summary>
    public class FitLineContourXldParameter : BindableBase, IHandleParameter
    {
        private string algorithm = "tukey";
        private int maxNumIterations = -1;
        private double clippingFactor = 2.0;

        [DisplayName("算法")]
        [Description("可选值：tukey、huber、regression、gauss")]
        public string Algorithm { get => algorithm; set => SetProperty(ref algorithm, value); }

        [DisplayName("最大迭代次数")]
        [Description("最大迭代次数，-1 表示自动")]
        public int MaxNumIterations { get => maxNumIterations; set => SetProperty(ref maxNumIterations, value); }

        [DisplayName("裁剪因子")]
        [Description("稳健拟合的裁剪因子，值越大对离群点越宽容")]
        public double ClippingFactor { get => clippingFactor; set => SetProperty(ref clippingFactor, value); }

        public IHandleParameter Clone() => new FitLineContourXldParameter
        {
            Algorithm = Algorithm,
            MaxNumIterations = MaxNumIterations,
            ClippingFactor = ClippingFactor
        };

        public void Reset()
        {
            Algorithm = "tukey";
            MaxNumIterations = -1;
            ClippingFactor = 2.0;
        }
    }

    /// <summary>
    /// 轮廓圆拟合参数。
    /// </summary>
    public class FitCircleContourXldParameter : BindableBase, IHandleParameter
    {
        private string algorithm = "atukey";
        private int maxNumIterations = -1;
        private double clippingFactor = 2.0;

        [DisplayName("算法")]
        [Description("可选值：algebraic、ahuber、atukey、geotukey")]
        public string Algorithm { get => algorithm; set => SetProperty(ref algorithm, value); }

        [DisplayName("最大迭代次数")]
        [Description("最大迭代次数，-1 表示自动")]
        public int MaxNumIterations { get => maxNumIterations; set => SetProperty(ref maxNumIterations, value); }

        [DisplayName("裁剪因子")]
        [Description("稳健拟合的裁剪因子，值越大对离群点越宽容")]
        public double ClippingFactor { get => clippingFactor; set => SetProperty(ref clippingFactor, value); }

        public IHandleParameter Clone() => new FitCircleContourXldParameter
        {
            Algorithm = Algorithm,
            MaxNumIterations = MaxNumIterations,
            ClippingFactor = ClippingFactor
        };

        public void Reset()
        {
            Algorithm = "atukey";
            MaxNumIterations = -1;
            ClippingFactor = 2.0;
        }
    }

    /// <summary>
    /// 轮廓椭圆拟合参数。
    /// </summary>
    public class FitEllipseContourXldParameter : BindableBase, IHandleParameter
    {
        private string algorithm = "atukey";
        private int maxNumIterations = -1;
        private double clippingFactor = 2.0;

        [DisplayName("算法")]
        [Description("可选值：algebraic、ahuber、atukey、geotukey")]
        public string Algorithm { get => algorithm; set => SetProperty(ref algorithm, value); }

        [DisplayName("最大迭代次数")]
        [Description("最大迭代次数，-1 表示自动")]
        public int MaxNumIterations { get => maxNumIterations; set => SetProperty(ref maxNumIterations, value); }

        [DisplayName("裁剪因子")]
        [Description("稳健拟合的裁剪因子，值越大对离群点越宽容")]
        public double ClippingFactor { get => clippingFactor; set => SetProperty(ref clippingFactor, value); }

        public IHandleParameter Clone() => new FitEllipseContourXldParameter
        {
            Algorithm = Algorithm,
            MaxNumIterations = MaxNumIterations,
            ClippingFactor = ClippingFactor
        };

        public void Reset()
        {
            Algorithm = "atukey";
            MaxNumIterations = -1;
            ClippingFactor = 2.0;
        }
    }

    /// <summary>
    /// 轮廓相邻联合参数。
    /// </summary>
    public class UnionAdjacentContoursXldParameter : BindableBase, IHandleParameter
    {
        private double maxDistAbs = 10.0;
        private double maxDistRel = 1.0;
        private string mode = "attr_keep";

        [DisplayName("最大绝对距离")]
        [Description("两轮廓端点之间的最大绝对距离 (px)")]
        public double MaxDistAbs { get => maxDistAbs; set => SetProperty(ref maxDistAbs, value); }

        [DisplayName("最大相对距离")]
        [Description("两轮廓端点之间的最大相对距离")]
        public double MaxDistRel { get => maxDistRel; set => SetProperty(ref maxDistRel, value); }

        [DisplayName("属性模式")]
        [Description("属性保留模式：attr_keep, attr_forget")]
        public string Mode { get => mode; set => SetProperty(ref mode, value); }

        public IHandleParameter Clone() => new UnionAdjacentContoursXldParameter
        {
            MaxDistAbs = MaxDistAbs,
            MaxDistRel = MaxDistRel,
            Mode = Mode
        };

        public void Reset()
        {
            MaxDistAbs = 10.0;
            MaxDistRel = 1.0;
            Mode = "attr_keep";
        }
    }

    /// <summary>
    /// 轮廓闭合参数。
    /// </summary>
    public class CloseContoursXldParameter : BindableBase, IHandleParameter
    {
        private double maxDist = 10.0;

        [DisplayName("最大闭合间距")]
        [Description("允许闭合的首尾端点最大距离 (px)")]
        public double MaxDist { get => maxDist; set => SetProperty(ref maxDist, value); }

        public IHandleParameter Clone() => new CloseContoursXldParameter
        {
            MaxDist = MaxDist
        };

        public void Reset()
        {
            MaxDist = 10.0;
        }
    }
}
