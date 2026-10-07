using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using AAAandSphinx.Vision.SDK;
using HalconVisionStudio.Shared;
using Prism.Commands;

namespace Plugin.Handle.CameraHik.ViewModels
{
    public class CameraHikViewModel : HandleDialogBase
    {
        private AAAandSphinx.Vision.SDK.HK.CameraHik _camera;
        private CameraHikParameter _parameter;
        private IHandle _handle;

        #region Camera Selection

        private ObservableCollection<string> _availableCameras = new ObservableCollection<string>();
        public ObservableCollection<string> AvailableCameras
        {
            get => _availableCameras;
            set => SetProperty(ref _availableCameras, value);
        }

        private string _selectedCameraSN;
        public string SelectedCameraSN
        {
            get => _selectedCameraSN;
            set => SetProperty(ref _selectedCameraSN, value);
        }

        private bool _isCameraOpen;
        public bool IsCameraOpen
        {
            get => _isCameraOpen;
            set
            {
                SetProperty(ref _isCameraOpen, value);
                OpenCameraCommand.RaiseCanExecuteChanged();
                CloseCameraCommand.RaiseCanExecuteChanged();
                CaptureCommand.RaiseCanExecuteChanged();
                EnumerateCameraCommand.RaiseCanExecuteChanged();
                ApplyParamsCommand.RaiseCanExecuteChanged();
            }
        }

        private string _cameraStatus = "未连接";
        public string CameraStatus
        {
            get => _cameraStatus;
            set => SetProperty(ref _cameraStatus, value);
        }

        private bool _isEnumerating;
        public bool IsEnumerating
        {
            get => _isEnumerating;
            set
            {
                SetProperty(ref _isEnumerating, value);
                EnumerateCameraCommand.RaiseCanExecuteChanged();
            }
        }

        #endregion

        #region Image Display

        private BitmapSource _cameraImage;
        public BitmapSource CameraImage
        {
            get => _cameraImage;
            set
            {
                SetProperty(ref _cameraImage, value);
                HasImage = value != null;
            }
        }

        private bool _hasImage;
        public bool HasImage
        {
            get => _hasImage;
            set => SetProperty(ref _hasImage, value);
        }

        #endregion

        #region Camera Parameters

        private float _exposureTime = 1000f;
        public float ExposureTime
        {
            get => _exposureTime;
            set => SetProperty(ref _exposureTime, value);
        }

        private float _gain = 1f;
        public float Gain
        {
            get => _gain;
            set => SetProperty(ref _gain, value);
        }

        private float _triggerDelay = 0f;
        public float TriggerDelay
        {
            get => _triggerDelay;
            set => SetProperty(ref _triggerDelay, value);
        }

        private ETriggerMode _triggerMode = ETriggerMode.On;
        public ETriggerMode TriggerMode
        {
            get => _triggerMode;
            set => SetProperty(ref _triggerMode, value);
        }

        private ETriggerSource _triggerSource = ETriggerSource.Software;
        public ETriggerSource TriggerSource
        {
            get => _triggerSource;
            set => SetProperty(ref _triggerSource, value);
        }

        #endregion

        #region Enums for ComboBox Binding

        public static Array TriggerModeValues => Enum.GetValues(typeof(ETriggerMode));
        public static Array TriggerSourceValues => Enum.GetValues(typeof(ETriggerSource));

        #endregion

        #region Commands

        public DelegateCommand EnumerateCameraCommand { get; }
        public DelegateCommand OpenCameraCommand { get; }
        public DelegateCommand CloseCameraCommand { get; }
        public DelegateCommand CaptureCommand { get; }
        public DelegateCommand ApplyParamsCommand { get; }

        #endregion

        public CameraHikViewModel()
        {
            EnumerateCameraCommand = new DelegateCommand(OnEnumerateCameras, CanEnumerateCameras);
            OpenCameraCommand = new DelegateCommand(OnOpenCamera, CanOpenCamera);
            CloseCameraCommand = new DelegateCommand(OnCloseCamera, CanCloseCamera);
            CaptureCommand = new DelegateCommand(OnCapture, CanCapture);
            ApplyParamsCommand = new DelegateCommand(OnApplyParams, CanApplyParams);
        }

        #region Dialog Lifecycle

        public string Title => "海康相机采图";

        public override bool CanCloseDialog() => true;

        public override void OnDialogOpened(IDialogParameters parameters)
        {
            _parameter = parameters.GetValue<CameraHikParameter>(Common.HandleParameter_ParamName);
            _handle = parameters.GetValue<IHandle>(Common.HandleParameter_HandleName);

            if (_parameter != null)
            {
                SelectedCameraSN = _parameter.CameraSN;
                if (_parameter.AvailableCameras != null)
                {
                    AvailableCameras = new ObservableCollection<string>(_parameter.AvailableCameras);
                }
                ExposureTime = _parameter.ExposureTime;
                Gain = _parameter.Gain;
                TriggerDelay = _parameter.TriggerDelay;
                TriggerMode = _parameter.TriggerMode;
                TriggerSource = _parameter.TriggerSource;
            }
        }

        public override void OnDialogClosed()
        {
            CleanupCamera();
            if (_parameter != null)
            {
                _parameter.CameraSN = SelectedCameraSN ?? string.Empty;
                _parameter.AvailableCameras = new ObservableCollection<string>(AvailableCameras);
                _parameter.ExposureTime = ExposureTime;
                _parameter.Gain = Gain;
                _parameter.TriggerDelay = TriggerDelay;
                _parameter.TriggerMode = TriggerMode;
                _parameter.TriggerSource = TriggerSource;
            }
        }

        #endregion

        #region HandleDialogBase Implementation

        protected override void OnExecute()
        {
            if (_handle == null)
            {
                CameraStatus = "执行失败：Handle 未初始化";
                return;
            }

            CleanupCamera();

            try
            {
                _handle.Handle();
                CameraStatus = "采图成功，流程已执行";
            }
            catch (Exception ex)
            {
                CameraStatus = $"执行异常: {ex.Message}";
            }
        }

        protected override void OnReset()
        {
            _parameter?.Reset();

            SelectedCameraSN = string.Empty;
            AvailableCameras.Clear();
            ExposureTime = 1000f;
            Gain = 1f;
            TriggerDelay = 0f;
            TriggerMode = ETriggerMode.On;
            TriggerSource = ETriggerSource.Software;
            CameraStatus = "参数已重置";
        }

        #endregion

        #region Camera Operations

        private bool CanEnumerateCameras()
        {
            return !IsEnumerating && !IsCameraOpen;
        }

        private void OnEnumerateCameras()
        {
            try
            {
                IsEnumerating = true;
                CameraStatus = "正在枚举相机...";
                AvailableCameras.Clear();

                using (var tempCamera = new AAAandSphinx.Vision.SDK.HK.CameraHik())
                {
                    var devices = tempCamera.GetCameraEnum();
                    foreach (var device in devices)
                    {
                        AvailableCameras.Add(device);
                    }
                }

                CameraStatus = AvailableCameras.Count > 0
                    ? $"枚举完成，发现 {AvailableCameras.Count} 台相机"
                    : "未发现海康相机";
            }
            catch (Exception ex)
            {
                CameraStatus = $"枚举失败: {ex.Message}";
            }
            finally
            {
                IsEnumerating = false;
            }
        }

        private bool CanOpenCamera()
        {
            return !IsCameraOpen && !string.IsNullOrEmpty(SelectedCameraSN);
        }

        private void OnOpenCamera()
        {
            if (string.IsNullOrEmpty(SelectedCameraSN))
            {
                CameraStatus = "请先选择相机";
                return;
            }

            try
            {
                CameraStatus = "正在打开相机...";
                CleanupCamera();

                _camera = new AAAandSphinx.Vision.SDK.HK.CameraHik();
                _camera.ImageCallbackEvent += OnCameraImageCallback;
                var result = _camera.InitizeDevice(SelectedCameraSN);

                if (result)
                {
                    _camera.SetTriggerMode(ETriggerMode.Off, ETriggerSource.Software);
                    IsCameraOpen = true;
                    CameraStatus = $"相机已连接: {SelectedCameraSN}（实时预览中）";
                    ReadCurrentCameraParams();
                }
                else
                {
                    CameraStatus = $"连接相机失败: {SelectedCameraSN}";
                    CleanupCamera();
                }
            }
            catch (Exception ex)
            {
                CameraStatus = $"打开相机异常: {ex.Message}";
                CleanupCamera();
            }
        }

        private bool CanCloseCamera()
        {
            return IsCameraOpen;
        }

        private void OnCloseCamera()
        {
            try
            {
                CameraStatus = "正在关闭相机...";
                CleanupCamera();
                IsCameraOpen = false;
                CameraStatus = "相机已关闭";
                CameraImage = null;
            }
            catch (Exception ex)
            {
                CameraStatus = $"关闭相机异常: {ex.Message}";
            }
        }

        private bool CanCapture()
        {
            return IsCameraOpen;
        }

        private void OnCapture()
        {
            if (_camera == null || !IsCameraOpen)
            {
                CameraStatus = "请先打开相机";
                return;
            }

            try
            {
                _camera.GetTriggerMode(out ETriggerMode savedMode, out ETriggerSource savedSource);
                _camera.SetTriggerMode(ETriggerMode.On, ETriggerSource.Software);
                _camera.SoftTrigger();

                Bitmap bitmap = null;
                _camera.GetImage(out bitmap, 3000);

                _camera.SetTriggerMode(ETriggerMode.Off, ETriggerSource.Software);

                if (bitmap != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CameraImage = BitmapToBitmapSource(bitmap);
                    });
                    CameraStatus = "采图预览成功";
                    bitmap.Dispose();
                }
                else
                {
                    CameraStatus = "采图超时或失败";
                }
            }
            catch (Exception ex)
            {
                CameraStatus = $"采图异常: {ex.Message}";
            }
        }

        private bool CanApplyParams()
        {
            return IsCameraOpen;
        }

        private void OnApplyParams()
        {
            if (_camera == null || !IsCameraOpen)
            {
                CameraStatus = "请先打开相机";
                return;
            }

            try
            {
                _camera.SetExpouseTime(ExposureTime);
                _camera.SetGain(Gain);
                _camera.SetTriggerDelay(TriggerDelay);
                _camera.SetTriggerMode(TriggerMode, TriggerSource);
                ReadCurrentCameraParams();
                CameraStatus = "参数已应用";
            }
            catch (Exception ex)
            {
                CameraStatus = $"设置参数失败: {ex.Message}";
            }
        }

        #endregion

        #region Camera Callback (Live Preview)

        private void OnCameraImageCallback(string cameraSN, Bitmap bitmap)
        {
            if (bitmap == null) return;
            if (cameraSN != SelectedCameraSN) return;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (IsCameraOpen)
                {
                    CameraImage = BitmapToBitmapSource(bitmap);
                }
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        #endregion

        #region Helpers

        private void ReadCurrentCameraParams()
        {
            if (_camera == null) return;
            try
            {
                _camera.GetExpouseTime(out float exp);
                _camera.GetGain(out float gain);
                _camera.GetTriggerMode(out ETriggerMode mode, out ETriggerSource source);
                _camera.GetTriggerDelay(out float delay);

                ExposureTime = exp;
                Gain = gain;
                TriggerDelay = delay;
                TriggerMode = mode;
                TriggerSource = source;
            }
            catch
            {
            }
        }

        private void CleanupCamera()
        {
            if (_camera != null)
            {
                try
                {
                    _camera.ImageCallbackEvent -= OnCameraImageCallback;
                    _camera.StopGrabbing();
                    _camera.CloseDevice();
                    _camera.Dispose();
                }
                catch
                {
                }
                _camera = null;
            }
        }

        private BitmapSource BitmapToBitmapSource(Bitmap bitmap)
        {
            if (bitmap == null) return null;
            using (var memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Bmp);
                memory.Position = 0;
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
        }

        #endregion
    }
}
