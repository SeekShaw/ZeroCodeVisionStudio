using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.GenRectangle2
{
    /// <summary>OpenCV 版生成旋转矩形（Center + 角度 + 半长轴/半短轴）。</summary>
    [Handle("生成矩形2(OpenCV)", "CvGenRectangle2", "OpenCV版：生成旋转矩形区域。", Common.HandleType_Geometry, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvGenRectangle2Handle : ParameterHandleBase<GenRectangle2Parameter>
    {
        private const int DefaultWidth = 800;
        private const int DefaultHeight = 600;

        public CvGenRectangle2Handle(IDialogService dialogService) : base(dialogService) { }
        protected override string DialogName => Common.ShapeDrawDialogName;

        public override void EditParameters()
        {
            DialogParameters param = new DialogParameters
            {
                { Common.HandleParameter_ParamName, HandleParameter },
                { Common.HandleParameter_HandleName, this },
                { Common.HandleParameter_RunModel, ContextRunModel },
                { "ShapeType", "rectangle2" }
            };
            dialogService.ShowDialog(DialogName, param);
        }

        public override void Handle()
        {
            int width = DefaultWidth, height = DefaultHeight;
            var upstream = RunParameter.MatGrayImage ?? RunParameter.MatOutput;
            if (upstream != null && !upstream.Empty()) { width = upstream.Width; height = upstream.Height; }

            var mat = new Mat(height, width, MatType.CV_8UC1, Scalar.All(0));

            // OpenCV RotatedRect: center, size (width, height), angle(degrees)
            // Halcon: Row, Column, Phi(radians), Length1(半长轴), Length2(半短轴)
            var center = new Point2f((float)Parameter.Column, (float)Parameter.Row);
            var size = new Size2f((float)(Parameter.Length2 * 2), (float)(Parameter.Length1 * 2));
            var angle = (float)(Parameter.Phi * 180.0 / Math.PI);

            var rect = new RotatedRect(center, size, angle);
            var vertices = Cv2.BoxPoints(rect); // 4 个顶点
            var pts = vertices.Select(v => new Point((int)Math.Round(v.X), (int)Math.Round(v.Y))).ToArray();
            Cv2.FillConvexPoly(mat, pts, Scalar.White, LineTypes.AntiAlias);

            RunParameter.MatOutput = mat;
            RunParameter.SetOutput(RunOutputKeys.Output, mat);
            RunParameter.SetOutput(RunOutputKeys.Region, mat);
            if (HalconRuntimeProbe.IsAvailable)
                RunParameter.HOutput = MatHObjectConverter.MatToHObject(mat);
        }
    }
}
