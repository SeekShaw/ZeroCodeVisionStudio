using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroCode.Vision.SDK
{
    /// <summary>
    /// 触发信号来源枚举。
    /// 指定相机触发信号的来源渠道：软触发命令或指定 I/O 硬件线。
    /// </summary>
    public enum ETriggerSource
    {
        /// <summary>软触发：通过软件命令（发送触发指令）触发相机采图。</summary>
        Software,

        /// <summary>硬件 Line0：来自 I/O Line0 的外部硬触发信号。</summary>
        Line0,

        /// <summary>硬件 Line1：来自 I/O Line1 的外部硬触发信号。</summary>
        Line1,

        /// <summary>硬件 Line2：来自 I/O Line2 的外部硬触发信号。</summary>
        Line2,

        /// <summary>硬件 Line3：来自 I/O Line3 的外部硬触发信号。</summary>
        Line3,

        /// <summary>硬件 Line4：来自 I/O Line4 的外部硬触发信号。</summary>
        Line4,

        /// <summary>硬件 Line5：来自 I/O Line5 的外部硬触发信号。</summary>
        Line5,
    }
}
