using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.GenRectangle1
{
    /// <summary>OpenCV 版生成轴对齐矩形（Row1/Col1/Row2/Col2 对角点）。</summary>
    [Handle("生成矩形1(OpenCV)", "CvGenRectangle1", "OpenCV版：生成轴向对齐矩形区域。", Common.HandleType_Geometry, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvGenRectangle1Handle : ParameterHandleBase<GenRectangle1Parameter>
    {
        private const int DefaultWidth = 800;
        private const int DefaultHeight = 600;

        public CvGenRectangle1Handle(IDialogService dialogService) : base(dialogService) { }
        protected override string DialogName => Common.ShapeDrawDialogName;

        public override void EditParameters()
        {
            DialogParameters param = new DialogParameters
            {
                { Common.HandleParameter_ParamName, HandleParameter },
                { Common.HandleParameter_HandleName, this },
                { Common.HandleParameter_RunModel, ContextRunModel },
                { "ShapeType", "rectangle1" }
            };
            dialogService.ShowDialog(DialogName, param);
        }

        public override void Handle()
        {
            int width = DefaultWidth, height = DefaultHeight;
            var upstream = RunParameter.MatGrayImage ?? RunParameter.MatOutput;
            if (upstream != null && !upstream.Empty()) { width = upstream.Width; height = upstream.Height; }

            var mat = new Mat(height, width, MatType.CV_8UC1, Scalar.All(0));
            // Halcon (Row, Column) → OpenCV (Point(col, row))
            var pt1 = new Point((int)Parameter.Column1, (int)Parameter.Row1);
            var pt2 = new Point((int)Parameter.Column2, (int)Parameter.Row2);
            Cv2.Rectangle(mat, pt1, pt2, Scalar.White, -1, LineTypes.AntiAlias);

            RunParameter.MatOutput = mat;
            RunParameter.SetOutput(RunOutputKeys.Output, mat);
            RunParameter.SetOutput(RunOutputKeys.Region, mat);
            if (HalconRuntimeProbe.IsAvailable)
                RunParameter.HOutput = MatHObjectConverter.MatToHObject(mat);
        }
    }
}
