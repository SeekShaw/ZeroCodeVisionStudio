using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.FindScaledShapeModel
{
    [Handle("查找缩放形状模板", "FindScaledShapeModel", "在当前图像中查找支持等比例缩放的形状模板实例。", Common.HandleType_ShapeMatching, OperatorBackend = OperatorBackend.Halcon)]
    public class FindScaledShapeModelHandle : ParameterHandleBase<FindScaledShapeModelParameter>, IHandleDataSlots
    {
        public FindScaledShapeModelHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.GrayImage, "输入灰度图像", typeof(HObject)),
            new RunSlot(RunOutputKeys.RawImage, "输入原始图像", typeof(HObject)),
            new RunSlot(RunOutputKeys.ShapeModelId, "形状模板 ID", typeof(HTuple)),
            new RunSlot(RunOutputKeys.ShapeModelContours, "模板轮廓", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "匹配轮廓", typeof(HObject)),
            new RunSlot(RunOutputKeys.Xld, "匹配 XLD", typeof(HObject)),
            new RunSlot(RunOutputKeys.MatchRows, "匹配行坐标", typeof(HTuple)),
            new RunSlot(RunOutputKeys.MatchColumns, "匹配列坐标", typeof(HTuple)),
            new RunSlot(RunOutputKeys.MatchAngles, "匹配角度", typeof(HTuple)),
            new RunSlot(RunOutputKeys.MatchScores, "匹配分数", typeof(HTuple))
        };

        public override void Handle()
        {
            var source = HandleInputGuard.RequireImage(HandleInputGuard.GetInputImage(this), "FindScaledShapeModel");

            var modelId = HandleInputGuard.RequireModelId(HandleInputGuard.GetInputModelId(this), "FindScaledShapeModel");
            using var angleStart = HalconTupleHelper.ToRadians(Parameter.AngleStartDeg);
            using var angleExtent = HalconTupleHelper.ToRadians(Parameter.AngleExtentDeg);

            HOperatorSet.FindScaledShapeModel(
                source,
                modelId,
                angleStart,
                angleExtent,
                Math.Max(0.0001, Parameter.ScaleMin),
                Math.Max(0.0001, Parameter.ScaleMax),
                Parameter.MinScore,
                Math.Max(0, Parameter.NumMatches),
                Parameter.MaxOverlap,
                Parameter.SubPixel.ToHalconString(),
                Math.Max(0, Parameter.NumLevels),
                Parameter.Greediness,
                out var rows,
                out var columns,
                out var angles,
                out var scales,
                out var scores);

            RunParameter.SetOutput(RunOutputKeys.MatchRows, rows);
            RunParameter.SetOutput(RunOutputKeys.MatchColumns, columns);
            RunParameter.SetOutput(RunOutputKeys.MatchAngles, angles);
            RunParameter.SetOutput("shape_match.scales", scales);
            RunParameter.SetOutput(RunOutputKeys.MatchScores, scores);
            RunParameter.ResultMessage = scores.Length == 0
                ? "Scaled shape matches: 0"
                : $"Scaled shape matches: {scores.Length}, best score: {scores[0].D:0.###}, scale: {scales[0].D:0.###}";

            if (scores.Length > 0)
            {
                var contours = BuildMatchContours(modelId, HandleInputGuard.GetInputModelContours(this), rows, columns, angles, scales);
                RunParameter.HOutput = contours;
                RunParameter.SetOutput(RunOutputKeys.Output, contours);
                RunParameter.SetOutput(RunOutputKeys.Xld, contours);
                RaiseImageDisplay(contours);
                return;
            }

            RunParameter.HOutput = source;
            RunParameter.SetOutput(RunOutputKeys.Output, source);
            RaiseImageDisplay(source);
        }

        private static HObject BuildMatchContours(HTuple modelId, HObject cachedContours, HTuple rows, HTuple columns, HTuple angles, HTuple scales)
        {
            var modelContours = cachedContours;
            var ownsModelContours = false;
            if (modelContours == null)
            {
                HOperatorSet.GetShapeModelContours(out modelContours, modelId, 1);
                ownsModelContours = true;
            }

            try
            {
                HOperatorSet.GenEmptyObj(out var allContours);
                for (var index = 0; index < rows.Length; index++)
                {
                    HOperatorSet.VectorAngleToRigid(0, 0, 0, rows[index], columns[index], angles[index], out var homMat2D);
                    using (homMat2D)
                    {
                        HOperatorSet.HomMat2dScale(homMat2D, scales[index], scales[index], rows[index], columns[index], out var scaledHomMat2D);
                        using (scaledHomMat2D)
                        {
                            HOperatorSet.AffineTransContourXld(modelContours, out var transformed, scaledHomMat2D);
                            HOperatorSet.ConcatObj(allContours, transformed, out var nextContours);
                            allContours.Dispose();
                            transformed.Dispose();
                            allContours = nextContours;
                        }
                    }
                }

                return allContours;
            }
            finally
            {
                if (ownsModelContours)
                {
                    modelContours.Dispose();
                }
            }
        }
    }
}
