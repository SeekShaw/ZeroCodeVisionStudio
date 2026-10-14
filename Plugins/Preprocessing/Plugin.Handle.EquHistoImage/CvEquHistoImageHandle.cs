using System;
using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.EquHistoImage
{
    /// <summary>
    /// OpenCV 版直方图均衡化。使用 Cv2.EqualizeHist 增强低对比度图像。
    /// </summary>
    [Handle("直方图均衡化(OpenCV)", "CvEquHistoImage", "OpenCV版：使用 Cv2.EqualizeHist 增强低对比度图像细节。", Common.HandleType_Preprocessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvEquHistoImageHandle : HandleBase
    {
        public CvEquHistoImageHandle(IDialogService dialogService) : base(dialogService)
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
                throw new InvalidOperationException("CvEquHistoImage: 未找到可用的输入图像。");

            // 确保是灰度
            Mat gray = mat.Channels() > 1 ? new Mat() : mat;
            if (mat.Channels() > 1) Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);

            var result = new Mat();
            Cv2.EqualizeHist(gray, result);

            RunParameter.MatOutput = result.Clone();
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, result);
            RunParameter.ResultMessage = "OpenCV直方图均衡化完成。";

            if (HalconRuntimeProbe.IsAvailable)
                RunParameter.HOutput = MatHObjectConverter.MatToHObject(result);

            RaiseImageDisplay(result);
        }
    }
}
