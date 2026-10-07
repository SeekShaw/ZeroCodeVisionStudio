using System;
using System.Collections.Generic;
using System.Text;

namespace AAAandSphinx.Vision.SDK.Halcon
{
    /// <summary>
    /// HALCON 图像采集接口（Interface）类型枚举。
    /// 在 HALCON 中通过 open_framegrabber 或 HFramegrabber 连接相机时，
    /// 需要指定对应的接口名称（Name 参数）。此枚举列出常用的接口类型。
    /// </summary>
    public enum EInterfaceType
    {
        /// <summary>DirectFile：直接从文件读取图像（模拟采集，用于离线测试）。</summary>
        DirectFile,

        /// <summary>DirectShow：通过 DirectShow 接口访问摄像头（适用于 USB 网络摄像头等）。</summary>
        DirectShow,

        /// <summary>File：从文件系统读取图像序列（支持多种图像格式）。</summary>
        File,

        /// <summary>GenICamTL：通过 GenICam 传输层访问符合 GenICam 标准的相机（GigE、USB3 等）。</summary>
        GenICamTL,

        /// <summary>GigEVision2：通过 GigE Vision 2.0 协议直接访问网口工业相机。</summary>
        GigEVision2,

        /// <summary>MediaFoundation：通过 Windows Media Foundation 接口访问摄像头。</summary>
        MediaFoundation,

        /// <summary>MindVision_X64：通过迈迪威视 SDK（64位）访问迈迪威视工业相机。</summary>
        MindVision_X64,

        /// <summary>MVision：通过另一种迈迪威视接口访问相机。</summary>
        MVision,

        /// <summary>RealSense：通过 Intel RealSense SDK 访问 RealSense 深度/彩色相机。</summary>
        RealSense,

        /// <summary>USB3Vision：通过 USB3 Vision 协议访问 USB3 工业相机。</summary>
        USB3Vision
    }
}
