using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace ZeroCodeVisionStudio.Tests
{
    /// <summary>
    /// 测试辅助工具：快速构造 RunParameter + 随机 Mat + 默认 Parameter + 执行 Handle 的封装。
    /// OpenCV Handle 不需要真正的 IDialogService（Handle() 不调用 EditParameters），所以直接 pass null。
    /// </summary>
    internal static class TestHelper
    {
        /// <summary>创建指定尺寸的灰度测试图（随机噪声）。</summary>
        public static Mat CreateGrayMat(int width = 200, int height = 150, int seed = 42)
        {
            var mat = new Mat(height, width, MatType.CV_8UC1, Scalar.All(0));
            var rng = new Random(seed);
            unsafe
            {
                byte* p = (byte*)mat.Data;
                for (int i = 0; i < height * width; i++)
                    p[i] = (byte)rng.Next(0, 256);
            }
            return mat;
        }

        /// <summary>创建纯黑灰度图。</summary>
        public static Mat CreateBlackMat(int width = 200, int height = 150)
            => new Mat(height, width, MatType.CV_8UC1, Scalar.All(0));

        /// <summary>创建纯白灰度图。</summary>
        public static Mat CreateWhiteMat(int width = 200, int height = 150)
            => new Mat(height, width, MatType.CV_8UC1, Scalar.All(255));

        /// <summary>
        /// 给 Handle 设置 RunParameter（含上游 Mat 输入）+ Parameter 对象，然后调用 Handle()。
        /// ParameterHandleBase<TParameter> 内部的 Parameter 属性在构造时已实例化，
        /// 所以这里是把传入 parameter 的属性值复制到已有的 HandleParameter 实例上。
        /// </summary>
        public static void Execute(IHandle handle, Mat upstreamMat = null, object parameter = null)
        {
            var rp = new RunParameter();
            if (upstreamMat != null && !upstreamMat.Empty())
            {
                rp.MatGrayImage = upstreamMat;
                rp.MatOutput = upstreamMat;
            }

            if (parameter != null)
            {
                var targetParam = handle.HandleParameter;
                if (targetParam != null)
                {
                    // 反射：把传入 parameter 的同名属性值复制到 HandleParameter 实例
                    foreach (var srcProp in parameter.GetType().GetProperties())
                    {
                        var dstProp = targetParam.GetType().GetProperty(srcProp.Name);
                        if (dstProp != null && dstProp.CanWrite && dstProp.PropertyType.IsAssignableFrom(srcProp.PropertyType))
                        {
                            dstProp.SetValue(targetParam, srcProp.GetValue(parameter));
                        }
                    }
                }
            }

            handle.SetRunParameter(rp);
            handle.Handle();
        }

        /// <summary>取 MatOutput 的像素值（安全处理 null/空 Mat）。</summary>
        public static byte GetPixel(Mat mat, int x, int y)
        {
            if (mat == null || mat.Empty()) return 0;
            return mat.At<byte>(y, x);
        }
    }
}
