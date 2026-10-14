using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 框架内置异常类型枚举。
    /// 用于标识 <see cref="HandleException"/> 的具体错误原因，便于调用方精确捕获和处理。
    /// </summary>
    public enum ExceptionType
    {
        /// <summary>图像为 null，未能获取有效图像数据。</summary>
        E_IMAGE_NULL,

        /// <summary>区域为 null，未能获取有效区域数据。</summary>
        E_REGION_NULL,

        /// <summary>指定的文件未找到。</summary>
        E_FILE_NOT_FOUND,

        /// <summary>尝试注册一个已存在的算子键名。</summary>
        E_HANDLE_ALREADY_REGISTERED,

        /// <summary>模板ID为空。</summary>
        E_MODEL_ID_NULL,

        /// <summary>轮廓为空。</summary>
        E_CONTOUR_NULL,

        /// <summary>元组/数据为空。</summary>
        E_TUPLE_NULL,

        /// <summary>参数设置无效。</summary>
        E_PARAMETER_INVALID,

        /// <summary>上游节点未执行。</summary>
        E_UPSTREAM_NOT_EXECUTED,

        /// <summary>没有有效的输入数据。</summary>
        E_NO_VALID_INPUT,
    }

    /// <summary>
    /// ROI（感兴趣区域）形状类型枚举。
    /// 用于标识用户在图像上绘制的 ROI 形状，供形状绘制对话框和相关算子使用。
    /// </summary>
    public enum EShapeType
    {
        /// <summary>未定义形状。</summary>
        undefined = 0,

        /// <summary>轴对齐矩形（rectangle1），由左上角和右下角坐标定义。</summary>
        rectangle1 = 1,

        /// <summary>任意角度矩形（rectangle2），由中心、角度和半轴长度定义。</summary>
        rectangle2 = 2,

        /// <summary>圆形，由圆心和半径定义。</summary>
        circle = 3,
    }

    /// <summary>
    /// 亮暗极性枚举。
    /// 用于指定阈值分割等算子中目标区域相对于背景的灰度极性。
    /// </summary>
    public enum ELightDark
    {
        /// <summary>目标区域比背景暗（暗目标）。</summary>
        dark = 0,

        /// <summary>目标区域比背景亮（亮目标）。</summary>
        light = 1
    }

    /// <summary>
    /// 框架公共常量和配置类。
    /// 集中定义算子分类名称、对话框名称以及参数传递键名等字符串常量，避免魔术字符串分散在各处。
    /// </summary>
    public class Common
    {
        // ==================== 算子分类常量 ====================

        /// <summary>图像处理类算子的分类名称。</summary>
        public const string HandleType_ImageProcessing = "图像处理";

        /// <summary>图像输入类算子的分类名称（相机采集、文件读取等）。</summary>
        public const string HandleType_ImageInput = "图像输入";

        /// <summary>图像预处理类算子的分类名称（滤波、灰度转换等）。</summary>
        public const string HandleType_Preprocessing = "图像预处理";

        /// <summary>图像分割类算子的分类名称（阈值、动态阈值等）。</summary>
        public const string HandleType_Segmentation = "图像分割";

        /// <summary>区域处理类算子的分类名称（形态学、连通域等）。</summary>
        public const string HandleType_RegionProcessing = "区域处理";

        /// <summary>缺陷检测类算子的分类名称。</summary>
        public const string HandleType_Measurement = "缺陷检测";

        /// <summary>流程逻辑类算子的分类名称（逻辑运算/计数/延时/变量/分支/触发/抽检）。</summary>
        public const string HandleType_Logic = "流程逻辑";

        /// <summary>几何生成类算子的分类名称。</summary>
        public const string HandleType_Geometry = "几何生成";

        /// <summary>形状模板匹配类算子的分类名称。</summary>
        public const string HandleType_ShapeMatching = "形状匹配";

        /// <summary>结果输出类算子的分类名称（显示文本、显示图像等）。</summary>
        public const string HandleType_ResultOutput = "结果输出";

        /// <summary>形状特征测量类算子的分类名称（外接圆/矩形、内接圆/矩形、面积重心等）。</summary>
        public const string HandleType_ShapeFeature = "形状特征";

        /// <summary>灰度特征测量类算子的分类名称（灰度最值、平均灰度等）。</summary>
        public const string HandleType_GrayFeature = "灰度特征";

        // ==================== 对话框参数键名 ====================

        /// <summary>向参数对话框传递算子参数对象时使用的键名。</summary>
        public const string HandleParameter_ParamName = "Model";

        /// <summary>向参数对话框传递算子实例（IHandle）时使用的键名。</summary>
        public const string HandleParameter_HandleName = "Handle";

        /// <summary>向参数对话框传递运行模型（IHandleRunModel）时使用的键名。</summary>
        public const string HandleParameter_RunModel = "RunModel";

        /// <summary>向参数对话框传递输入绑定编辑器列表时使用的键名。</summary>
        public const string HandleParameter_InputBindings = "InputBindings";

        // ==================== 对话框注册名称 ====================

        /// <summary>无参数预览对话框的注册名称（用于 Prism IDialogService）。</summary>
        public const string NoParameterPreviewDialogName = "NoParameterPreviewDialog";

        /// <summary>通用参数编辑对话框的注册名称（用于 Prism IDialogService）。</summary>
        public const string ParameterDialogName = "ParameterDialog";

        /// <summary>形状绘制对话框的注册名称（用于 Prism IDialogService）。</summary>
        public const string ShapeDrawDialogName = "ShapeDrawDialog";

        /// <summary>测量绘制模式参数键名。</summary>
        public const string ShapeDrawModeKey = "ShapeType";
    }
}
