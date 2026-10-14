namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 动态阈值（dyn_threshold）分割模式枚举。
    /// 对应 HALCON dyn_threshold 算子的 LightDark 参数，
    /// 指定相对于局部背景，目标区域的灰度特征。
    /// </summary>
    public enum DynThresholdMode
    {
        /// <summary>提取比局部背景暗的区域（暗目标）。</summary>
        dark,

        /// <summary>提取比局部背景亮的区域（亮目标）。</summary>
        light,

        /// <summary>提取与局部背景亮度相近的区域（差值接近零）。</summary>
        equal,

        /// <summary>提取与局部背景亮度不同的区域（差值较大）。</summary>
        not_equal
    }

    /// <summary>
    /// 形态学操作类型枚举（圆形结构元素）。
    /// 用于选择对区域执行哪种形态学运算，对应 HALCON 中的 opening/closing/dilation/erosion 系列算子。
    /// </summary>
    public enum MorphologyOperation
    {
        /// <summary>开运算（先腐蚀后膨胀），去除小的噪点区域。</summary>
        OpeningCircle,

        /// <summary>闭运算（先膨胀后腐蚀），填充小的孔洞。</summary>
        ClosingCircle,

        /// <summary>膨胀运算，扩大区域边界。</summary>
        DilationCircle,

        /// <summary>腐蚀运算，收缩区域边界。</summary>
        ErosionCircle
    }

    /// <summary>
    /// 区域筛选特征枚举（select_shape 算子）。
    /// 指定用于筛选区域的形状特征属性，对应 HALCON select_shape 算子的 Features 参数。
    /// </summary>
    public enum SelectShapeFeature
    {
        /// <summary>面积（像素数）。</summary>
        Area,

        /// <summary>区域最小外接矩形的宽度。</summary>
        Width,

        /// <summary>区域最小外接矩形的高度。</summary>
        Height,

        /// <summary>圆形度（0~1，越接近 1 越接近圆形）。</summary>
        Circularity,

        /// <summary>矩形度（0~1，越接近 1 越接近矩形）。</summary>
        Rectangularity,

        /// <summary>各向异性（长宽比，越接近 1 越接近正方形/圆形）。</summary>
        Anisometry,

        /// <summary>区域重心的行坐标。</summary>
        Row,

        /// <summary>区域重心的列坐标。</summary>
        Column
    }

    /// <summary>
    /// 多特征筛选的逻辑组合方式枚举（select_shape 算子）。
    /// 指定多个特征条件的组合逻辑：And 表示必须同时满足，Or 表示满足其一即可。
    /// </summary>
    public enum SelectShapeOperation
    {
        /// <summary>所有特征条件必须同时满足（交集）。</summary>
        And,

        /// <summary>任意一个特征条件满足即可（并集）。</summary>
        Or
    }

    /// <summary>
    /// 中值滤波结构元素形状枚举（median_image 算子）。
    /// 指定中值滤波使用圆形或方形邻域进行灰度排序。
    /// </summary>
    public enum MedianMaskType
    {
        /// <summary>圆形邻域，各方向滤波效果更均匀。</summary>
        Circle,

        /// <summary>方形邻域，计算效率更高。</summary>
        Square
    }

    /// <summary>
    /// Sobel 滤波类型枚举（sobel_amp 算子）。
    /// 指定 Sobel 梯度滤波的输出方式和方向。
    /// </summary>
    public enum SobelFilterType
    {
        /// <summary>X、Y 方向梯度绝对值之和（sum_abs）：综合边缘强度。</summary>
        SumAbs,

        /// <summary>精化的梯度绝对值之和（thin_sum_abs）：边缘更细。</summary>
        ThinSumAbs,

        /// <summary>仅 X 方向（水平）梯度。</summary>
        X,

        /// <summary>仅 Y 方向（垂直）梯度。</summary>
        Y
    }

    /// <summary>
    /// 区域形状变换模式枚举（shape_trans 算子）。
    /// 指定将区域转换为何种几何形状。
    /// </summary>
    public enum ShapeTransMode
    {
        /// <summary>凸包变换（convex），将区域转换为其凸多边形近似。</summary>
        Convex,

        /// <summary>最小轴对齐外接矩形（rectangle1）。</summary>
        Rectangle1,

        /// <summary>最小任意角度外接矩形（rectangle2）。</summary>
        Rectangle2,

        /// <summary>最大内接圆（inner_circle）。</summary>
        InnerCircle,

        /// <summary>最小外接圆（outer_circle）。</summary>
        OuterCircle
    }

    /// <summary>
    /// 形状模板匹配极性度量枚举（create_shape_model 算子的 Metric 参数）。
    /// 控制模板匹配时如何处理目标与模板的灰度极性关系。
    /// </summary>
    public enum ShapeModelMetric
    {
        /// <summary>使用极性（use_polarity）：模板和目标的边缘方向必须一致，抗干扰能力最强。</summary>
        UsePolarity,

        /// <summary>忽略全局极性（ignore_global_polarity）：允许整体亮暗反转，适用于打光不稳定的场景。</summary>
        IgnoreGlobalPolarity,

        /// <summary>忽略局部极性（ignore_local_polarity）：允许局部边缘方向不一致，适用于复杂纹理。</summary>
        IgnoreLocalPolarity
    }

    /// <summary>
    /// 形状模板匹配亚像素精度模式枚举（find_shape_model 算子的 SubPixel 参数）。
    /// 控制匹配结果的位置精度。
    /// </summary>
    public enum ShapeModelSubPixel
    {
        /// <summary>最小二乘法（least_squares）：最高精度，适用于精密定位场景。</summary>
        LeastSquares,

        /// <summary>插值法（interpolation）：精度适中，速度较快。</summary>
        Interpolation,

        /// <summary>不进行亚像素精化（none）：仅返回像素级精度，速度最快。</summary>
        None
    }

    /// <summary>
    /// 亚像素边缘提取滤波器类型枚举（edges_sub_pix 算子的 Filter 参数）。
    /// </summary>
    public enum EdgesFilterType
    {
        /// <summary>Canny 边缘检测器，综合效果好，默认推荐。</summary>
        Canny,

        /// <summary>Deriche 一阶滤波器。</summary>
        Deriche1,

        /// <summary>Deriche 二阶滤波器。</summary>
        Deriche2,

        /// <summary>Lanser 一阶滤波器。</summary>
        Lanser1,

        /// <summary>Lanser 二阶滤波器。</summary>
        Lanser2,

        /// <summary>Shen 滤波器。</summary>
        Shen,

        /// <summary>改进的 Shen 滤波器（mshen）。</summary>
        Mshen,

        /// <summary>Sobel 滤波器。</summary>
        Sobel,

        /// <summary>快速 Sobel 滤波器（sobel_fast）。</summary>
        SobelFast
    }

    /// <summary>
    /// 高斯线条检测线模型枚举（lines_gauss 算子的 LineModel 参数）。
    /// </summary>
    public enum LinesGaussLineModel
    {
        /// <summary>条状模型（bar-shaped）：适用于宽度近似恒定的线条。</summary>
        BarShaped,

        /// <summary>抛物线模型（parabolic）：适用于灰度剖面呈抛物线形的线条。</summary>
        Parabolic,

        /// <summary>高斯模型（gaussian）：适用于灰度剖面呈高斯形的线条。</summary>
        Gaussian
    }
}
