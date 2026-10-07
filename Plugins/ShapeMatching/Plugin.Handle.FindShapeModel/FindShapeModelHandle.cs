using HalconVisionStudio.Shared;
using HalconDotNet;

namespace Plugin.Handle.FindShapeModel
{
    [Handle("查找形状模板", "FindShapeModel", "在当前图像中查找 HALCON 形状模板实例。", Common.HandleType_ShapeMatching)]
    public class FindShapeModelHandle : ParameterHandleBase<FindShapeModelParameter>, IHandleDataSlots
    {
        public FindShapeModelHandle(IDialogService dialogService) : base(dialogService)
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
            var source = HandleInputGuard.RequireImage(HandleInputGuard.GetInputImage(this), "FindShapeModel");

            var modelId = HandleInputGuard.RequireModelId(HandleInputGuard.GetInputModelId(this), "FindShapeModel");
            using var angleStart = HalconTupleHelper.ToRadians(Parameter.AngleStartDeg);
            using var angleExtent = HalconTupleHelper.ToRadians(Parameter.AngleExtentDeg);

            HOperatorSet.FindShapeModel(
                source,
                modelId,
                angleStart,
                angleExtent,
                Parameter.MinScore,
                Math.Max(0, Parameter.NumMatches),
                Parameter.MaxOverlap,
                Parameter.SubPixel.ToHalconString(),
                Math.Max(0, Parameter.NumLevels),
                Parameter.Greediness,
                out var rows,
                out var columns,
                out var angles,
                out var scores);

            RunParameter.SetOutput(RunOutputKeys.MatchRows, rows);
            RunParameter.SetOutput(RunOutputKeys.MatchColumns, columns);
            RunParameter.SetOutput(RunOutputKeys.MatchAngles, angles);
            RunParameter.SetOutput(RunOutputKeys.MatchScores, scores);
            RunParameter.ResultMessage = BuildResultMessage(scores);

            if (scores.Length > 0)
            {
                var contours = BuildMatchContours(modelId, HandleInputGuard.GetInputModelContours(this), rows, columns, angles);
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

        private static string BuildResultMessage(HTuple scores)
        {
            return scores.Length == 0
                ? "Shape matches: 0"
                : $"Shape matches: {scores.Length}, best score: {scores[0].D:0.###}";
        }

        private static HObject BuildMatchContours(HTuple modelId, HObject cachedContours, HTuple rows, HTuple columns, HTuple angles)
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
                        HOperatorSet.AffineTransContourXld(modelContours, out var transformed, homMat2D);
                        HOperatorSet.ConcatObj(allContours, transformed, out var nextContours);
                        allContours.Dispose();
                        transformed.Dispose();
                        allContours = nextContours;
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
