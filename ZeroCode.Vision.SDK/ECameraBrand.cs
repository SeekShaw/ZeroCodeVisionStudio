using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroCode.Vision.SDK
{
    /// <summary>
    /// 相机品牌枚举。
    /// 用于标识工业相机的制造商，便于根据品牌加载对应的 SDK 驱动实现。
    /// </summary>
    public enum ECameraBrand
    {
        /// <summary>海康威视（Hikvision）工业相机。</summary>
        HK,

        /// <summary>巴斯勒（Basler）工业相机（通过 HALCON 接口访问）。</summary>
        Basler,

        /// <summary>迈迪威视（MindVision）工业相机。</summary>
        MindVision
    }
}
