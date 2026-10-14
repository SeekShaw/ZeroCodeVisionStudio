namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// HALCON 算子枚举值转换器。
    /// 将框架内的 C# 枚举值转换为 HALCON 算子接受的字符串参数，
    /// 集中管理所有"枚举 → HALCON 字符串"的映射关系，避免各算子中分散的硬编码字符串。
    /// </summary>
    public static class HalconValueConverter
    {
        /// <summary>
        /// 将 <see cref="DynThresholdMode"/> 枚举转换为 HALCON dyn_threshold 算子的 LightDark 参数字符串。
        /// </summary>
        /// <param name="mode">动态阈值分割模式。</param>
        /// <returns>对应的 HALCON 字符串（如 "dark"、"light"）。</returns>
        public static string ToHalconString(this DynThresholdMode mode)
        {
            return mode.ToString();
        }

        /// <summary>
        /// 将 <see cref="SelectShapeFeature"/> 枚举转换为 HALCON select_shape 算子的 Features 参数字符串。
        /// </summary>
        /// <param name="feature">区域筛选特征类型。</param>
        /// <returns>对应的 HALCON 特征字符串（如 "area"、"width"）。</returns>
        public static string ToHalconString(this SelectShapeFeature feature)
        {
            return feature switch
            {
                SelectShapeFeature.Area => "area",
                SelectShapeFeature.Width => "width",
                SelectShapeFeature.Height => "height",
                SelectShapeFeature.Circularity => "circularity",
                SelectShapeFeature.Rectangularity => "rectangularity",
                SelectShapeFeature.Anisometry => "anisometry",
                SelectShapeFeature.Row => "row",
                SelectShapeFeature.Column => "column",
                _ => "area"
            };
        }

        /// <summary>
        /// 将 <see cref="SelectShapeOperation"/> 枚举转换为 HALCON select_shape 算子的 Operation 参数字符串。
        /// </summary>
        /// <param name="operation">多特征逻辑组合方式（And/Or）。</param>
        /// <returns>"or" 或 "and"。</returns>
        public static string ToHalconString(this SelectShapeOperation operation)
        {
            return operation == SelectShapeOperation.Or ? "or" : "and";
        }

        /// <summary>
        /// 将 <see cref="MedianMaskType"/> 枚举转换为 HALCON median_image 算子的 MaskType 参数字符串。
        /// </summary>
        /// <param name="maskType">中值滤波结构元素形状（圆形/方形）。</param>
        /// <returns>"circle" 或 "square"。</returns>
        public static string ToHalconString(this MedianMaskType maskType)
        {
            return maskType == MedianMaskType.Square ? "square" : "circle";
        }

        /// <summary>
        /// 将 <see cref="SobelFilterType"/> 枚举转换为 HALCON sobel_amp 算子的 FilterType 参数字符串。
        /// </summary>
        /// <param name="filterType">Sobel 滤波类型。</param>
        /// <returns>对应的 HALCON 字符串（如 "sum_abs"、"x"、"y"）。</returns>
        public static string ToHalconString(this SobelFilterType filterType)
        {
            return filterType switch
            {
                SobelFilterType.ThinSumAbs => "thin_sum_abs",
                SobelFilterType.X => "x",
                SobelFilterType.Y => "y",
                _ => "sum_abs"
            };
        }

        /// <summary>
        /// 将 <see cref="ShapeTransMode"/> 枚举转换为 HALCON shape_trans 算子的 Type 参数字符串。
        /// </summary>
        /// <param name="mode">区域形状变换模式。</param>
        /// <returns>对应的 HALCON 字符串（如 "convex"、"rectangle1"）。</returns>
        public static string ToHalconString(this ShapeTransMode mode)
        {
            return mode switch
            {
                ShapeTransMode.Rectangle1 => "rectangle1",
                ShapeTransMode.Rectangle2 => "rectangle2",
                ShapeTransMode.InnerCircle => "inner_circle",
                ShapeTransMode.OuterCircle => "outer_circle",
                _ => "convex"
            };
        }

        /// <summary>
        /// 将 <see cref="ShapeModelMetric"/> 枚举转换为 HALCON create_shape_model 算子的 Metric 参数字符串。
        /// </summary>
        /// <param name="metric">形状模板匹配极性度量方式。</param>
        /// <returns>对应的 HALCON 字符串（如 "use_polarity"、"ignore_global_polarity"）。</returns>
        public static string ToHalconString(this ShapeModelMetric metric)
        {
            return metric switch
            {
                ShapeModelMetric.IgnoreGlobalPolarity => "ignore_global_polarity",
                ShapeModelMetric.IgnoreLocalPolarity => "ignore_local_polarity",
                _ => "use_polarity"
            };
        }

        /// <summary>
        /// 将 <see cref="ShapeModelSubPixel"/> 枚举转换为 HALCON find_shape_model 算子的 SubPixel 参数字符串。
        /// </summary>
        /// <param name="subPixel">亚像素精化方式。</param>
        /// <returns>对应的 HALCON 字符串（如 "least_squares"、"interpolation"、"none"）。</returns>
        public static string ToHalconString(this ShapeModelSubPixel subPixel)
        {
            return subPixel switch
            {
                ShapeModelSubPixel.Interpolation => "interpolation",
                ShapeModelSubPixel.None => "none",
                _ => "least_squares"
            };
        }

        /// <summary>
        /// 将 <see cref="EdgesFilterType"/> 枚举转换为 HALCON edges_sub_pix 算子的 Filter 参数字符串。
        /// </summary>
        /// <param name="filterType">边缘滤波器类型。</param>
        /// <returns>对应的 HALCON 字符串（如 "canny"、"deriche1"）。</returns>
        public static string ToHalconString(this EdgesFilterType filterType)
        {
            return filterType switch
            {
                EdgesFilterType.Deriche1 => "deriche1",
                EdgesFilterType.Deriche2 => "deriche2",
                EdgesFilterType.Lanser1 => "lanser1",
                EdgesFilterType.Lanser2 => "lanser2",
                EdgesFilterType.Shen => "shen",
                EdgesFilterType.Mshen => "mshen",
                EdgesFilterType.Sobel => "sobel",
                EdgesFilterType.SobelFast => "sobel_fast",
                _ => "canny"
            };
        }

        /// <summary>
        /// 将 <see cref="LinesGaussLineModel"/> 枚举转换为 HALCON lines_gauss 算子的 LineModel 参数字符串。
        /// </summary>
        /// <param name="lineModel">线条灰度剖面模型。</param>
        /// <returns>对应的 HALCON 字符串（如 "bar-shaped"、"parabolic"、"gaussian"）。</returns>
        public static string ToHalconString(this LinesGaussLineModel lineModel)
        {
            return lineModel switch
            {
                LinesGaussLineModel.Parabolic => "parabolic",
                LinesGaussLineModel.Gaussian => "gaussian",
                _ => "bar-shaped"
            };
        }

        /// <summary>
        /// 将 <see cref="ELightDark"/> 枚举转换为 HALCON 算子的 LightDark 参数字符串。
        /// </summary>
        /// <param name="lightDark">亮暗极性。</param>
        /// <returns>"dark" 或 "light"。</returns>
        public static string ToHalconString(this ELightDark lightDark)
        {
            return lightDark == ELightDark.light ? "light" : "dark";
        }

        /// <summary>
        /// 将布尔值转换为 HALCON 常用的 "true"/"false" 字符串参数。
        /// </summary>
        /// <param name="value">布尔值。</param>
        /// <returns>"true" 或 "false"。</returns>
        public static string ToHalconString(this bool value)
        {
            return value ? "true" : "false";
        }
    }
}
