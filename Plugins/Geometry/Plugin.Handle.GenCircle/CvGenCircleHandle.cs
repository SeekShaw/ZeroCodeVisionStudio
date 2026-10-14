using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.GenCircle
{
    /// <summary>
    /// OpenCV 版生成圆。在空白 Mat 上绘制白色填充圆，输出 MatOutput + 可选同步桥接到 HOutput。
    /// </summary>
    [Handle("生成圆(OpenCV)", "CvGenCircle", "OpenCV版：根据圆心坐标和半径在空白图上绘制圆形区域。", Common.HandleType_Geometry, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvGenCircleHandle : ParameterHandleBase<GenCircleParameter>
    {
        // 默认画布尺寸（如果 RunParameter 没有上游图像）
        private const int DefaultWidth = 800;
        private const int DefaultHeight = 600;

        public CvGenCircleHandle(IDialogService dialogService) : base(dialogService) { }

        protected override string DialogName => Common.ShapeDrawDialogName;

        public override void EditParameters()
        {
            DialogParameters param = new DialogParameters
            {
                { Common.HandleParameter_ParamName, HandleParameter },
                { Common.HandleParameter_HandleName, this },
                { Common.HandleParameter_RunModel, ContextRunModel },
                { "ShapeType", "circle" }
            };
            dialogService.ShowDialog(DialogName, param);
        }

        public override void Handle()
        {
            int width = DefaultWidth;
            int height = DefaultHeight;

            // 优先取上游图像尺寸
            var upstream = RunParameter.MatGrayImage ?? RunParameter.MatOutput;
            if (upstream != null && !upstream.Empty())
            {
                width = upstream.Width;
                height = upstream.Height;
            }
            else if (RunParameter.HOutput != null || RunParameter.GrayImage != null)
            {
                // Halcon 图像 — 通过桥接拿尺寸
                var hImg = RunParameter.GrayImage ?? RunParameter.HOutput;
                if (hImg != null && HalconRuntimeProbe.IsAvailable)
                {
                    try
                    {
                        HalconDotNet.HOperatorSet.GetImageSize(hImg, out var wT, out var hT);
                        width = wT.I; height = hT.I;
                    }
                    catch { }
                }
            }

            // OpenCV: 单通道空白 + 绘制白色填充圆
            var mat = new Mat(height, width, MatType.CV_8UC1, Scalar.All(0));
            Cv2.Circle(mat,
                center: new Point((int)Parameter.Column, (int)Parameter.Row),
                radius: (int)Parameter.Radius,
                color: Scalar.White,
                thickness: -1,   // 填充
                lineType: LineTypes.AntiAlias);

            RunParameter.MatOutput = mat;
            RunParameter.SetOutput(RunOutputKeys.Output, mat);
            RunParameter.SetOutput(RunOutputKeys.Region, mat);

            // Halcon 可用时同步桥接到 HOutput
            if (HalconRuntimeProbe.IsAvailable)
            {
                RunParameter.HOutput = MatHObjectConverter.MatToHObject(mat);
            }

            RaiseImageDisplay(mat);
        }
    }
}
