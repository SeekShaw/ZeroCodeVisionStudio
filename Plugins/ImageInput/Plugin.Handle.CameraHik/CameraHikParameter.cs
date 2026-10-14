using System.Collections.ObjectModel;
using System.ComponentModel;
using ZeroCode.Vision.SDK;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.CameraHik
{
    public enum GrabMode
    {
        Continuous,
        SingleShot
    }

    public class CameraHikParameter : BindableBase, IHandleParameter
    {
        private string cameraSN = string.Empty;
        private ObservableCollection<string> availableCameras = new ObservableCollection<string>();
        private ETriggerMode triggerMode = ETriggerMode.On;
        private ETriggerSource triggerSource = ETriggerSource.Software;
        private float exposureTime = 1000.0f;
        private float gain = 1.0f;
        private float triggerDelay = 0.0f;
        private GrabMode grabMode = GrabMode.Continuous;

        [DisplayName("可用相机")]
        public ObservableCollection<string> AvailableCameras 
        { 
            get => availableCameras; 
            set => SetProperty(ref availableCameras, value); 
        }

        [DisplayName("选中相机")]
        public string CameraSN { get => cameraSN; set => SetProperty(ref cameraSN, value); }

        [DisplayName("采集模式")]
        public GrabMode GrabMode { get => grabMode; set => SetProperty(ref grabMode, value); }

        [DisplayName("触发模式")]
        public ETriggerMode TriggerMode { get => triggerMode; set => SetProperty(ref triggerMode, value); }

        [DisplayName("触发源")]
        public ETriggerSource TriggerSource { get => triggerSource; set => SetProperty(ref triggerSource, value); }

        [DisplayName("曝光时间(us)")]
        public float ExposureTime { get => exposureTime; set => SetProperty(ref exposureTime, value); }

        [DisplayName("增益")]
        public float Gain { get => gain; set => SetProperty(ref gain, value); }

        [DisplayName("触发延迟(us)")]
        public float TriggerDelay { get => triggerDelay; set => SetProperty(ref triggerDelay, value); }

        public IHandleParameter Clone() => new CameraHikParameter 
        { 
            CameraSN = CameraSN,
            AvailableCameras = new ObservableCollection<string>(AvailableCameras),
            GrabMode = GrabMode,
            TriggerMode = TriggerMode,
            TriggerSource = TriggerSource,
            ExposureTime = ExposureTime,
            Gain = Gain,
            TriggerDelay = TriggerDelay
        };

        public void Reset()
        {
            CameraSN = string.Empty;
            AvailableCameras.Clear();
            GrabMode = GrabMode.Continuous;
            TriggerMode = ETriggerMode.On;
            TriggerSource = ETriggerSource.Software;
            ExposureTime = 1000.0f;
            Gain = 1.0f;
            TriggerDelay = 0.0f;
        }

        public void RefreshCameraList()
        {
            AvailableCameras.Clear();
            try
            {
                using (var camera = new ZeroCode.Vision.SDK.HK.CameraHik())
                {
                    var devices = camera.GetCameraEnum();
                    foreach (var device in devices)
                    {
                        AvailableCameras.Add(device);
                    }
                }
            }
            catch
            {
            }
        }
    }
}