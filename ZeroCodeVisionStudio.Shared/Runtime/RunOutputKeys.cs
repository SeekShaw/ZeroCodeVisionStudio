namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// RunParameter 命名输出的公共键名常量类。
    /// 所有通过 RunParameter.Outputs 传递的具名数据均应使用此处定义的常量，
    /// 避免各算子自行定义字符串键名导致拼写不一致和难以维护的问题。
    /// <para>
    /// 使用示例：
    /// <code>
    /// // 写入输出
    /// RunParameter.SetOutput(RunOutputKeys.Region, region);
    /// // 读取输出
    /// RunParameter.TryGetOutput&lt;HRegion&gt;(RunOutputKeys.Region, out var region);
    /// </code>
    /// </para>
    /// </summary>
    public static class RunOutputKeys
    {
        // ==================== 基础图像/区域 ====================

        /// <summary>主输出（图像或区域），是节点间默认传递的核心数据。</summary>
        public const string Output = "output";

        // ==================== 执行状态（引擎每轮执行后写入，全节点常驻输出） ====================

        /// <summary>是否成功：true=成功，false=失败（软失败/硬失败均为 false）。可被下游绑定或用于执行条件。</summary>
        public const string ExecutionSuccess = "exec.success";

        /// <summary>执行结果：成功 / 失败 / 跳过。可被下游绑定（如 $执行结果$ == "失败"）。</summary>
        public const string ExecutionStatus = "exec.status";

        /// <summary>原始（彩色）图像，由相机采集或文件读取节点写入。</summary>
        public const string RawImage = "image.raw";

        /// <summary>灰度图像，由 RGB 转灰度等预处理节点写入。</summary>
        public const string GrayImage = "image.gray";

        /// <summary>区域（HRegion），由阈值分割、形态学等节点写入。</summary>
        public const string Region = "region";

        /// <summary>亚像素轮廓（HXLD），由边缘检测等节点写入。</summary>
        public const string Xld = "xld";

        // ==================== 缺陷检测结果 ====================

        /// <summary>缺陷数量，由缺陷分析节点写入。</summary>
        public const string DefectCount = "defect.count";

        /// <summary>缺陷总面积（像素），由缺陷分析节点写入。</summary>
        public const string TotalDefectArea = "defect.total_area";

        // ==================== 形状模板匹配结果 ====================

        /// <summary>形状模板 ID，由创建/读取模板节点写入，供匹配节点使用。</summary>
        public const string ShapeModelId = "shape_model.id";
        public const string ShapeModel_ModelRow  = "shape_model.modelrow";
        public const string ShapeModel_ModelCol  = "shape_model.modelcol";
        /// <summary>形状模板轮廓，用于叠加显示模板边缘。</summary>
        public const string ShapeModelContours = "shape_model.contours";

        /// <summary>匹配结果的行坐标数组（HTuple）。</summary>
        public const string MatchRows = "shape_match.rows";

        /// <summary>匹配结果的列坐标数组（HTuple）。</summary>
        public const string MatchColumns = "shape_match.columns";

        /// <summary>匹配结果的角度数组（HTuple，弧度）。</summary>
        public const string MatchAngles = "shape_match.angles";

        /// <summary>匹配结果的得分数组（HTuple，0~1）。</summary>
        public const string MatchScores = "shape_match.scores";

        // ==================== 显示叠加层 ====================

        /// <summary>叠加显示的文字内容，由显示信息节点写入。</summary>
        public const string DisplayText = "display.text";

        /// <summary>叠加文字的前景色（HTML 颜色字符串，如 "#FFFFFF"）。</summary>
        public const string DisplayTextColor = "display.text_color";

        /// <summary>叠加文字的背景色（HTML 颜色字符串，如 "#80000000"）。</summary>
        public const string DisplayBackgroundColor = "display.background_color";

        /// <summary>结果消息字符串，显示在状态栏中。</summary>
        public const string ResultMessage = "result.message";

        // ==================== 几何测量结果：最小外接圆 ====================

        /// <summary>最小外接圆的圆心行坐标。</summary>
        public const string SmallestCircleRow = "geometry.smallest_circle.row";

        /// <summary>最小外接圆的圆心列坐标。</summary>
        public const string SmallestCircleColumn = "geometry.smallest_circle.column";

        /// <summary>最小外接圆的半径。</summary>
        public const string SmallestCircleRadius = "geometry.smallest_circle.radius";

        // ==================== 几何测量结果：最小外接矩形1（轴对齐） ====================

        /// <summary>最小轴对齐外接矩形左上角行坐标。</summary>
        public const string SmallestRectangle1Row1 = "geometry.smallest_rectangle1.row1";

        /// <summary>最小轴对齐外接矩形左上角列坐标。</summary>
        public const string SmallestRectangle1Column1 = "geometry.smallest_rectangle1.column1";

        /// <summary>最小轴对齐外接矩形右下角行坐标。</summary>
        public const string SmallestRectangle1Row2 = "geometry.smallest_rectangle1.row2";

        /// <summary>最小轴对齐外接矩形右下角列坐标。</summary>
        public const string SmallestRectangle1Column2 = "geometry.smallest_rectangle1.column2";

        /// <summary>最小轴对齐外接矩形宽度。</summary>
        public const string SmallestRectangle1Width = "geometry.smallest_rectangle1.width";

        /// <summary>最小轴对齐外接矩形高度。</summary>
        public const string SmallestRectangle1Height = "geometry.smallest_rectangle1.height";

        // ==================== 几何测量结果：最小外接矩形2（任意角度） ====================

        /// <summary>最小任意角度外接矩形的中心行坐标。</summary>
        public const string SmallestRectangle2Row = "geometry.smallest_rectangle2.row";

        /// <summary>最小任意角度外接矩形的中心列坐标。</summary>
        public const string SmallestRectangle2Column = "geometry.smallest_rectangle2.column";

        /// <summary>最小任意角度外接矩形的旋转角（弧度）。</summary>
        public const string SmallestRectangle2Phi = "geometry.smallest_rectangle2.phi";

        /// <summary>最小任意角度外接矩形的半长轴长度。</summary>
        public const string SmallestRectangle2Length1 = "geometry.smallest_rectangle2.length1";

        /// <summary>最小任意角度外接矩形的半短轴长度。</summary>
        public const string SmallestRectangle2Length2 = "geometry.smallest_rectangle2.length2";

        // ==================== 几何测量结果：最大内接圆 ====================

        /// <summary>最大内接圆的圆心行坐标。</summary>
        public const string InnerCircleRow = "geometry.inner_circle.row";

        /// <summary>最大内接圆的圆心列坐标。</summary>
        public const string InnerCircleColumn = "geometry.inner_circle.column";

        /// <summary>最大内接圆的半径。</summary>
        public const string InnerCircleRadius = "geometry.inner_circle.radius";

        // ==================== 几何测量结果：最大轴对齐内接矩形 ====================

        /// <summary>最大轴对齐内接矩形左上角行坐标。</summary>
        public const string InnerRectangle1Row1 = "geometry.inner_rectangle1.row1";

        /// <summary>最大轴对齐内接矩形左上角列坐标。</summary>
        public const string InnerRectangle1Column1 = "geometry.inner_rectangle1.column1";

        /// <summary>最大轴对齐内接矩形右下角行坐标。</summary>
        public const string InnerRectangle1Row2 = "geometry.inner_rectangle1.row2";

        /// <summary>最大轴对齐内接矩形右下角列坐标。</summary>
        public const string InnerRectangle1Column2 = "geometry.inner_rectangle1.column2";

        // ==================== 区域特征：AreaCenter ====================

        /// <summary>区域面积（像素数）。</summary>
        public const string AreaCenterArea = "region.area";

        /// <summary>区域几何重心行坐标。</summary>
        public const string AreaCenterRow = "region.center_row";

        /// <summary>区域几何重心列坐标。</summary>
        public const string AreaCenterColumn = "region.center_column";

        /// <summary>区域计数（独立连通域数量）。</summary>
        public const string AreaCenterCount = "region.count";

        // ==================== 区域特征：灰度加权重心 ====================

        /// <summary>灰度加权面积（等效灰度总和）。</summary>
        public const string AreaCenterGrayArea = "region.gray_area";

        /// <summary>灰度加权重心行坐标。</summary>
        public const string AreaCenterGrayRow = "region.gray_center_row";

        /// <summary>灰度加权重心列坐标。</summary>
        public const string AreaCenterGrayColumn = "region.gray_center_column";

        // ==================== 区域特征：灰度统计 ====================

        /// <summary>区域内灰度最小值。</summary>
        public const string MinGray = "region.min_gray";

        /// <summary>区域内灰度最大值。</summary>
        public const string MaxGray = "region.max_gray";

        /// <summary>区域内灰度范围（最大-最小）。</summary>
        public const string RangeGray = "region.range_gray";

        // ==================== 区域特征：平均灰度与标准差 ====================

        /// <summary>区域内平均灰度值。</summary>
        public const string IntensityMean = "region.intensity_mean";

        /// <summary>区域内灰度标准差。</summary>
        public const string IntensityDeviation = "region.intensity_deviation";

        // ==================== 距离测量结果 ====================

        /// <summary>距离值（像素）。</summary>
        public const string Distance = "measure.distance";

        /// <summary>物理距离值（经标定换算后的工程单位值）。</summary>
        public const string PhysicalDistance = "measure.physical_distance";

        /// <summary>测量判定结果（true 为 OK，false 为 NG）。</summary>
        public const string IsOK = "measure.is_ok";

        /// <summary>测量值与标准值的公差偏差。</summary>
        public const string ToleranceDev = "measure.tolerance_dev";

        /// <summary>测量图形标注（XLD 轮廓）。</summary>
        public const string MeasurementOverlay = "measure.overlay";

        /// <summary>距离起点行坐标。</summary>
        public const string DistanceRow1 = "measure.distance.row1";

        /// <summary>距离起点列坐标。</summary>
        public const string DistanceColumn1 = "measure.distance.column1";

        /// <summary>距离终点行坐标。</summary>
        public const string DistanceRow2 = "measure.distance.row2";

        /// <summary>距离终点列坐标。</summary>
        public const string DistanceColumn2 = "measure.distance.column2";

        // ==================== 角度测量结果 ====================

        /// <summary>角度值（度）。</summary>
        public const string Angle = "measure.angle";

        /// <summary>弧度值。</summary>
        public const string Radian = "measure.radian";

        // ==================== 轮廓测量结果 ====================

        /// <summary>轮廓。</summary>
        public const string Contour = "contour";

        /// <summary>轮廓长度。</summary>
        public const string ContourLength = "measure.contour_length";

        // ==================== 流程逻辑结果 ====================

        /// <summary>逻辑运算结果 / 计数器取模命中。</summary>
        public const string LogicResult = "logic.result";

        /// <summary>逻辑明细（各路输入状态 / 分支名 / 抽检阶段）。</summary>
        public const string LogicDetail = "logic.detail";

        /// <summary>条件分流真分支。</summary>
        public const string TrueBranch = "logic.branch_true";

        /// <summary>条件分流假分支。</summary>
        public const string FalseBranch = "logic.branch_false";

        /// <summary>计数器累计值。</summary>
        public const string CounterCount = "logic.count";

        /// <summary>计数器连续命中数。</summary>
        public const string CounterConsecutive = "logic.consecutive";

        /// <summary>计数器取模余数。</summary>
        public const string CounterModIndex = "logic.mod_index";

        /// <summary>边沿触发本轮是否命中。</summary>
        public const string EdgeTriggered = "logic.triggered";

        /// <summary>抽检判定：累计件数。</summary>
        public const string SampleIndex = "logic.sample_index";

        /// <summary>抽检判定：本轮是否需要检测。</summary>
        public const string ShouldInspect = "logic.should_inspect";

        /// <summary>变量值（文本）。</summary>
        public const string VariableValueText = "logic.var_text";

        /// <summary>变量值（数值）。</summary>
        public const string VariableValueNumber = "logic.var_number";

        /// <summary>变量值（布尔）。</summary>
        public const string VariableValueBool = "logic.var_bool";
    }
}
