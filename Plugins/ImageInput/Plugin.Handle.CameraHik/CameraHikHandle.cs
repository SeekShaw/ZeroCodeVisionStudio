using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using ZeroCode.Vision.SDK;
using ZeroCodeVisionStudio.Shared;
using HalconDotNet;
using Plugin.Handle.CameraHik.Views;

namespace Plugin.Handle.CameraHik
{
    [Handle("海康相机采图", "CameraHik", "从海康相机采集图像。支持枚举并选择相机、打开/关闭设备、设置曝光/增益/触发模式等参数，并在对话框中实时预览采图图像。", Common.HandleType_ImageInput, OperatorBackend = OperatorBackend.OpenCV)]
    public class CameraHikHandle : HandleBase, IHandleDataSlots
    {
        private ZeroCode.Vision.SDK.HK.CameraHik camera;

        /// <summary>当前已初始化设备对应的目标序列号（null=参数中未指定），用于设备缓存判断。</summary>
        private string initializedTargetSN;

        public CameraHikHandle(IDialogService dialogService) : base(dialogService)
        {
            HandleParameter = new CameraHikParameter();
            SetRunParameter(null);
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = Array.Empty<RunSlot>();

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出图像", typeof(HObject)),
            new RunSlot(RunOutputKeys.RawImage, "原始图像", typeof(HObject))
        };

        /// <summary>
        /// 使用自定义的海康相机采图对话框，而非通用参数编辑器。
        /// </summary>
        protected override string DialogName => nameof(CameraHikView);

        /// <summary>
        /// 批量模式执行：根据已配置的参数连接相机、采图并显示。
        /// 交互式采图请通过对话框操作。
        /// </summary>
        public override void Handle()
        {
            var parameter = HandleParameter as CameraHikParameter;
            if (parameter == null)
            {
                throw new HandleException(ExceptionType.E_PARAMETER_INVALID, "CameraHik 的参数对象为空，无法连接相机。");
            }

            if (camera == null)
            {
                camera = new ZeroCode.Vision.SDK.HK.CameraHik();
            }

            InitializeCamera(parameter);
            ApplyCameraConfig(parameter);

            Bitmap bitmap = null;

            if (parameter.GrabMode == GrabMode.Continuous)
            {
                camera.StartGrabbing();
                camera.SoftTrigger();
                camera.GetImage(out bitmap, 5000);
            }
            else
            {
                camera.GetImageOnSoftTrigger(out bitmap, 5000);
            }

            if (bitmap == null)
            {
                throw new HandleException(
                    ExceptionType.E_NO_VALID_INPUT,
                    "CameraHik 未采集到图像，请检查相机连接、触发模式、曝光参数和采图超时时间。");
            }

            var hObject = BitmapToHObject(bitmap);
            bitmap.Dispose();

            RunParameter.RawImage = hObject;
            RunParameter.HOutput = hObject;
            RunParameter.SetOutput(RunOutputKeys.Output, hObject);
            RunParameter.SetOutput(RunOutputKeys.RawImage, hObject);

            RaiseImageDisplay(hObject);
        }

        /// <summary>
        /// 根据参数中保存的相机序列号初始化设备。
        /// 设备缓存：目标序列号未变化且连接仍有效（SN 非空）时直接复用已打开的设备，
        /// 避免连续生产模式下每帧重复"枚举设备→创建→打开→StartGrabbing"的开销。
        /// CameraSN 为空时使用枚举列表中的第一台相机。
        /// </summary>
        private void InitializeCamera(CameraHikParameter parameter)
        {
            string desiredSN = parameter?.CameraSN;
            if (camera != null && !string.IsNullOrEmpty(camera.SN) &&
                string.Equals(desiredSN, initializedTargetSN, StringComparison.Ordinal))
            {
                // 复用已初始化的设备，仅需停止上一轮采集。
                if (camera.IsGrabbing)
                {
                    camera.StopGrabbing();
                }
                return;
            }

            // 目标变化或连接失效：释放旧设备后完整重建。
            if (camera != null && !string.IsNullOrEmpty(camera.SN))
            {
                camera.Dispose();
            }

            if (camera.IsGrabbing)
            {
                camera.StopGrabbing();
            }

            var devices = camera.GetCameraEnum();
            if (devices.Count == 0)
            {
                throw new HandleException(
                    ExceptionType.E_NO_VALID_INPUT,
                    "CameraHik 未枚举到海康相机，请检查相机连接、驱动和网络配置。");
            }

            string targetSN = parameter?.CameraSN;
            if (string.IsNullOrEmpty(targetSN))
            {
                camera.InitizeDevice(devices[0]);
            }
            else
            {
                var targetDevice = devices.FirstOrDefault(d => d.Contains(targetSN));
                if (targetDevice != null)
                {
                    camera.InitizeDevice(targetDevice);
                }
                else
                {
                    camera.InitizeDevice(devices[0]);
                }
            }

            // InitizeDevice 成功后 camera.SN 非空；记录本次目标用于后续帧的缓存判断。
            initializedTargetSN = desiredSN;
        }

        /// <summary>
        /// 将参数中的曝光、增益、触发模式写入相机。
        /// </summary>
        private void ApplyCameraConfig(CameraHikParameter parameter)
        {
            if (parameter == null) return;

            camera.SetExpouseTime(parameter.ExposureTime);
            camera.SetGain(parameter.Gain);
            camera.SetTriggerDelay(parameter.TriggerDelay);
            camera.SetTriggerMode(parameter.TriggerMode, parameter.TriggerSource);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && camera != null)
            {
                if (camera.IsGrabbing)
                {
                    camera.StopGrabbing();
                }

                camera.Dispose();
                camera = null;
                initializedTargetSN = null;
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// 将 GDI Bitmap 转换为 HALCON HObject 图像，支持 RGB、ARGB、8位灰度等常见像素格式。
        /// 使用 GenImageInterleaved 安全处理交错排列数据，避免通道错位和内存越界。
        /// </summary>
        private HObject BitmapToHObject(Bitmap bitmap)
        {
            var hImage = new HImage();

            using (var bmp = new Bitmap(bitmap))
            {
                if (bmp.PixelFormat == PixelFormat.Format24bppRgb)
                {
                    var bmpData = bmp.LockBits(
                        new Rectangle(0, 0, bmp.Width, bmp.Height),
                        ImageLockMode.ReadOnly,
                        PixelFormat.Format24bppRgb);

                    try
                    {
                        hImage.GenImageInterleaved(
                            bmpData.Scan0,
                            "bgr",
                            bmp.Width,
                            bmp.Height,
                            -1,
                            "byte",
                            bmp.Width,
                            bmp.Height,
                            0,
                            0,
                            -1,
                            0);
                    }
                    finally
                    {
                        bmp.UnlockBits(bmpData);
                    }
                }
                else if (bmp.PixelFormat == PixelFormat.Format32bppArgb || bmp.PixelFormat == PixelFormat.Format32bppRgb)
                {
                    var bmpData = bmp.LockBits(
                        new Rectangle(0, 0, bmp.Width, bmp.Height),
                        ImageLockMode.ReadOnly,
                        PixelFormat.Format32bppArgb);

                    try
                    {
                        hImage.GenImageInterleaved(
                            bmpData.Scan0,
                            "bgra",
                            bmp.Width,
                            bmp.Height,
                            -1,
                            "byte",
                            bmp.Width,
                            bmp.Height,
                            0,
                            0,
                            -1,
                            0);
                    }
                    finally
                    {
                        bmp.UnlockBits(bmpData);
                    }
                }
                else if (bmp.PixelFormat == PixelFormat.Format8bppIndexed)
                {
                    var bmpData = bmp.LockBits(
                        new Rectangle(0, 0, bmp.Width, bmp.Height),
                        ImageLockMode.ReadOnly,
                        PixelFormat.Format8bppIndexed);

                    try
                    {
                        hImage.GenImage1("byte", bmp.Width, bmp.Height, bmpData.Scan0);
                    }
                    finally
                    {
                        bmp.UnlockBits(bmpData);
                    }
                }
                else
                {
                    using (var convertedBmp = bmp.Clone(new Rectangle(0, 0, bmp.Width, bmp.Height), PixelFormat.Format24bppRgb))
                    {
                        var convertedData = convertedBmp.LockBits(
                            new Rectangle(0, 0, convertedBmp.Width, convertedBmp.Height),
                            ImageLockMode.ReadOnly,
                            PixelFormat.Format24bppRgb);

                        try
                        {
                            hImage.GenImageInterleaved(
                                convertedData.Scan0,
                                "bgr",
                                convertedBmp.Width,
                                convertedBmp.Height,
                                -1,
                                "byte",
                                convertedBmp.Width,
                                convertedBmp.Height,
                                0,
                                0,
                                -1,
                                0);
                        }
                        finally
                        {
                            convertedBmp.UnlockBits(convertedData);
                        }
                    }
                }
            }

            return hImage;
        }
    }
}
