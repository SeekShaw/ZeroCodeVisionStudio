using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.GenEllipse
{
    /// <summary>OpenCV 版生成椭圆。</summary>
    [Handle("生成椭圆(OpenCV)", "CvGenEllipse", "OpenCV版：根据圆心/角度/长短轴绘制椭圆区域。", Common.HandleType_Geometry, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvGenEllipseHandle : ParameterHandleBase<GenEllipseParameter>
    {
        private const int DefaultWidth = 800;
        private const int DefaultHeight = 600;

        public CvGenEllipseHandle(IDialogService dialogService) : base(dialogService) { }
        protected override string DialogName => Common.ShapeDrawDialogName;

        public override void EditParameters()
        {
            DialogParameters param = new DialogParameters
            {
                { Common.HandleParameter_ParamName, HandleParameter },
                { Common.HandleParameter_HandleName, this },
                { Common.HandleParameter_RunModel, ContextRunModel },
                { "ShapeType", "ellipse" }
            };
            dialogService.ShowDialog(DialogName, param);
        }

        public override void Handle()
        {
            int width = DefaultWidth, height = DefaultHeight;
            var upstream = RunParameter.MatGrayImage ?? RunParameter.MatOutput;
            if (upstream != null && !upstream.Empty()) { width = upstream.Width; height = upstream.Height; }

            var mat = new Mat(height, width, MatType.CV_8UC1, Scalar.All(0));
            // OpenCV axes 是 (half_width, half_height)，Halcon Radius1=长轴, Radius2=短轴
            var axes = new Size((int)Parameter.Radius2, (int)Parameter.Radius1);
            Cv2.Ellipse(mat,
                center: new Point((int)Parameter.Column, (int)Parameter.Row),
                axes: axes,
                angle: (double)Parameter.Phi * 180.0 / Math.PI, // Halcon Phi 弧度 → OpenCV 角度
                startAngle: 0, endAngle: 360,
                color: Scalar.White,
                thickness: -1,
                lineType: LineTypes.AntiAlias);

            RunParameter.MatOutput = mat;
            RunParameter.SetOutput(RunOutputKeys.Output, mat);
            RunParameter.SetOutput(RunOutputKeys.Region, mat);
            if (HalconRuntimeProbe.IsAvailable)
                RunParameter.HOutput = MatHObjectConverter.MatToHObject(mat);
        }
    }
}
