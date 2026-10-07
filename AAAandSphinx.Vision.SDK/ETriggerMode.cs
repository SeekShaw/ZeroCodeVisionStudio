using System;
using System.Collections.Generic;
using System.Text;

namespace AAAandSphinx.Vision.SDK
{
    /// <summary>
    /// 相机触发模式枚举。
    /// 控制相机是否处于外部触发等待状态。
    /// </summary>
    public enum ETriggerMode
    {
        /// <summary>关闭触发模式（自由运行模式）：相机以设定帧率连续采图，不等待外部触发信号。</summary>
        Off,

        /// <summary>开启触发模式：相机等待触发信号（软触发或硬触发）后才采集一帧图像。</summary>
        On
    }
}
