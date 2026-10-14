using System;
using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.RgbToGray
{
    /// <summary>
    /// OpenCV 版 RGB 转灰度。使用 Cv2.CvtColor + BGR2GRAY。
    /// </summary>
    [Handle("RGB转灰度(OpenCV)", "CvRgbToGray", "OpenCV版：使用 Cv2.CvtColor 将 RGB 图像转换为灰度图像。", Common.HandleType_Preprocessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvRgbToGrayHandle : HandleBase
    {
        public CvRgbToGrayHandle(IDialogService dialogService) : base(dialogService)
        {
            HandleParameter = new EmptyParameter();
            SetRunParameter(null);
        }

        protected override string DialogName => Common.ParameterDialogName;

        public override void Handle()
        {
            var mat = RunParameter.MatGrayImage ?? RunParameter.MatOutput;
            if (mat == null || mat.Empty())
            {
                if (RunParameter.GrayImage != null || RunParameter.HOutput != null)
                {
                    var hImg = RunParameter.GrayImage ?? RunParameter.HOutput;
                    if (hImg != null && HalconRuntimeProbe.IsAvailable)
                    {
                        try { mat = MatHObjectConverter.HObjectToMat(hImg); }
                        catch { }
                    }
                }
            }

            if (mat == null || mat.Empty())
                throw new InvalidOperationException("CvRgbToGray: 未找到可用的输入图像。");

            var result = new Mat();
            if (mat.Channels() > 1)
            {
                Cv2.CvtColor(mat, result, ColorConversionCodes.BGR2GRAY);
            }
            else
            {
                mat.CopyTo(result);
            }

            RunParameter.MatOutput = result.Clone();
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, result);
            RunParameter.ResultMessage = "OpenCV RGB 转灰度完成。";

            if (HalconRuntimeProbe.IsAvailable)
                RunParameter.HOutput = MatHObjectConverter.MatToHObject(result);

            RaiseImageDisplay(result);
        }
    }
}
