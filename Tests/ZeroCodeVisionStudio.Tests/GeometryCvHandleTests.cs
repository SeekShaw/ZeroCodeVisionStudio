using OpenCvSharp;
using Plugin.Handle.GenCircle;
using Plugin.Handle.GenEllipse;
using Plugin.Handle.GenRectangle1;
using Plugin.Handle.GenRectangle2;
using Plugin.Handle.GenEmptyRegion;
using Xunit;

namespace ZeroCodeVisionStudio.Tests
{
    /// <summary>
    /// Geometry 模块 5 个 OpenCV Handle 单元测试。
    /// 每个测试：Arrange(Handle+Parameter) → Act(Handle()) → Assert(输出 Mat 的 Size/Type/关键像素)。
    /// </summary>
    public class GeometryCvHandleTests
    {
        // ==================== CvGenCircleHandle ====================

        [Fact]
        public void CvGenCircle_DefaultSize_CircleDrawn()
        {
            // Arrange
            using var handle = new CvGenCircleHandle(null);
            var param = new GenCircleParameter { Row = 150, Column = 200, Radius = 50 };

            // Act
            TestHelper.Execute(handle, parameter: param);

            // Assert
            var rp = handle.RunParameter;
            Assert.NotNull(rp.MatOutput);
            Assert.Equal(800, rp.MatOutput.Width);   // 默认
            Assert.Equal(600, rp.MatOutput.Height);   // 默认
            Assert.Equal(MatType.CV_8UC1, rp.MatOutput.Type());

            // 圆心像素应该是白色（填充圆）
            Assert.Equal(255, rp.MatOutput.At<byte>(150, 200));
            // 离圆心足够远的角落应该是黑色
            Assert.Equal(0, rp.MatOutput.At<byte>(10, 10));
        }

        [Fact]
        public void CvGenCircle_UsesUpstreamImageSize()
        {
            // Arrange: 上游 Mat 尺寸决定输出尺寸
            using var upstream = TestHelper.CreateBlackMat(300, 200);
            using var handle = new CvGenCircleHandle(null);
            var param = new GenCircleParameter { Row = 100, Column = 150, Radius = 30 };

            // Act
            TestHelper.Execute(handle, upstream, param);

            // Assert
            Assert.Equal(300, handle.RunParameter.MatOutput.Width);
            Assert.Equal(200, handle.RunParameter.MatOutput.Height);
            Assert.Equal(255, handle.RunParameter.MatOutput.At<byte>(100, 150));
        }

        // ==================== CvGenEllipseHandle ====================

        [Fact]
        public void CvGenEllipse_CenterIsWhite()
        {
            using var handle = new CvGenEllipseHandle(null);
            var param = new GenEllipseParameter
            {
                Row = 200, Column = 250,
                Phi = 0, Radius1 = 60, Radius2 = 30
            };

            TestHelper.Execute(handle, parameter: param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.Equal(800, mat.Width);
            Assert.Equal(600, mat.Height);
            Assert.Equal(255, mat.At<byte>(200, 250));
            Assert.Equal(0, mat.At<byte>(5, 5));
        }

        [Fact]
        public void CvGenEllipse_RotatedByPhi()
        {
            // Phi=π/2 表示椭圆旋转 90°（短轴变水平）
            using var handle = new CvGenEllipseHandle(null);
            var param = new GenEllipseParameter
            {
                Row = 300, Column = 400,
                Phi = Math.PI / 2, Radius1 = 60, Radius2 = 30
            };

            TestHelper.Execute(handle, parameter: param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            // 圆心还是白的
            Assert.Equal(255, mat.At<byte>(300, 400));
        }

        // ==================== CvGenRectangle1Handle ====================

        [Fact]
        public void CvGenRectangle1_DiagonalCorners()
        {
            using var handle = new CvGenRectangle1Handle(null);
            var param = new GenRectangle1Parameter
            {
                Row1 = 100, Column1 = 150,
                Row2 = 300, Column2 = 400
            };

            TestHelper.Execute(handle, parameter: param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            // 取矩形内部点（远离边界，避免 AntiAlias 不精确）
            Assert.Equal(255, mat.At<byte>(150, 200));  // 内部
            Assert.Equal(0, mat.At<byte>(5, 5));        // 角落外
            Assert.Equal(0, mat.At<byte>(500, 700));    // 右下外
        }

        // ==================== CvGenRectangle2Handle ====================

        [Fact]
        public void CvGenRectangle2_RotatedBox()
        {
            using var handle = new CvGenRectangle2Handle(null);
            var param = new GenRectangle2Parameter
            {
                Row = 300, Column = 400,
                Phi = Math.PI / 4,     // 45° 旋转
                Length1 = 80, Length2 = 50
            };

            TestHelper.Execute(handle, parameter: param);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.Equal(800, mat.Width);
            Assert.Equal(600, mat.Height);
            Assert.Equal(255, mat.At<byte>(300, 400));
            Assert.Equal(0, mat.At<byte>(5, 5));
        }

        // ==================== CvGenEmptyRegionHandle ====================

        [Fact]
        public void CvGenEmptyRegion_AllBlack()
        {
            using var handle = new CvGenEmptyRegionHandle(null);

            TestHelper.Execute(handle);

            var mat = handle.RunParameter.MatOutput;
            Assert.NotNull(mat);
            Assert.Equal(800, mat.Width);
            Assert.Equal(600, mat.Height);

            // 整个 Mat 应该全 0
            var sum = 0;
            unsafe
            {
                byte* p = (byte*)mat.Data;
                for (int i = 0; i < mat.Rows * mat.Cols; i++) sum += p[i];
            }
            Assert.Equal(0, sum);
        }

        [Fact]
        public void CvGenEmptyRegion_UsesUpstreamSize()
        {
            using var upstream = TestHelper.CreateGrayMat(123, 456);
            using var handle = new CvGenEmptyRegionHandle(null);

            TestHelper.Execute(handle, upstream);

            Assert.Equal(123, handle.RunParameter.MatOutput.Width);
            Assert.Equal(456, handle.RunParameter.MatOutput.Height);
        }
    }
}
