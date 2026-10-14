using OpenCvSharp;
using Plugin.Handle.Connection;
using Plugin.Handle.FillUp;
using Plugin.Handle.OpeningRectangle;
using Plugin.Handle.ClosingRectangle;
using Plugin.Handle.Morphology;
using Plugin.Handle.SelectShape;
using Plugin.Handle.ShapeTrans;
using Plugin.Handle.Skeleton;
using Plugin.Handle.UnionRegion;
using Plugin.Handle.GenCircle;
using ZeroCodeVisionStudio.Shared;
using Xunit;

namespace ZeroCodeVisionStudio.Tests
{
    /// <summary>
    /// RegionProcessing 模块 9 个 OpenCV Handle 单元测试。
    /// 使用纯白图、圆形区域 mask 等作为输入，验证区域处理算子的基本行为。
    /// </summary>
    public class RegionProcessingCvHandleTests
    {
        // ==================== 辅助：生成圆形白区域 mask ====================

        private static Mat CreateCircleMask(int width = 200, int height = 150, int row = 75, int col = 100, int radius = 40)
        {
            using var upstream = TestHelper.CreateBlackMat(width, height);
            using var handle = new CvGenCircleHandle(null);
            var param = new GenCircleParameter { Row = row, Column = col, Radius = radius };
            TestHelper.Execute(handle, upstream, param);
            // 克隆 Mat 避免 RunParameter.Dispose 影响
            return handle.RunParameter.MatOutput.Clone();
        }

        // ==================== CvConnectionHandle ====================

        [Fact]
        public void CvConnection_CircleMask_ShouldReturnRegionMat()
        {
            // Arrange: 生成一个圆形白区域（黑色背景）
            using var circleMask = CreateCircleMask();
            using var handle = new CvConnectionHandle(null);

            // Act
            TestHelper.Execute(handle, circleMask);

            // Assert
            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // 圆心应该是白色区域
            Assert.Equal(255, mat.At<byte>(75, 100));
        }

        // ==================== CvFillUpHandle ====================

        [Fact]
        public void CvFillUp_CircleMask_ShouldNotBeNull()
        {
            using var circleMask = CreateCircleMask();
            using var handle = new CvFillUpHandle(null);

            TestHelper.Execute(handle, circleMask);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
        }

        // ==================== CvOpeningRectangleHandle ====================

        [Fact]
        public void CvOpeningRectangle_WhiteImage_ShouldAllWhite()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvOpeningRectangleHandle(null);
            var param = new OpeningRectangleParameter { Width = 5, Height = 5 };

            TestHelper.Execute(handle, upstream, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // 白图区域开运算 → 先腐蚀后膨胀 → 全 255
            Assert.Equal(255, mat.At<byte>(75, 100));
        }

        // ==================== CvClosingRectangleHandle ====================

        [Fact]
        public void CvClosingRectangle_WhiteImage_ShouldAllWhite()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvClosingRectangleHandle(null);
            var param = new ClosingRectangleParameter { Width = 5, Height = 5 };

            TestHelper.Execute(handle, upstream, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // 白图区域闭运算 → 全 255
            Assert.Equal(255, mat.At<byte>(75, 100));
        }

        // ==================== CvMorphologyHandle ====================

        [Fact]
        public void CvMorphology_WhiteImage_ClosingCircle_ShouldAllWhite()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvMorphologyHandle(null);
            var param = new MorphologyParameter
            {
                Operation = MorphologyOperation.ClosingCircle,
                Radius = 3.5
            };

            TestHelper.Execute(handle, upstream, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            Assert.Equal(255, mat.At<byte>(75, 100));
        }

        [Fact]
        public void CvMorphology_WhiteImage_ErosionCircle_ShouldNotGrow()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvMorphologyHandle(null);
            var param = new MorphologyParameter
            {
                Operation = MorphologyOperation.ErosionCircle,
                Radius = 3.5
            };

            TestHelper.Execute(handle, upstream, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // 白图腐蚀：全 255 被腐蚀后边界外缩，但内部仍是 255
            Assert.Equal(255, mat.At<byte>(75, 100));
        }

        // ==================== CvSelectShapeHandle ====================

        [Fact]
        public void CvSelectShape_CircleMask_ShouldReturnRegion()
        {
            // 用圆形 mask 作为输入（面积约 π*40² ≈ 5026）
            using var circleMask = CreateCircleMask();
            using var handle = new CvSelectShapeHandle(null);
            var param = new SelectShapeParameter
            {
                Feature = SelectShapeFeature.Area,
                Operation = SelectShapeOperation.And,
                Min = 100,
                Max = 99999999
            };

            TestHelper.Execute(handle, circleMask, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // 圆心在筛选范围内，应保留
            Assert.Equal(255, mat.At<byte>(75, 100));
        }

        // ==================== CvShapeTransHandle ====================

        [Fact]
        public void CvShapeTrans_CircleMask_Rectangle1_ShouldReturnRectangle()
        {
            using var circleMask = CreateCircleMask();
            using var handle = new CvShapeTransHandle(null);
            var param = new ShapeTransParameter { Mode = ShapeTransMode.Rectangle1 };

            TestHelper.Execute(handle, circleMask, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // BoundingRect 把圆变成矩形，圆心仍在矩形内
            Assert.Equal(255, mat.At<byte>(75, 100));
        }

        // ==================== CvSkeletonHandle ====================

        [Fact]
        public void CvSkeleton_CircleMask_ShouldNotBeNull()
        {
            using var circleMask = CreateCircleMask();
            using var handle = new CvSkeletonHandle(null);

            TestHelper.Execute(handle, circleMask);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // 骨架应该有至少一些非零像素（圆的骨架是一个点或十字）
            Assert.True(Cv2.CountNonZero(mat) > 0);
        }

        // ==================== CvUnionRegionHandle ====================

        [Fact]
        public void CvUnionRegion_SingleMat_ShouldNotBeNull()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvUnionRegionHandle(null);

            TestHelper.Execute(handle, upstream);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
        }
    }
}
