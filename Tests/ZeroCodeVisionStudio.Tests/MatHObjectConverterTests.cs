using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;
using Xunit;

namespace ZeroCodeVisionStudio.Tests
{
    /// <summary>
    /// MatHObjectConverter 双向转换测试。
    /// HalconRuntimeProbe.IsAvailable 为 true 时验证完整 round-trip；为 false 时验证 graceful null。
    /// </summary>
    public class MatHObjectConverterTests
    {
        [Fact]
        public void MatToHObject_NullOrEmpty_ShouldReturnNull()
        {
            Assert.Null(MatHObjectConverter.MatToHObject(null));
            using var empty = new Mat();
            Assert.Null(MatHObjectConverter.MatToHObject(empty));
        }

        [Fact]
        public void HObjectToMat_Null_ShouldReturnNull()
        {
            Assert.Null(MatHObjectConverter.HObjectToMat(null));
        }

        [Fact]
        public void MatToHObject_Gray_WhenHalconAvailable()
        {
            if (!HalconRuntimeProbe.IsAvailable)
                return; // skip gracefully

            using var gray = TestHelper.CreateGrayMat(80, 60, seed: 123);

            var hobj = MatHObjectConverter.MatToHObject(gray);

            Assert.NotNull(hobj);
            hobj.Dispose();
        }

        [Fact]
        public void MatToHObject_Bgr_WhenHalconAvailable()
        {
            if (!HalconRuntimeProbe.IsAvailable)
                return;

            using var bgr = new Mat(60, 80, MatType.CV_8UC3, Scalar.All(0));
            bgr.SetTo(new Scalar(10, 20, 30));

            var hobj = MatHObjectConverter.MatToHObject(bgr);

            Assert.NotNull(hobj);
            hobj.Dispose();
        }

        [Fact]
        public void RoundTrip_GrayMat_HalconAvailable()
        {
            if (!HalconRuntimeProbe.IsAvailable)
                return;

            using var original = TestHelper.CreateGrayMat(100, 80, seed: 999);

            var hobj = MatHObjectConverter.MatToHObject(original);
            Assert.NotNull(hobj);

            var restored = MatHObjectConverter.HObjectToMat(hobj);
            hobj.Dispose();

            Assert.NotNull(restored);
            Assert.Equal(original.Width, restored.Width);
            Assert.Equal(original.Height, restored.Height);
            Assert.Equal(original.Type(), restored.Type());

            // 像素值应该一致（临时文件路径下可能有轻微差异但 PNG 是无损的）
            for (int y = 0; y < original.Height; y++)
                for (int x = 0; x < original.Width; x++)
                    Assert.Equal(original.At<byte>(y, x), restored.At<byte>(y, x));

            restored.Dispose();
        }

        [Fact]
        public void MaskToHRegion_Null_ShouldReturnNull()
        {
            Assert.Null(MatHObjectConverter.MaskToHRegion(null));
        }

        [Fact]
        public void MaskToHRegion_WhenHalconAvailable()
        {
            if (!HalconRuntimeProbe.IsAvailable)
                return;

            using var mask = new Mat(50, 50, MatType.CV_8UC1, Scalar.All(0));
            Cv2.Circle(mask, new Point(25, 25), 10, Scalar.White, -1);

            var region = MatHObjectConverter.MaskToHRegion(mask);
            Assert.NotNull(region);
            region.Dispose();
        }
    }
}
