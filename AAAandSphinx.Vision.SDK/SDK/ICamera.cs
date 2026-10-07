using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace AAAandSphinx.Vision.SDK
{
    /// <summary>
    /// 工业相机统一接口。
    /// 封装不同品牌相机（海康、巴斯勒等）的通用操作，包括设备枚举、图像采集、
    /// 触发控制和 I/O 参数配置。具体品牌的 SDK 实现继承此接口。
    /// </summary>
    public interface ICamera : IDisposable
    {
        /// <summary>
        /// 相机图像回调事件。
        /// 相机在连续采集模式下每次曝光完成后触发，传递采集到的图像（参数1：相机SN，参数2：Bitmap图像）。
        /// </summary>
        event Action<string, Bitmap> ImageCallbackEvent;

        /// <summary>相机的序列号（Serial Number），用于唯一标识一台相机。</summary>
        string SN { get; set; }

        /// <summary>
        /// 关闭并释放相机设备。
        /// 释放 SDK 内部资源，关闭相机连接。
        /// </summary>
        /// <returns>操作成功返回 true，否则返回 false。</returns>
        bool CloseDevice();

        /// <summary>
        /// 枚举当前系统中所有可用的相机设备。
        /// </summary>
        /// <returns>可用相机序列号（SN）列表；未发现相机时返回空列表。</returns>
        List<string> GetCameraEnum();

        /// <summary>
        /// 以硬触发模式等待并获取一帧图像。
        /// 调用后阻塞，直到相机回调中收到图像或超时。
        /// </summary>
        /// <param name="image">成功时输出采集到的 Bitmap 图像，失败时为 null。</param>
        /// <param name="timeout">等待超时时间（毫秒），默认 3000ms。</param>
        /// <returns>成功获取图像返回 true，超时或失败返回 false。</returns>
        bool GetImage(out Bitmap image, int timeout = 3000);

        /// <summary>
        /// 发送软触发命令并同步等待采集一帧图像。
        /// 内部自动完成：启动采集 → 切换为软触发模式 → 发送软触发 → 等待图像 → 恢复原触发模式 → 停止采集。
        /// </summary>
        /// <param name="image">成功时输出采集到的 Bitmap 图像，失败时为 null。</param>
        /// <param name="timeout">等待超时时间（毫秒），默认 3000ms。</param>
        /// <returns>成功获取图像返回 true，否则返回 false。</returns>
        bool GetImageOnSoftTrigger(out Bitmap image, int timeout = 3000);

        /// <summary>
        /// 初始化并打开指定序列号的相机。
        /// 必须在其他操作之前调用，失败时不可进行后续采集操作。
        /// </summary>
        /// <param name="CamSN">要打开的相机序列号。</param>
        /// <returns>初始化成功返回 true，否则返回 false。</returns>
        bool InitizeDevice(string CamSN);

        /// <summary>
        /// 发送一次软触发命令。
        /// 需要相机已处于触发模式（<see cref="ETriggerMode.On"/>）且触发源为 Software。
        /// </summary>
        /// <returns>命令发送成功返回 true，否则返回 false。</returns>
        bool SoftTrigger();

        /// <summary>
        /// 切换到硬触发模式并开始采集。
        /// 会自动将 <see cref="ETriggerSource.Software"/> 修正为 <see cref="ETriggerSource.Line0"/>。
        /// </summary>
        /// <param name="triggerSource">硬触发信号来源，仅支持 Line0~Line5。</param>
        /// <returns>启动成功返回 true，否则返回 false。</returns>
        bool StartWitchHardTriggerMode(ETriggerSource triggerSource);

        /// <summary>
        /// 切换到软触发模式并开始采集（关闭触发，即自由运行模式）。
        /// </summary>
        /// <returns>启动成功返回 true，否则返回 false。</returns>
        bool StartWitchSoftTriggerMode();

        /// <summary>
        /// 停止图像采集（停止 Grab），释放采集资源。
        /// </summary>
        /// <returns>操作成功返回 true，否则返回 false。</returns>
        bool StopGrabbing();

        #region 相机参数配置

        /// <summary>
        /// 执行自动白平衡校正（适用于彩色相机）。
        /// </summary>
        /// <returns>操作成功返回 true，否则返回 false。</returns>
        bool AutoBalanceWhite();

        /// <summary>
        /// 批量读取相机当前所有配置参数（曝光、增益、触发模式等）。
        /// </summary>
        /// <param name="config">输出的相机配置数据对象。</param>
        void GetCamConfig(out CameraConfig config);

        /// <summary>
        /// 获取当前曝光时间。
        /// </summary>
        /// <param name="value">输出的曝光时间（微秒）。</param>
        /// <returns>读取成功返回 true，否则返回 false。</returns>
        bool GetExpouseTime(out float value);

        /// <summary>
        /// 获取当前模拟增益值。
        /// </summary>
        /// <param name="gain">输出的增益值（无量纲）。</param>
        /// <returns>读取成功返回 true，否则返回 false。</returns>
        bool GetGain(out float gain);

        /// <summary>
        /// 读取指定 I/O 线当前的电平状态。
        /// </summary>
        /// <param name="line">要读取的 I/O 线编号。</param>
        /// <param name="lineStatus">输出的电平状态（High/Low）。</param>
        /// <returns>读取成功返回 true，否则返回 false。</returns>
        bool GetLineStatus(EIOLines line, out ELineStatus lineStatus);

        /// <summary>
        /// 获取当前触发延迟时间。
        /// </summary>
        /// <param name="delay">输出的触发延迟时间（微秒）。</param>
        /// <returns>读取成功返回 true，否则返回 false。</returns>
        bool GetTriggerDelay(out float delay);

        /// <summary>
        /// 获取当前触发滤波时间。
        /// </summary>
        /// <param name="flitertime">输出的触发滤波时间（微秒）。</param>
        /// <returns>读取成功返回 true，否则返回 false。</returns>
        bool GetTriggerFliter(out float flitertime);

        /// <summary>
        /// 获取当前触发模式及触发来源。
        /// </summary>
        /// <param name="mode">输出的触发模式（Off/On）。</param>
        /// <param name="hardTriggerModel">输出的触发来源（Software/Line0~5）。</param>
        /// <returns>读取成功返回 true，否则返回 false。</returns>
        bool GetTriggerMode(out ETriggerMode mode, out ETriggerSource hardTriggerModel);

        /// <summary>
        /// 获取当前硬触发信号极性。
        /// </summary>
        /// <param name="polarity">输出的触发极性（RisingEdge/FallingEdge）。</param>
        /// <returns>读取成功返回 true，否则返回 false。</returns>
        bool GetTriggerPolarity(out ETriggerPolarity polarity);

        /// <summary>
        /// 批量写入相机配置参数（曝光、增益、触发模式等）。
        /// </summary>
        /// <param name="config">包含所有待写入参数的配置对象。</param>
        void SetCamConfig(CameraConfig config);

        /// <summary>
        /// 设置曝光时间。
        /// </summary>
        /// <param name="value">曝光时间（微秒）。</param>
        /// <returns>设置成功返回 true，否则返回 false。</returns>
        bool SetExpouseTime(float value);

        /// <summary>
        /// 设置模拟增益。
        /// </summary>
        /// <param name="gain">增益值（无量纲），具体范围参考相机规格书。</param>
        /// <returns>设置成功返回 true，否则返回 false。</returns>
        bool SetGain(float gain);

        /// <summary>
        /// 设置指定 I/O 线的工作模式（输入/输出）。
        /// </summary>
        /// <param name="line">I/O 线编号。</param>
        /// <param name="mode">工作模式（Input/Output）。</param>
        /// <returns>设置成功返回 true，否则返回 false。</returns>
        bool SetLineMode(EIOLines line, ELineMode mode);

        /// <summary>
        /// 设置指定 I/O 线（输出模式）的电平状态。
        /// 仅对工作模式为 Output 的线有效。
        /// </summary>
        /// <param name="line">I/O 线编号。</param>
        /// <param name="linestatus">目标电平状态（High/Low）。</param>
        /// <returns>设置成功返回 true，否则返回 false。</returns>
        bool SetLineStatus(EIOLines line, ELineStatus linestatus);

        /// <summary>
        /// 设置触发延迟时间。
        /// </summary>
        /// <param name="delay">触发延迟（微秒）。</param>
        /// <returns>设置成功返回 true，否则返回 false。</returns>
        bool SetTriggerDelay(float delay);

        /// <summary>
        /// 设置触发滤波时间（用于消除抖动干扰）。
        /// </summary>
        /// <param name="flitertime">滤波时间（微秒）。</param>
        /// <returns>设置成功返回 true，否则返回 false。</returns>
        bool SetTriggerFliter(float flitertime);

        /// <summary>
        /// 设置触发模式和触发来源。
        /// </summary>
        /// <param name="mode">触发模式（Off = 自由运行，On = 触发模式）。</param>
        /// <param name="triggerEnum">触发来源，默认为 Line0。</param>
        /// <returns>设置成功返回 true，否则返回 false。</returns>
        bool SetTriggerMode(ETriggerMode mode, ETriggerSource triggerEnum = ETriggerSource.Line0);

        /// <summary>
        /// 设置硬触发信号极性。
        /// </summary>
        /// <param name="polarity">触发极性（RisingEdge/FallingEdge）。</param>
        /// <returns>设置成功返回 true，否则返回 false。</returns>
        bool SetTriggerPolarity(ETriggerPolarity polarity);

        #endregion
    }
}
