using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;
using Xunit;

namespace ZeroCodeVisionStudio.Tests
{
    /// <summary>
    /// RunParameter 的 MatOutput / MatGrayImage 生命周期测试。
    /// 验证 Clone / Reset / MergeFrom / SetOutput 对 Mat 的正确处理。
    /// </summary>
    public class RunParameterMatTests
    {
        [Fact]
        public void SetMatOutput_ShouldBeReadable()
        {
            var rp = new RunParameter();
            using var mat = TestHelper.CreateGrayMat(100, 80);

            rp.MatOutput = mat;

            Assert.NotNull(rp.MatOutput);
            Assert.Equal(100, rp.MatOutput.Width);
            Assert.Equal(80, rp.MatOutput.Height);
            Assert.Equal(MatType.CV_8UC1, rp.MatOutput.Type());
        }

        [Fact]
        public void Clone_ShouldDeepCloneMat()
        {
            var rp = new RunParameter();
            using var mat = TestHelper.CreateGrayMat(100, 80);
            rp.MatOutput = mat;

            var clone = (RunParameter)rp.Clone();

            // Clone 应该深拷贝，修改 clone 不影响原对象
            clone.MatOutput.SetTo(Scalar.All(0));
            Assert.NotEqual(0, rp.MatOutput.At<byte>(40, 40));
            Assert.Equal(0, clone.MatOutput.At<byte>(40, 40));
        }

        [Fact]
        public void CloneShared_ShouldNotCloneMat()
        {
            var rp = new RunParameter();
            using var mat = TestHelper.CreateGrayMat(100, 80);
            rp.MatOutput = mat;

            var shared = rp.CloneShared();

            // CloneShared 应该是同引用
            Assert.Same(rp.MatOutput, shared.MatOutput);
            // 修改 shared 会影响 rp（因为同引用）
            shared.MatOutput.SetTo(Scalar.All(0));
            Assert.Equal(0, rp.MatOutput.At<byte>(40, 40));
        }

        [Fact]
        public void Reset_ShouldClearMatFields()
        {
            var rp = new RunParameter();
            using var mat = TestHelper.CreateGrayMat(100, 80);
            rp.MatOutput = mat;
            rp.MatGrayImage = mat;

            rp.Reset();

            Assert.Null(rp.MatOutput);
            Assert.Null(rp.MatGrayImage);
        }

        [Fact]
        public void MergeFrom_ShouldCopyMatFields()
        {
            var src = new RunParameter();
            using var mat = TestHelper.CreateGrayMat(100, 80);
            src.MatOutput = mat;

            var dst = new RunParameter();
            dst.MergeFrom(src);

            Assert.NotNull(dst.MatOutput);
            Assert.Equal(100, dst.MatOutput.Width);
            Assert.Equal(80, dst.MatOutput.Height);
        }

        [Fact]
        public void SetOutput_Mat_ShouldBeRetrievable()
        {
            var rp = new RunParameter();
            using var mat = TestHelper.CreateGrayMat(100, 80);

            rp.SetOutput("test.mat", mat);

            var retrieved = rp.GetOutput<Mat>("test.mat");
            Assert.Same(mat, retrieved);
        }

        [Fact]
        public void MatGrayImage_SetAndRead()
        {
            var rp = new RunParameter();
            using var mat = TestHelper.CreateBlackMat(50, 50);

            rp.MatGrayImage = mat;

            Assert.Same(mat, rp.MatGrayImage);
        }

        [Fact]
        public void SetMat_ShouldDisposeOldMat()
        {
            var rp = new RunParameter();
            var oldMat = TestHelper.CreateBlackMat(50, 50);
            rp.MatOutput = oldMat;
            var oldHandle = rp.MatOutput;

            using var newMat = TestHelper.CreateWhiteMat(50, 50);
            rp.MatOutput = newMat;

            Assert.Same(newMat, rp.MatOutput);
            // oldMat 被 Dispose 后 IsDisposed 应为 true
            Assert.True(oldMat.IsDisposed);
        }
    }
}
