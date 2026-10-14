using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.GenEmptyRegion
{
    /// <summary>OpenCV 版生成空区域（空 Mat）。</summary>
    [Handle("生成空区域(OpenCV)", "CvGenEmptyRegion", "OpenCV版：生成空白区域（全0 Mat）。", Common.HandleType_Geometry, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvGenEmptyRegionHandle : HandleBase
    {
        private const int DefaultWidth = 800;
        private const int DefaultHeight = 600;

        public CvGenEmptyRegionHandle(IDialogService dialogService) : base(dialogService) { }
        protected override string DialogName => Common.ParameterDialogName;

        public override void Handle()
        {
            int width = DefaultWidth, height = DefaultHeight;
            var upstream = RunParameter.MatGrayImage ?? RunParameter.MatOutput;
            if (upstream != null && !upstream.Empty()) { width = upstream.Width; height = upstream.Height; }

            var mat = new Mat(height, width, MatType.CV_8UC1, Scalar.All(0));
            RunParameter.MatOutput = mat;
            RunParameter.SetOutput(RunOutputKeys.Output, mat);
            RunParameter.SetOutput(RunOutputKeys.Region, mat);
            if (HalconRuntimeProbe.IsAvailable)
                RunParameter.HOutput = MatHObjectConverter.MatToHObject(mat);
        }
    }
}
