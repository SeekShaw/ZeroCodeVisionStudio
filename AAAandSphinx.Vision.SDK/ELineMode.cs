using System;
using System.Collections.Generic;
using System.Text;

namespace AAAandSphinx.Vision.SDK
{
    /// <summary>
    /// I/O 信号线工作模式枚举。
    /// 决定某条 I/O 线是作为输入信号接收外部触发，还是作为输出信号控制外部设备。
    /// </summary>
    public enum ELineMode
    {
        /// <summary>输入模式，相机从此线接收外部信号（如外部触发信号）。</summary>
        Input,

        /// <summary>输出模式，相机通过此线向外部设备输出控制信号（如频闪灯控制）。</summary>
        Output
    }
}
