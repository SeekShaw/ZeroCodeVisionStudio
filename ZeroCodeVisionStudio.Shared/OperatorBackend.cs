using System;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 算子后端实现分类。
    /// 标记算子的底层图像处理引擎来源，用于工具箱分组显示和运行时依赖检查。
    /// </summary>
    public enum OperatorBackend
    {
        /// <summary>未指定（默认，视为 Halcon 兼容）。</summary>
        Unknown = 0,

        /// <summary>基于 OpenCV / OpenCvSharp4 开源库实现。</summary>
        OpenCV = 1,

        /// <summary>依赖 Halcon 专有算子 API（ShapeModel、Measure 系列、XLD 等）。</summary>
        Halcon = 2,

        /// <summary>组合实现，部分功能 OpenCV、部分 Halcon。</summary>
        Hybrid = 3
    }
}
