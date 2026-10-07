using System;
using System.Collections.Generic;
using System.Text;

namespace AAAandSphinx.Vision.SDK
{
    /// <summary>
    /// 工业相机 I/O 信号线枚举。
    /// 用于标识相机上的具体 I/O 线编号，供设置/读取 I/O 线模式和电平状态时使用。
    /// </summary>
    public enum EIOLines
    {
        /// <summary>I/O 线 0（Line0），常用于外部触发输入。</summary>
        Line0,

        /// <summary>I/O 线 1（Line1）。</summary>
        Line1,

        /// <summary>I/O 线 2（Line2）。</summary>
        Line2,

        /// <summary>I/O 线 3（Line3）。</summary>
        Line3,

        /// <summary>I/O 线 4（Line4）。</summary>
        Line4,

        /// <summary>I/O 线 5（Line5）。</summary>
        Line5
    }
}
