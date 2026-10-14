using System;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 统一图像/区域数据抽象接口。
    /// 屏蔽底层图像引擎差异（OpenCV Mat vs Halcon HObject），
    /// 使算子实现可选择任意后端，同时上下游节点间数据传递保持兼容。
    /// </summary>
    public interface IVisionImage : IDisposable
    {
        /// <summary>图像宽度（像素）。</summary>
        int Width { get; }

        /// <summary>图像高度（像素）。</summary>
        int Height { get; }

        /// <summary>图像通道数（1=灰度，3=BGR，4=BGRA）。</summary>
        int Channels { get; }

        /// <summary>底层数据后端类型。</summary>
        OperatorBackend Backend { get; }

        /// <summary>底层 OpenCV Mat（OpenCV 后端时有值）。</summary>
        OpenCvSharp.Mat Mat { get; }

        /// <summary>底层 Halcon HObject（Halcon 后端时有值）。</summary>
        HalconDotNet.HObject HObject { get; }

        /// <summary>是否为空区域或空图像。</summary>
        bool IsEmpty { get; }
    }
}
