using System;
using System.Collections.Generic;
using System.Text;

namespace AAAandSphinx.Vision.SDK
{
    /// <summary>
    /// 相机配置参数数据类，汇总相机常用的可配置项。
    /// 通过 <see cref="ICamera.GetCamConfig"/> 批量读取，或通过 <see cref="ICamera.SetCamConfig"/> 批量写入。
    /// </summary>
    public class CameraConfig
    {
        /// <summary>
        /// 触发模式（Off = 自由运行，On = 触发模式）。
        /// </summary>
        public ETriggerMode TriggerMode { get; set; }

        /// <summary>
        /// 触发信号来源（Software = 软触发，Line0~5 = 硬件 I/O 线触发）。
        /// </summary>
        public ETriggerSource TriggeSource { get; set; }

        /// <summary>
        /// 触发信号极性（RisingEdge = 上升沿，FallingEdge = 下降沿）。
        /// </summary>
        public ETriggerPolarity TriggerPolarity { get; set; }

        /// <summary>
        /// 曝光时间，单位微秒（μs）。
        /// 曝光时间越长，图像越亮，但可能引入运动模糊。
        /// </summary>
        public float ExpouseTime { get; set; }

        /// <summary>
        /// 触发信号滤波时间，单位微秒（μs）。
        /// 短于此时间的触发脉冲将被过滤，用于消除外部信号抖动干扰。
        /// </summary>
        public float TriggerFilter { get; set; }

        /// <summary>
        /// 触发延迟时间，单位微秒（μs）。
        /// 相机接收到触发信号后延迟指定时间再开始曝光，用于补偿机械传动等时序误差。
        /// </summary>
        public float TriggerDelay { get; set; }

        /// <summary>
        /// 模拟增益（Gain），无量纲。
        /// 增益越大，图像越亮，但噪声也越明显。
        /// </summary>
        public float Gain { get; set; }
    }
}
