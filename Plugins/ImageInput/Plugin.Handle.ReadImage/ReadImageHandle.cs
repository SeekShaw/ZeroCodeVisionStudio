using System;
using System.Collections.Generic;
using System.IO;
using ZeroCodeVisionStudio.Shared;
using HalconDotNet;
using OpenCvSharp;
using Plugin.Handle.ReadImage.View;

namespace Plugin.Handle.ReadImage
{
    [Handle("读取图像", "ReadImage", "从本地文件读取图像，支持单张、批量循环与随机抽取。", Common.HandleType_ImageInput, OperatorBackend = OperatorBackend.Unknown)]
    public class ReadImageHandle : HandleBase, IHandleDataSlots
    {
        private readonly ReadImageParameter readParameter;
        private int _runtimeIndex = 0;

        public ReadImageHandle(IDialogService dialogService) : base(dialogService)
        {
            readParameter = new ReadImageParameter();
            HandleParameter = readParameter;
            SetRunParameter(null);
        }

        protected override string DialogName => nameof(ReadImageView);

        public IReadOnlyList<RunSlot> InputSlots { get; } = Array.Empty<RunSlot>();

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出图像", typeof(HObject)),
            new RunSlot(RunOutputKeys.RawImage, "原始图像", typeof(HObject))
        };

        public override void Handle()
        {
            if (readParameter.ImageFiles == null || readParameter.ImageFiles.Count == 0)
                throw new HandleException(ExceptionType.E_FILE_NOT_FOUND, "未设置图像文件路径，请在参数配置中添加图像文件");

            int count = readParameter.ImageFiles.Count;
            int currentIndex = ResolveIndex(count);
            var filePath = readParameter.ImageFiles[currentIndex];
            if (!File.Exists(filePath))
                throw new HandleException(ExceptionType.E_FILE_NOT_FOUND, $"图像文件不存在: {filePath}");

            var fileName = Path.GetFileName(filePath);

            // 三层降级：Halcon → OpenCV → 纯 WPF (Windows 自带，不依赖任何第三方 native)
            string sourceTag;
            bool loaded = TryHalcon(filePath, fileName, currentIndex, count, out sourceTag)
                       || TryOpenCv(filePath, fileName, currentIndex, count, out sourceTag)
                       || TryWpfBitmap(filePath, fileName, currentIndex, count, out sourceTag);

            if (!loaded)
                throw new HandleException(ExceptionType.E_FILE_NOT_FOUND,
                    $"无法解码图像文件（Halcon/OpenCV/WPF 三种解码路径均失败）：{filePath}");

            RunParameter.Outputs["CurrentIndex"] = currentIndex + 1;
            RunParameter.Outputs["TotalCount"] = count;
            RunParameter.Outputs["FileName"] = fileName;
            RunParameter.Outputs["FilePath"] = filePath;
        }

        private bool TryHalcon(string filePath, string fileName, int idx, int count, out string sourceTag)
        {
            sourceTag = "Halcon";
            if (!HalconRuntimeProbe.IsAvailable) return false;
            try
            {
                HOperatorSet.ReadImage(out var img, filePath);
                HOperatorSet.GetImageSize(img, out var width, out var height);
                RunParameter.RawImage = img;
                RunParameter.HOutput = img;
                RunParameter.SetOutput(RunOutputKeys.Output, img);
                RunParameter.SetOutput(RunOutputKeys.RawImage, img);
                RunParameter.ResultMessage = $"[{idx + 1}/{count}] {fileName} ({width.I}×{height.I})";
                RaiseImageDisplay(img);
                return true;
            }
            catch (Exception halconEx)
            {
                System.Diagnostics.Debug.WriteLine($"Halcon ReadImage 失败（降级 OpenCV）：{halconEx.Message}");
                return false;
            }
        }

        private bool TryOpenCv(string filePath, string fileName, int idx, int count, out string sourceTag)
        {
            sourceTag = "OpenCV";
            try
            {
                using var mat = Cv2.ImRead(filePath);
                if (mat.Empty()) return false;

                // 持久化 Mat（无论 Halcon 可不可用，Mat 必须写进 RunParameter —— OpenCV 算子链路的基础）
                var matClone = mat.Clone();
                RunParameter.MatOutput = matClone;
                var gray = matClone.Channels() > 1 ? new Mat() : matClone;
                if (matClone.Channels() > 1) Cv2.CvtColor(matClone, gray, ColorConversionCodes.BGR2GRAY);
                RunParameter.MatGrayImage = gray;
                RunParameter.SetOutput(RunOutputKeys.Output, matClone);
                RunParameter.SetOutput(RunOutputKeys.RawImage, matClone);

                // Halcon 可用时额外生成 HObject（保持 Halcon 算子链路兼容）
                if (HalconRuntimeProbe.IsAvailable)
                {
                    try
                    {
                        var hobj = MatHObjectConverter.MatToHObject(mat);
                        if (hobj != null)
                        {
                            RunParameter.RawImage = hobj;
                            RunParameter.HOutput = hobj;
                            RunParameter.SetOutput(RunOutputKeys.Output, hobj);
                            RunParameter.SetOutput(RunOutputKeys.RawImage, hobj);
                        }
                    }
                    catch { /* OpenCV→HObject 失败不影响 Mat 路径 */ }
                }

                RunParameter.ResultMessage = $"[{idx + 1}/{count}] {fileName} ({mat.Width}×{mat.Height}) [OpenCV]";
                RaiseImageDisplay(matClone);
                return true;
            }
            catch (Exception opencvEx)
            {
                System.Diagnostics.Debug.WriteLine($"OpenCV ReadImage 失败（降级 WPF）：{opencvEx.Message}");
                return false;
            }
        }

        /// <summary>
        /// 终极兜底：纯 WPF BitmapImage — Windows 自带 WIC 组件，
        /// 不依赖 HALCON、不依赖 OpenCvSharp4 native，100% 可用。
        /// </summary>
        private bool TryWpfBitmap(string filePath, string fileName, int idx, int count, out string sourceTag)
        {
            sourceTag = "WPF";
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var frame = System.Windows.Media.Imaging.BitmapFrame.Create(
                    fs, System.Windows.Media.Imaging.BitmapCreateOptions.None,
                    System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                frame.Freeze();

                RunParameter.ResultMessage = $"[{idx + 1}/{count}] {fileName} ({frame.PixelWidth}×{frame.PixelHeight}) [WPF]";
                RaiseImageDisplay(frame);  // 直接走 BitmapSource 事件 — 主窗口 Image 控件立刻显示
                return true;
            }
            catch (Exception wpfEx)
            {
                System.Diagnostics.Debug.WriteLine($"WPF BitmapImage Read 也失败：{wpfEx.Message}");
                return false;
            }
        }

        private int ResolveIndex(int count)
        {
            switch (readParameter.ReadMode)
            {
                case ImageReadMode.Single:
                    return Math.Clamp(readParameter.CurrentIndex, 0, count - 1);
                case ImageReadMode.Random:
                    return Random.Shared.Next(count);
                case ImageReadMode.Cycle:
                default:
                    if (_runtimeIndex < 0 || _runtimeIndex >= count)
                        _runtimeIndex = Math.Clamp(readParameter.CurrentIndex, 0, count - 1);
                    var idx = _runtimeIndex;
                    _runtimeIndex = (_runtimeIndex + 1) % count;
                    return idx;
            }
        }
    }
}
