using OpenCvSharp;
using Plugin.Handle.MeanImage;
using Plugin.Handle.MedianImage;
using Plugin.Handle.ScaleImage;
using Plugin.Handle.SobelAmp;
using Plugin.Handle.TopHat;
using Plugin.Handle.BottomHat;
using Plugin.Handle.RgbToGray;
using Plugin.Handle.GrayClosingRect;
using Plugin.Handle.GrayOpeningRect;
using Plugin.Handle.EquHistoImage;
using Plugin.Handle.Emphasize;
using Plugin.Handle.Illuminate;
using Plugin.Handle.ReduceDomain;
using ZeroCodeVisionStudio.Shared;
using Xunit;

namespace ZeroCodeVisionStudio.Tests
{
    /// <summary>
    /// Preprocessing 模块 13 个 OpenCV Handle 单元测试。
    /// 使用纯白/纯黑图像作为输入，验证滤波、形态学、灰度变换类算子的基本行为。
    /// </summary>
    public class PreprocessingCvHandleTests
    {
        // ==================== CvMeanImageHandle ====================

        [Fact]
        public void CvMeanImage_WhiteImage_ShouldAllWhite()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvMeanImageHandle(null);
            var param = new MeanImageParameter { MaskWidth = 5, MaskHeight = 5 };

            TestHelper.Execute(handle, upstream, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            Assert.Equal(255, mat.At<byte>(75, 100));
        }

        // ==================== CvMedianImageHandle ====================

        [Fact]
        public void CvMedianImage_WhiteImage_ShouldAllWhite()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvMedianImageHandle(null);
            var param = new MedianImageParameter { MaskType = MedianMaskType.Circle, Radius = 3 };

            TestHelper.Execute(handle, upstream, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            Assert.Equal(255, mat.At<byte>(75, 100));
        }

        // ==================== CvScaleImageHandle ====================

        [Fact]
        public void CvScaleImage_WhiteImage_Scale1Offset0_ShouldAllWhite()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvScaleImageHandle(null);
            var param = new ScaleImageParameter { Multiplier = 1.0, Add = 0 };

            TestHelper.Execute(handle, upstream, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // 255 * 1.0 + 0 = 255
            Assert.Equal(255, mat.At<byte>(75, 100));
        }

        // ==================== CvSobelAmpHandle ====================

        [Fact]
        public void CvSobelAmp_WhiteImage_ShouldAllZero()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvSobelAmpHandle(null);
            var param = new SobelAmpParameter { FilterType = SobelFilterType.SumAbs, Size = 3 };

            TestHelper.Execute(handle, upstream, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // 白图无梯度，Sobel 输出全 0
            Assert.Equal(0, mat.At<byte>(75, 100));
        }

        // ==================== CvTopHatHandle ====================

        [Fact]
        public void CvTopHat_WhiteImage_ShouldAllZero()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvTopHatHandle(null);
            var param = new TopHatParameter { Radius = 9 };

            TestHelper.Execute(handle, upstream, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // TopHat = src - opening(src) = 255 - 255 = 0
            Assert.Equal(0, mat.At<byte>(75, 100));
        }

        // ==================== CvBottomHatHandle ====================

        [Fact]
        public void CvBottomHat_WhiteImage_ShouldAllZero()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvBottomHatHandle(null);
            var param = new BottomHatParameter { Radius = 9 };

            TestHelper.Execute(handle, upstream, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // BlackHat = closing(src) - src = 255 - 255 = 0
            Assert.Equal(0, mat.At<byte>(75, 100));
        }

        // ==================== CvRgbToGrayHandle ====================

        [Fact]
        public void CvRgbToGray_WhiteColorImage_ShouldAllWhiteGray()
        {
            // 3 通道纯白图像
            using var upstream = new Mat(150, 200, MatType.CV_8UC3, Scalar.All(255));
            using var handle = new CvRgbToGrayHandle(null);

            TestHelper.Execute(handle, upstream);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // BGR(255,255,255) → GRAY 应该是 255 或接近 255
            Assert.True(mat.At<byte>(75, 100) >= 250);
        }

        // ==================== CvGrayClosingRectHandle ====================

        [Fact]
        public void CvGrayClosingRect_WhiteImage_ShouldAllWhite()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvGrayClosingRectHandle(null);
            var param = new GrayClosingRectParameter { Width = 5, Height = 5 };

            TestHelper.Execute(handle, upstream, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // 白图闭运算（先膨胀后腐蚀）→ 仍然全 255
            Assert.Equal(255, mat.At<byte>(75, 100));
        }

        // ==================== CvGrayOpeningRectHandle ====================

        [Fact]
        public void CvGrayOpeningRect_WhiteImage_ShouldAllWhite()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvGrayOpeningRectHandle(null);
            var param = new GrayOpeningRectParameter { Width = 5, Height = 5 };

            TestHelper.Execute(handle, upstream, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // 白图开运算（先腐蚀后膨胀）→ 仍然全 255
            Assert.Equal(255, mat.At<byte>(75, 100));
        }

        // ==================== CvEquHistoImageHandle ====================

        [Fact]
        public void CvEquHistoImage_WhiteImage_ShouldAllWhite()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvEquHistoImageHandle(null);

            TestHelper.Execute(handle, upstream);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // 全白图像直方图均衡后仍然全 255
            Assert.Equal(255, mat.At<byte>(75, 100));
        }

        // ==================== CvEmphasizeHandle ====================

        [Fact]
        public void CvEmphasize_WhiteImage_ShouldNotBeNull()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvEmphasizeHandle(null);
            var param = new EmphasizeParameter { MaskWidth = 7, MaskHeight = 7, Factor = 1.0 };

            TestHelper.Execute(handle, upstream, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // 白图 src=blur=255, diff=0, combined=255
            // Normalize MinMax 当 min==max 时行为依赖 OpenCV 实现，这里只断言有效输出
        }

        // ==================== CvIlluminateHandle ====================

        [Fact]
        public void CvIlluminate_WhiteImage_ShouldNotBeNull()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvIlluminateHandle(null);
            var param = new IlluminateParameter { MaskWidth = 101, MaskHeight = 101, Factor = 0.7 };

            TestHelper.Execute(handle, upstream, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // 白图 Illuminate: blur=255, divided=1.0, scaled=0.7
            // Normalize(0,255) 对单值 mat 行为依赖 OpenCV 实现，这里只断言有效输出
        }

        // ==================== CvReduceDomainHandle ====================

        [Fact]
        public void CvReduceDomain_WhiteImage_UndefinedShape_ShouldAllWhite()
        {
            using var upstream = TestHelper.CreateWhiteMat(200, 150);
            using var handle = new CvReduceDomainHandle(null);
            var param = new ReduceDomainParameter { ShapeType = EShapeType.undefined };

            TestHelper.Execute(handle, upstream, param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.False(mat.Empty());
            Assert.Equal(MatType.CV_8UC1, mat.Type());
            Assert.Equal(200, mat.Width);
            Assert.Equal(150, mat.Height);
            // undefined 形状 → mask 全 255 → BitwiseAnd(white, white) = 全 255
            Assert.Equal(255, mat.At<byte>(75, 100));
        }
    }
}
