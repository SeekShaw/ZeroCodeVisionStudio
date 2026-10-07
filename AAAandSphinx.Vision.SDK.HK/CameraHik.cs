using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using MvCameraControl;
using static System.Net.Mime.MediaTypeNames;

namespace AAAandSphinx.Vision.SDK.HK
{
    public class CameraHik : BaseCamera
    {
        public CameraHik()
        {
        }
        //private readonly DeviceTLayerType enumTLayerType = DeviceTLayerType.MvGigEDevice | DeviceTLayerType.MvUsbDevice | DeviceTLayerType.MvGenTLGigEDevice | DeviceTLayerType.MvGenTLCXPDevice | DeviceTLayerType.MvGenTLCameraLinkDevice | DeviceTLayerType.MvVirGigEDevice | DeviceTLayerType.MvCameraLinkDevice | DeviceTLayerType.MvVirUsbDevice | DeviceTLayerType.MvGenTLXoFDevice;
        private readonly DeviceTLayerType enumTLayerType = DeviceTLayerType.MvGigEDevice | DeviceTLayerType.MvUsbDevice | DeviceTLayerType.MvGenTLGigEDevice | DeviceTLayerType.MvGenTLCXPDevice | DeviceTLayerType.MvGenTLCameraLinkDevice | DeviceTLayerType.MvGenTLXoFDevice;

        private static bool IsColorPixelFormat(MvGvspPixelType enType)
        {
            switch (enType)
            {
                case MvGvspPixelType.PixelType_Gvsp_BGR8_Packed:
                case MvGvspPixelType.PixelType_Gvsp_YUV422_Packed:
                case MvGvspPixelType.PixelType_Gvsp_YUV422_YUYV_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerGR8:
                case MvGvspPixelType.PixelType_Gvsp_BayerRG8:
                case MvGvspPixelType.PixelType_Gvsp_BayerGB8:
                case MvGvspPixelType.PixelType_Gvsp_BayerBG8:
                case MvGvspPixelType.PixelType_Gvsp_BayerGB10:
                case MvGvspPixelType.PixelType_Gvsp_BayerGB10_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerBG10:
                case MvGvspPixelType.PixelType_Gvsp_BayerBG10_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerRG10:
                case MvGvspPixelType.PixelType_Gvsp_BayerRG10_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerGR10:
                case MvGvspPixelType.PixelType_Gvsp_BayerGR10_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerGB12:
                case MvGvspPixelType.PixelType_Gvsp_BayerGB12_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerBG12:
                case MvGvspPixelType.PixelType_Gvsp_BayerBG12_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerRG12:
                case MvGvspPixelType.PixelType_Gvsp_BayerRG12_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerGR12:
                case MvGvspPixelType.PixelType_Gvsp_BayerGR12_Packed:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsMonoPixelFormat(MvGvspPixelType enType)
        {
            switch (enType)
            {
                case MvGvspPixelType.PixelType_Gvsp_Mono10:
                case MvGvspPixelType.PixelType_Gvsp_Mono10_Packed:
                case MvGvspPixelType.PixelType_Gvsp_Mono12:
                case MvGvspPixelType.PixelType_Gvsp_Mono12_Packed:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 回调函数(不要在内部增加耗时操作)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CallBackEventHandler(object sender, FrameGrabbedEventArgs e)
        {
            int nRet;
            Debug.WriteLine("Get Image Buffer: Width[{0}] , Height[{1}] , FrameNum[{2}]"
            , e.FrameOut.Image.Width, e.FrameOut.Image.Height, e.FrameOut.FrameNum);
            IImage inputImage = e.FrameOut.Image;
            IImage outImage = inputImage;
            MvGvspPixelType dstPixelType = MvGvspPixelType.PixelType_Gvsp_Undefined;

            if (IsColorPixelFormat(e.FrameOut.Image.PixelType))
            {
                dstPixelType = MvGvspPixelType.PixelType_Gvsp_RGB8_Packed;
            }
            else if (IsMonoPixelFormat(e.FrameOut.Image.PixelType))
            {
                dstPixelType = MvGvspPixelType.PixelType_Gvsp_Mono8;
            }
            else
            {
                // Debug.WriteLine("Don't need to convert!");
            }

            if (dstPixelType != MvGvspPixelType.PixelType_Gvsp_Undefined)
            {
                // ch:像素格式转换 | en:Pixel type convert
                nRet = device.PixelTypeConverter.ConvertPixelType(inputImage, out outImage, dstPixelType);
                if (nRet != MvError.MV_OK)
                {
                    Debug.WriteLine("Image Convert failed:{0:x8}", nRet);
                    device.StreamGrabber.FreeImageBuffer(e.FrameOut);
                    return;
                }
                Debug.WriteLine("Image Convert success!");
            }

            // 先从帧缓冲拷贝出 Bitmap，再归还 SDK 缓冲，避免释放后使用。
            var bitmap1 = outImage.ToBitmap();
            // ch: 释放图像缓存 | en: Release image buffer
            device.StreamGrabber.FreeImageBuffer(e.FrameOut);
            if (outImage != inputImage)
            {
                (outImage as IDisposable)?.Dispose();
            }

            if (bitmap1 != null)
            {
                // StoreCallbackImage 内部会释放被覆盖的旧帧，防止 GDI 句柄泄漏。
                StoreCallbackImage(bitmap1);
                RaiseImageCallbackEvent(SN, CallBackImg);
            }
        }
        private List<IDeviceInfo> GetListInfoEnum()
        {
            deviceInfoList.Clear();
            // ch:创建设备列表 | en:Create Device List
            int nRet = DeviceEnumerator.EnumDevices(enumTLayerType, out deviceInfoList);
            if (nRet != MvError.MV_OK)
            {
                Debug.WriteLine("Enumerate devices fail!", nRet);
                return new List<IDeviceInfo>();
            }

            // ch:在窗体列表中显示设备名 | en:Display device name in the form list
            for (int i = 0; i < deviceInfoList.Count; i++)
            {
                IDeviceInfo deviceInfo = deviceInfoList[i];
                if (deviceInfo.UserDefinedName != "")
                {
                    deviceSNList.Add(deviceInfo.TLayerType.ToString() + ": " + deviceInfo.UserDefinedName + " (" + deviceInfo.SerialNumber + ")");
                }
                else
                {
                    deviceSNList.Add(deviceInfo.TLayerType.ToString() + ": " + deviceInfo.ManufacturerName + " " + deviceInfo.ModelName + " (" + deviceInfo.SerialNumber + ")");
                }
            }

            // ch:选择第一项 | en:Select the first item
            if (deviceInfoList.Count != 0)
            {
                deviceSNList[0] = deviceSNList[0];
            }
            return deviceInfoList;
        }

        public override bool AutoBalanceWhite()
        {
            int nRet = device.Parameters.SetEnumValueByString("BalanceWhiteAuto", "Once");
            return (MvError.MV_OK == nRet);
        }

        public override bool CloseDevice()
        {
            try
            {
                if (m_BufForDriver != IntPtr.Zero)
                {
                    Marshal.Release(m_BufForDriver);
                }

                // ch:关闭设备 | en:Close device
                var nRet = device.Close();
                if (MvError.MV_OK == nRet)
                {
                    // ch:销毁设备 | en:Destroy device
                    device.Dispose();
                    return true;
                }

                nRet = device.Close();
                if (MvError.MV_OK != nRet)
                {
                    Debug.WriteLine("Close device failed:{0:x8}", nRet);
                    return false;
                }

                // ch:销毁设备 | en:Destroy device
                device.Dispose();
                return true;
            }
            catch
            {
                device.Dispose();
                device = null;
                return false;
            }
        }

        public override void Dispose()
        {
            if (device != null)
            {
                SN = string.Empty;
                device.Close();
                device.Dispose();
            }
        }

        public override List<string> GetCameraEnum()
        {
            List<string> deviceList = new List<string>();
            foreach (var item in GetListInfoEnum())
            {
                IDeviceInfo deviceInfo = item;
                if (deviceInfo.UserDefinedName != "")
                {
                    //deviceList.Add(deviceInfo.TLayerType.ToString() + ": " + deviceInfo.UserDefinedName + " (" + deviceInfo.SerialNumber + ")");
                    deviceList.Add(deviceInfo.SerialNumber);
                }
                else
                {
                    //deviceList.Add(deviceInfo.TLayerType.ToString() + ": " + deviceInfo.ManufacturerName + " " + deviceInfo.ModelName + " (" + deviceInfo.SerialNumber + ")");
                    deviceList.Add(deviceInfo.SerialNumber);
                }
            }
            return deviceList;
        }

        public override bool GetExpouseTime(out float value)
        {
            IFloatValue floatValue;
            int nRet = device.Parameters.GetFloatValue("ExposureTime", out floatValue);
            value = (float)floatValue.CurValue;
            return (MvError.MV_OK == nRet);
        }

        public override bool GetGain(out float gain)
        {
            gain = 0;
            IFloatValue floatValue;
            int nRet = device.Parameters.GetFloatValue("Gain", out floatValue);
            gain = (float)floatValue.CurValue;
            return (MvError.MV_OK == nRet);
        }

        public override bool GetLineStatus(EIOLines line, out ELineStatus linestatus)
        {
            bool resultsignal = false;
            int nRet = device.Parameters.GetBoolValue(line.ToString(), out resultsignal);
            linestatus = resultsignal ? ELineStatus.Hight : ELineStatus.Low;
            return (MvError.MV_OK == nRet);
        }

        public override bool GetTriggerDelay(out float delay)
        {
            delay = 0;
            IFloatValue floatValue;
            int nRet = device.Parameters.GetFloatValue("TriggerDelay", out floatValue);
            delay = (float)floatValue.CurValue;
            return (MvError.MV_OK == nRet);
        }

        public override bool GetTriggerFliter(out float flitertime)
        {
            flitertime = 1000;
            IFloatValue floatValue;
            int nRet = device.Parameters.GetFloatValue("ExposureTime", out floatValue);
            flitertime = (float)floatValue.CurValue;
            return (MvError.MV_OK == nRet);
        }

        public override bool GetTriggerMode(out ETriggerMode mode, out ETriggerSource source)
        {
            int nRet;
            mode = ETriggerMode.On;
            source = ETriggerSource.Software;

            IEnumValue enumValue;
            nRet = device.Parameters.GetEnumValue("TriggerMode", out enumValue);
            bool flag1 = (MvError.MV_OK == nRet);
            if (flag1)
            {
                switch (enumValue.CurEnumEntry.Symbolic)
                {
                    case "On":
                        mode = ETriggerMode.On;
                        break;
                    case "Off":
                        mode = ETriggerMode.Off;
                        break;
                    default:
                        mode = ETriggerMode.On;
                        break;
                }
            }

            nRet = device.Parameters.GetEnumValue("TriggerSource", out enumValue);
            bool flag2 = (MvError.MV_OK == nRet);
            if (flag2)
            {
                switch (enumValue.CurEnumEntry.Symbolic)
                {
                    case "TriggerSoftware":
                        source = ETriggerSource.Software;
                        break;
                    case "Line0":
                        source = ETriggerSource.Line0;
                        break;
                    case "Line1":
                        source = ETriggerSource.Line1;
                        break;
                    case "Line2":
                        source = ETriggerSource.Line2;
                        break;
                    default:
                        source = ETriggerSource.Line0;
                        break;
                }
            }
            return flag1 && flag2;
        }

        public override bool GetTriggerPolarity(out ETriggerPolarity polarity)
        {
            polarity = ETriggerPolarity.RisingEdge;
            IEnumValue enumValue;
            int nRet = _ifInstance.Parameters.GetEnumValue("TimerTriggerActivation", out enumValue);
            if (nRet != MvError.MV_OK)
            {
                return false;
            }
            string activate = enumValue.CurEnumEntry.Symbolic;
            //1下降沿 0 上升沿
            if (activate == "RisingEdge")
            { //上升沿
                polarity = ETriggerPolarity.RisingEdge;
            }
            else if (activate == "FallingEdge")
            { //下降沿
                polarity = ETriggerPolarity.FallingEdge;
            }
            return (MvError.MV_OK == nRet);
        }
        public override bool InitizeDevice(string CamSN)
        {
            try
            {
                int nRet;
                if (string.IsNullOrEmpty(CamSN)) return false;
                // 获取海康相机的序列化列表
                var infolist = GetListInfoEnum();
                if (infolist.Count < 1) return false;
                // 获取列表中第一台设备
                IDeviceInfo deviceInfo = infolist[0];
                bool selectSNflag = false;
                foreach (var item in infolist)
                {
                    if (item.SerialNumber.Equals(CamSN))
                    {
                        deviceInfo = item;
                        selectSNflag = true;
                        break;
                    }
                }
                if (!selectSNflag) return false;

                try
                {
                    // 通过查询到设备信息创建设备对象
                    device = DeviceFactory.CreateDevice(deviceInfo);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Create Device fail!" + ex.Message);
                    return false;
                }

                // 打开创建的设备
                nRet = device.Open();
                if (nRet != MvError.MV_OK)
                {
                    Debug.WriteLine("Open Device fail!", nRet);
                    return false;
                }

                // ch:注册回调函数 | en:Register image callback
                // 注册回调函数操作应放在打开采集流操作之前
                device.StreamGrabber.FrameGrabedEvent += CallBackEventHandler;

                #region 探测网络最佳包大小(只对GigE相机有效)
                if (device is IGigEDevice)
                {
                    int packetSize;
                    nRet = (device as IGigEDevice).GetOptimalPacketSize(out packetSize);
                    if (packetSize > 0)
                    {
                        nRet = device.Parameters.SetIntValue("GevSCPSPacketSize", packetSize);
                        if (nRet != MvError.MV_OK)
                        {
                            Debug.WriteLine("Warning: Set Packet Size failed {0:x8}", nRet);
                        }
                        else
                        {
                            Debug.WriteLine("Set PacketSize to {0}", packetSize);
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Warning: Get Packet Size failed {0:x8}", nRet);
                    }
                }
                #endregion


                // ch:设置触发模式为On || en:set trigger mode as On
                // 设置为Off会一直触发回调
                SetTriggerMode(ETriggerMode.On, ETriggerSource.Software);
                // 默认设置连续触发模式
                device.Parameters.SetEnumValueByString("AcquisitionMode", "Continuous");

                if (nRet != MvError.MV_OK)
                {
                    Debug.WriteLine("Set TriggerMode failed:{0:x8}", nRet);
                    return false;
                }

                if (!StartGrabbing())
                {
                    Debug.WriteLine("开始采集失败");
                    return false;
                }

                //ch: 设置合适的缓存节点数量 | en: Setting the appropriate number of image nodes
                device.StreamGrabber.SetImageNodeNum(5);

                SN = CamSN;
                return true;
            }
            catch
            {
                return false;
            }
        }
        public override bool SetExpouseTime(float value)
        {
            int nRet = device.Parameters.SetFloatValue("ExposureTime", value);
            return (MvError.MV_OK == nRet);
        }

        public override bool SetGain(float gain)
        {
            int nRet = device.Parameters.SetFloatValue("Gain", (float)gain);
            return (MvError.MV_OK == nRet);
        }

        public override bool SetLineMode(EIOLines line, ELineMode mode)
        {
            int nRet = device.Parameters.SetEnumValueByString(line.ToString(), mode.ToString());
            return (MvError.MV_OK == nRet);
        }

        public override bool SetLineStatus(EIOLines line, ELineStatus linestatus)
        {
            int nRet = device.Parameters.SetBoolValue(line.ToString(), linestatus.Equals(ELineStatus.Hight));
            return (MvError.MV_OK == nRet);
        }

        public override bool SetTriggerDelay(float delay)
        {
            int nRet = device.Parameters.SetFloatValue("TriggerDelay", delay);
            return (MvError.MV_OK == nRet);
        }

        public override bool SetTriggerFliter(float flitertime)
        {
            int nRet = _ifInstance.Parameters.SetIntValue("LineDebouncerTimeNs", (uint)flitertime);
            nRet = _ifInstance.Parameters.SetIntValue("LineDebouncerTime", (uint)flitertime);
            return (MvError.MV_OK == nRet);
        }

        public override bool SetTriggerMode(ETriggerMode mode, ETriggerSource triggerEnum = ETriggerSource.Line0)
        {
            int nRet;
            switch (mode)
            {
                case ETriggerMode.Off:
                    nRet = device.Parameters.SetEnumValueByString("TriggerMode", "Off");
                    break;
                case ETriggerMode.On:
                    nRet = device.Parameters.SetEnumValueByString("TriggerMode", "On");
                    break;
                default:
                    nRet = device.Parameters.SetEnumValueByString("TriggerMode", "On");
                    break;
            }
            bool flag1 = (MvError.MV_OK == nRet);
            switch (triggerEnum)
            {
                case ETriggerSource.Software:
                    nRet = device.Parameters.SetEnumValueByString("TriggerSource", "Software");
                    break;
                case ETriggerSource.Line0:
                    nRet = device.Parameters.SetEnumValueByString("TriggerSource", "Line0");
                    break;
                case ETriggerSource.Line1:
                    nRet = device.Parameters.SetEnumValueByString("TriggerSource", "Line1");
                    break;
                case ETriggerSource.Line2:
                    nRet = device.Parameters.SetEnumValueByString("TriggerSource", "Line2");
                    break;
                case ETriggerSource.Line3:
                    nRet = device.Parameters.SetEnumValueByString("TriggerSource", "Line3");
                    break;
                default:
                    nRet = device.Parameters.SetEnumValueByString("TriggerSource", "Line4");
                    break;
            }
            bool flag2 = (MvError.MV_OK == nRet);
            return flag1 && flag2;
        }

        public override bool SetTriggerPolarity(ETriggerPolarity polarity)
        {
            IEnumValue enumValue;
            int nRet = _ifInstance.Parameters.GetEnumValue("TimerTriggerActivation", out enumValue);
            if (nRet != MvError.MV_OK)
            {
                return false;
            }
            for (int i = 0; i < enumValue.SupportedNum; ++i)
            {

                if (polarity.ToString().Equals(enumValue.SupportEnumEntries[i].Symbolic, StringComparison.OrdinalIgnoreCase))
                {
                    nRet = _ifInstance.Parameters.SetEnumValue("TimerTriggerActivation", enumValue.SupportEnumEntries[i].Value);
                    if (nRet != MvError.MV_OK)
                    {
                        return false;
                    }

                    break;
                }
            }
            return (MvError.MV_OK == nRet);
        }

        public override bool SoftTrigger()
        {

            int nRet;
            // ch:触发命令 | en:Trigger command
            nRet = device.Parameters.SetCommandValue("TriggerSoftware");

            return (MvError.MV_OK == nRet);
        }

        public override bool StartGrabbing()
        {
            int nRet = device.StreamGrabber.StartGrabbing();
            if (MvError.MV_OK != nRet) Debug.WriteLine("Grab start failed");
            IsGrabbing = true;
            return (MvError.MV_OK == nRet);
        }

        public override bool StopGrabbing()
        {
            int nRet = device.StreamGrabber.StopGrabbing();
            if (MvError.MV_OK != nRet) Debug.WriteLine("Grab stop failed");
            IsGrabbing = false;
            return (MvError.MV_OK == nRet);
        }
        #region param
        private byte[] _buffer;

        UInt32 _bufferSize = 3072 * 2048 * 3;

        private byte[] _bufForSaveImage;

        private uint _buffSizeForSaveImage = 3072 * 2048 * (16 * 3 + 4) + 2048;

        IInterface _ifInstance;

        IDevice device = null;

        List<IDeviceInfo> deviceInfoList = new List<IDeviceInfo>();

        List<string> deviceSNList = new List<string>();

        IntPtr m_BufForDriver = IntPtr.Zero;

        private Object m_BufForSaveImageLock = new Object();

        public IntPtr m_pSaveImageBuf = IntPtr.Zero;

        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory", SetLastError = false)]
        public static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);
        #endregion
    }
}
