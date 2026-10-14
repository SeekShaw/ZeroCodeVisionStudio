using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace ZeroCode.Vision.SDK
{
    /// <summary>
    /// 工业相机抽象基类。
    /// 实现 <see cref="ICamera"/> 中可以统一处理的通用逻辑，减少各品牌 SDK 实现类的重复代码。
    /// <para>
    /// 统一实现的方法：
    /// <list type="bullet">
    ///   <item><description><see cref="GetCamConfig"/>：批量读取配置（内部调用各单项 Get 方法）。</description></item>
    ///   <item><description><see cref="SetCamConfig"/>：批量写入配置（内部调用各单项 Set 方法）。</description></item>
    ///   <item><description><see cref="GetImage"/>：超时轮询回调缓冲图像。</description></item>
    ///   <item><description><see cref="GetImageOnSoftTrigger"/>：一键软触发采图（自动切换模式）。</description></item>
    ///   <item><description><see cref="StartWitchHardTriggerMode"/>：切换到硬触发并启动采集。</description></item>
    ///   <item><description><see cref="StartWitchSoftTriggerMode"/>：切换到软触发并启动采集。</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public abstract class BaseCamera : ICamera
    {
        /// <summary>
        /// 相机图像回调事件。
        /// 具体品牌 SDK 的回调处理中应调用 <see cref="RaiseImageCallbackEvent"/> 触发此事件。
        /// </summary>
        public event Action<string, Bitmap> ImageCallbackEvent;

        /// <summary>回调缓冲的互斥锁：SDK 回调线程写入与 <see cref="GetImage"/> 读取并发访问。</summary>
        private readonly object bufferLock = new();

        /// <summary>
        /// 相机回调中最新收到的图像缓冲区。
        /// 由具体品牌 SDK 的回调处理方法通过 <see cref="StoreCallbackImage"/> 写入，<see cref="GetImage"/> 读取并清空。
        /// </summary>
        protected Bitmap CallBackImg { get; private set; }

        /// <summary>
        /// 供品牌 SDK 回调线程写入最新帧。
        /// 覆盖未被消费的旧帧时会先释放旧 Bitmap，防止连续采集下的 GDI 句柄泄漏。
        /// </summary>
        protected void StoreCallbackImage(Bitmap bitmap)
        {
            Bitmap old;
            lock (bufferLock)
            {
                old = CallBackImg;
                CallBackImg = bitmap;
            }
            old?.Dispose();
        }

        /// <summary>相机当前是否处于采集（Grabbing）状态。</summary>
        public bool IsGrabbing { get; protected set; }

        /// <summary>相机序列号（Serial Number），由 <see cref="InitizeDevice"/> 初始化后设置。</summary>
        public string SN { get; set; } = string.Empty;

        /// <inheritdoc/>
        public abstract bool AutoBalanceWhite();

        /// <inheritdoc/>
        public abstract bool CloseDevice();

        /// <summary>
        /// 触发图像回调事件，通知外部订阅者有新图像产生。
        /// 具体品牌 SDK 的图像回调处理方法应调用此方法。
        /// </summary>
        /// <param name="str1">相机标识字符串（通常为 SN）。</param>
        /// <param name="bitmap">采集到的 Bitmap 图像。</param>
        protected void RaiseImageCallbackEvent(string str1, Bitmap bitmap)
        {
            ImageCallbackEvent?.Invoke(str1, bitmap);
        }

        /// <summary>
        /// 批量读取相机所有配置参数（曝光、增益、触发模式/来源/滤波/延迟）。
        /// 内部依次调用各单项 Get 方法，并封装为 <see cref="CameraConfig"/> 返回。
        /// </summary>
        /// <param name="config">输出的相机配置对象。</param>
        public virtual void GetCamConfig(out CameraConfig config)
        {
            GetExpouseTime(out float expouseTime);
            GetTriggerMode(out ETriggerMode triggerMode, out ETriggerSource hardwareTriggerModel);
            GetTriggerFliter(out float triggerfilter);
            GetGain(out float gain);
            GetTriggerDelay(out float triggerdelay);

            config = new CameraConfig()
            {
                TriggerMode = triggerMode,
                TriggeSource = hardwareTriggerModel,
                TriggerFilter = triggerfilter,
                TriggerDelay = triggerdelay,
                ExpouseTime = expouseTime,
                Gain = gain
            };
        }

        /// <inheritdoc/>
        public abstract List<string> GetCameraEnum();

        /// <inheritdoc/>
        public abstract bool GetExpouseTime(out float value);

        /// <inheritdoc/>
        public abstract bool GetGain(out float gain);

        /// <summary>
        /// 等待并获取回调缓冲中的一帧图像（轮询实现）。
        /// 每隔 1ms 检查 <see cref="CallBackImg"/> 是否有新图像，直到超时。
        /// </summary>
        /// <param name="image">成功时输出 Bitmap 图像（克隆自回调缓冲），失败时为 null。</param>
        /// <param name="timeout">最大等待时间（毫秒），默认 3000ms。</param>
        /// <returns>取到新帧返回 true；超时未取到返回 false。</returns>
        public virtual bool GetImage(out Bitmap image, int timeout = 3000)
        {
            image = null;
            DateTime lastTime = DateTime.Now.AddMilliseconds(timeout);
            while (lastTime > DateTime.Now)
            {
                lock (bufferLock)
                {
                    if (CallBackImg != null)
                    {
                        image = CallBackImg.Clone() as Bitmap;
                        CallBackImg.Dispose();
                        CallBackImg = null;
                        return image != null;
                    }
                }
                Thread.Sleep(1);
            }
            return false;
        }

        /// <summary>
        /// 发送软触发命令并同步等待采集一帧图像。
        /// 执行流程：
        /// <list type="number">
        ///   <item><description>确保相机已处于采集状态（未采集则先启动）。</description></item>
        ///   <item><description>记录当前触发模式，切换为软触发模式。</description></item>
        ///   <item><description>发送软触发命令，等待图像回调。</description></item>
        ///   <item><description>恢复原来的触发模式，停止采集。</description></item>
        /// </list>
        /// </summary>
        /// <param name="image">成功时输出 Bitmap 图像，失败时为 null。</param>
        /// <param name="timeout">等待超时时间（毫秒），默认 3000ms。</param>
        /// <returns>成功获取到非 null 图像时返回 true。</returns>
        public bool GetImageOnSoftTrigger(out Bitmap image, int timeout = 3000)
        {
            if (!IsGrabbing)
            {
                StartGrabbing();
            }
            DateTime startTime = DateTime.Now;
            DateTime lastTime = startTime.AddMilliseconds(timeout);
            while (lastTime > DateTime.Now)
            {
                if (IsGrabbing)
                {
                    break;
                }
                Thread.Sleep(1);
            }
            // 保存当前触发模式，以便采图后恢复
            GetTriggerMode(out ETriggerMode triggerMode, out ETriggerSource triggerSource);
            // 切换为软触发模式
            SetTriggerMode(ETriggerMode.On, ETriggerSource.Software);
            image = null;
            if (!SoftTrigger()) { return false; }
            // 等待图像回调
            GetImage(out image, timeout);
            // 恢复原触发模式
            SetTriggerMode(triggerMode, triggerSource);
            // 停止采集
            StopGrabbing();
            return (image != null);
        }

        /// <inheritdoc/>
        public abstract bool GetLineStatus(EIOLines line, out ELineStatus lineStatus);

        /// <inheritdoc/>
        public abstract bool GetTriggerDelay(out float delay);

        /// <inheritdoc/>
        public abstract bool GetTriggerFliter(out float flitertime);

        /// <inheritdoc/>
        public abstract bool GetTriggerMode(out ETriggerMode mode, out ETriggerSource hardTriggerModel);

        /// <inheritdoc/>
        public abstract bool GetTriggerPolarity(out ETriggerPolarity polarity);

        /// <inheritdoc/>
        public abstract bool InitizeDevice(string CamSN);

        /// <summary>
        /// 批量写入相机配置参数。
        /// config 为 null 时直接返回，不抛出异常。
        /// </summary>
        /// <param name="config">包含所有待写入参数的配置对象。</param>
        public void SetCamConfig(CameraConfig config)
        {
            if (config == null) return;
            SetExpouseTime(config.ExpouseTime);
            SetTriggerMode(config.TriggerMode, config.TriggeSource);
            SetGain(config.Gain);
            SetTriggerDelay(config.TriggerDelay);
        }

        /// <inheritdoc/>
        public abstract bool SetExpouseTime(float value);

        /// <inheritdoc/>
        public abstract bool SetGain(float gain);

        /// <inheritdoc/>
        public abstract bool SetLineMode(EIOLines line, ELineMode mode);

        /// <inheritdoc/>
        public abstract bool SetLineStatus(EIOLines line, ELineStatus linestatus);

        /// <inheritdoc/>
        public abstract bool SetTriggerDelay(float delay);

        /// <inheritdoc/>
        public abstract bool SetTriggerFliter(float flitertime);

        /// <inheritdoc/>
        public abstract bool SetTriggerMode(ETriggerMode mode, ETriggerSource triggerEnum = ETriggerSource.Line0);

        /// <inheritdoc/>
        public abstract bool SetTriggerPolarity(ETriggerPolarity polarity);

        /// <inheritdoc/>
        public abstract bool SoftTrigger();

        /// <summary>
        /// 切换到指定硬触发线并启动采集。
        /// 若传入 Software，自动修正为 Line0，避免误配置。
        /// </summary>
        /// <param name="triggerSource">硬触发信号来源（Line0~Line5）。</param>
        /// <returns>采集启动成功返回 true。</returns>
        public bool StartWitchHardTriggerMode(ETriggerSource triggerSource)
        {
            if (triggerSource == ETriggerSource.Software) triggerSource = ETriggerSource.Line0;
            SetTriggerMode(ETriggerMode.On, triggerSource);
            return StartGrabbing();
        }

        /// <summary>
        /// 切换到自由运行模式（关闭触发）并启动采集。
        /// </summary>
        /// <returns>采集启动成功返回 true。</returns>
        public bool StartWitchSoftTriggerMode()
        {
            SetTriggerMode(ETriggerMode.Off, ETriggerSource.Software);
            return StartGrabbing();
        }

        #region 采集控制抽象方法

        /// <summary>
        /// 释放相机资源（具体品牌 SDK 实现）。
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// 开始图像采集（具体品牌 SDK 实现）。
        /// 启动成功后 <see cref="IsGrabbing"/> 应置为 true。
        /// </summary>
        /// <returns>启动成功返回 true。</returns>
        public abstract bool StartGrabbing();

        /// <summary>
        /// 停止图像采集（具体品牌 SDK 实现）。
        /// 停止成功后 <see cref="IsGrabbing"/> 应置为 false。
        /// </summary>
        /// <returns>停止成功返回 true。</returns>
        public abstract bool StopGrabbing();

        #endregion
    }
}
