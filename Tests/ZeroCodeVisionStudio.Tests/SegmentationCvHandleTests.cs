using OpenCvSharp;
using Plugin.Handle.Threshold;
using Plugin.Handle.AutoThreshold;
using Plugin.Handle.BinaryThreshold;
using Plugin.Handle.DynThreshold;
using Plugin.Handle.FastThreshold;
using Plugin.Handle.LocalThreshold;
using Plugin.Handle.VarThreshold;
using Plugin.Handle.HysteresisThreshold;
using ZeroCodeVisionStudio.Shared;
using Xunit;

namespace ZeroCodeVisionStudio.Tests
{
    /// <summary>
    /// Segmentation 模块 8 个 OpenCV Handle 单元测试。
    /// 使用纯白/纯黑图像作为输入，验证阈值分割类算子的基本行为。
    /// </summary>
    public class SegmentationCvHandleTests
    {
        // ==================== CvThresholdHandle ====================

        [Fact]
        public void CvThreshold_WhiteImage_Min0Max128_ShouldAllZero()
        {
            // Arrange
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvThresholdHandle(null);
            var param = new ThresholdParameter { MinGray = 0, MaxGray = 128 };

            // Act
            TestHelper.Execute(handle, upstream, param);

            // Assert
            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // 白图 255 不在 [0,128] 范围内 → mask 全 0
            Assert.Equal(0, mat.At<byte>(75, 100));
        }

        [Fact]
        public void CvThreshold_WhiteImage_Min127Max255_ShouldAllWhite()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvThresholdHandle(null);
            var param = new ThresholdParameter { MinGray = 127, MaxGray = 255 };

            TestHelper.Execute(handle, upstream, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // 白图 255 在 [127,255] 范围内 → mask 全 255
            Assert.Equal(255, mat.At<byte>(75, 100));
        }

        // ==================== CvAutoThresholdHandle ====================

        [Fact]
        public void CvAutoThreshold_WhiteImage_ShouldAllWhite()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvAutoThresholdHandle(null);

            TestHelper.Execute(handle, upstream);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // 白图 Otsu 自动阈值 → 全 255
            Assert.Equal(255, mat.At<byte>(75, 100));
        }

        [Fact]
        public void CvAutoThreshold_BlackImage_ShouldAllZero()
        {
            using var upstream = TestHelper.CreateBlackMat(200, 150);
            using var handle = new CvAutoThresholdHandle(null);

            TestHelper.Execute(handle, upstream);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // 黑图 Otsu 自动阈值 → 全 0
            Assert.Equal(0, mat.At<byte>(75, 100));
        }

        // ==================== CvBinaryThresholdHandle ====================

        [Fact]
        public void CvBinaryThreshold_WhiteImage_LightPolarity_ShouldAllWhite()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvBinaryThresholdHandle(null);
            var param = new BinaryThresholdParameter
            {
                LightDark = ELightDark.light,
                Method = EMethod.max_separability
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

        // ==================== CvDynThresholdHandle ====================

        [Fact]
        public void CvDynThreshold_WhiteImage_LightPolarity_ShouldAllWhite()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvDynThresholdHandle(null);
            var param = new DynThresholdParameter
            {
                MaskWidth = 25,
                MaskHeight = 25,
                Offset = 5,
                LightDark = DynThresholdMode.light
            };

            TestHelper.Execute(handle, upstream, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // MeanC 局部均值 255, Binary: 255 > 255-5=250 → 全 255
            Assert.Equal(255, mat.At<byte>(75, 100));
        }

        // ==================== CvFastThresholdHandle ====================

        [Fact]
        public void CvFastThreshold_WhiteImage_Min0Max255_ShouldAllWhite()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvFastThresholdHandle(null);
            var param = new FastThresholdParameter { MinGray = 0, MaxGray = 255, MinSize = 20 };

            TestHelper.Execute(handle, upstream, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            Assert.Equal(255, mat.At<byte>(75, 100));
        }

        // ==================== CvLocalThresholdHandle ====================

        [Fact]
        public void CvLocalThreshold_WhiteImage_LightPolarity_ShouldAllWhite()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvLocalThresholdHandle(null);
            var param = new LocalThresholdParameter
            {
                MaskWidth = 15,
                MaskHeight = 15,
                Scale = 0.2,
                LightDark = ELightDark.light
            };

            TestHelper.Execute(handle, upstream, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // GaussianC 局部均值 255, Binary: 255 > 255-51=204 → 全 255
            Assert.Equal(255, mat.At<byte>(75, 100));
        }

        // ==================== CvVarThresholdHandle ====================

        [Fact]
        public void CvVarThreshold_WhiteImage_LightPolarity_ShouldAllWhite()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvVarThresholdHandle(null);
            var param = new VarThresholdParameter
            {
                MaskWidth = 15,
                MaskHeight = 15,
                StdDevScale = 0.2,
                AbsThreshold = 10,
                LightDark = DynThresholdMode.light
            };

            TestHelper.Execute(handle, upstream, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // MeanC 局部均值 255, Binary: 255 > 255-10=245 → 全 255
            Assert.Equal(255, mat.At<byte>(75, 100));
        }

        // ==================== CvHysteresisThresholdHandle ====================

        [Fact]
        public void CvHysteresisThreshold_WhiteImage_High200Low100_ShouldAllWhite()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvHysteresisThresholdHandle(null);
            var param = new HysteresisThresholdParameter { Low = 100, High = 200, MaxLength = 10 };

            TestHelper.Execute(handle, upstream, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // strong(>200)=全 255, weak(>100)=全 255, dilatedStrong=全 255, AND=全 255
            Assert.Equal(255, mat.At<byte>(75, 100));
        }
    }
}
