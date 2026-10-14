using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroCode.Vision.SDK
{
    /// <summary>
    /// 硬触发信号极性枚举。
    /// 指定相机响应外部触发信号的有效边沿：上升沿或下降沿。
    /// </summary>
    public enum ETriggerPolarity
    {
        /// <summary>上升沿触发：信号从低电平变为高电平时触发采图。</summary>
        RisingEdge,

        /// <summary>下降沿触发：信号从高电平变为低电平时触发采图。</summary>
        FallingEdge
    }
}
