using System;
using System.IO;
using HalconDotNet;
using OpenCvSharp;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// Centralized precondition checks for plugin handles.
    /// </summary>
    public static class HandleInputGuard
    {
        public static HObject RequireImage(HObject image, string operatorName, string inputName = "输入图像")
        {
            if (image != null)
            {
                return image;
            }

            throw new HandleException(
                ExceptionType.E_IMAGE_NULL,
                $"{operatorName}需要{inputName}，但当前输入为空。请先执行图像读取/相机采集/灰度转换等上游算子，并检查输入绑定是否选择了图像输出。");
        }

        /// <summary>OpenCV Mat 版本 RequireImage。</summary>
        public static Mat RequireImage(Mat mat, string operatorName, string inputName = "输入图像")
        {
            if (mat != null && !mat.Empty())
            {
                return mat;
            }

            throw new HandleException(
                ExceptionType.E_IMAGE_NULL,
                $"{operatorName}需要{inputName}，但当前输入为空。请先执行图像读取/相机采集/灰度转换等上游算子，并检查输入绑定是否选择了图像输出。");
        }

        public static HObject RequireRegion(HObject region, string operatorName, string inputName = "输入区域")
        {
            if (region != null)
            {
                return region;
            }

            throw new HandleException(
                ExceptionType.E_REGION_NULL,
                $"{operatorName}需要{inputName}，但当前输入为空。请先执行阈值分割、区域生成或区域处理算子，并检查输入绑定是否选择了区域输出。");
        }

        public static HObject RequireContour(HObject contour, string operatorName, string inputName = "输入轮廓")
        {
            if (contour != null)
            {
                return contour;
            }

            throw new HandleException(
                ExceptionType.E_CONTOUR_NULL,
                $"{operatorName}需要{inputName}，但当前输入为空。请先执行边缘检测、线条检测或模板轮廓输出算子，并检查输入绑定是否选择了 XLD/轮廓输出。");
        }

        public static HTuple RequireModelId(HTuple modelId, string operatorName, string inputName = "模板 ID")
        {
            if (modelId != null && modelId.Length > 0)
            {
                return modelId;
            }

            throw new HandleException(
                ExceptionType.E_MODEL_ID_NULL,
                $"{operatorName}需要{inputName}，但当前值为空。请先执行创建形状模板或读取形状模板算子，并检查输入绑定是否选择了模板 ID 输出。");
        }

        public static T RequireParameter<T>(T parameter, string operatorName) where T : class
        {
            if (parameter != null)
            {
                return parameter;
            }

            throw new HandleException(
                ExceptionType.E_PARAMETER_INVALID,
                $"{operatorName}的参数对象为空，无法执行。请重新打开参数窗口保存配置，或删除后重新添加该算子。");
        }

        public static string RequirePath(string path, string operatorName, string parameterName)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            throw new HandleException(
                ExceptionType.E_PARAMETER_INVALID,
                $"{operatorName}的{parameterName}为空，请在参数中设置有效路径。");
        }

        public static string RequireExistingFile(string path, string operatorName, string parameterName)
        {
            RequirePath(path, operatorName, parameterName);
            if (File.Exists(path))
            {
                return path;
            }

            throw new HandleException(
                ExceptionType.E_FILE_NOT_FOUND,
                $"{operatorName}找不到{parameterName}指定的文件：{path}");
        }

        public static void RequireCondition(bool condition, string operatorName, string message)
        {
            if (condition)
            {
                return;
            }

            throw new HandleException(
                ExceptionType.E_PARAMETER_INVALID,
                $"{operatorName}参数无效：{message}");
        }

        // ==================== 通用输入获取 ====================

        /// <summary>
        /// 从算子的 RunParameter 中获取输入图像：
        /// 优先读取命名输入（灰度图像 → 原始图像），未绑定时回退到 GrayImage/RawImage/HOutput 属性链。
        /// 返回 null 时可配合 <see cref="RequireImage"/> 做前置校验。
        /// </summary>
        public static HObject GetInputImage(IHandle handle)
        {
            var rp = handle.RunParameter;
            if (rp?.TryGetInput<HObject>(RunOutputKeys.GrayImage, out var grayInput) == true && grayInput != null)
            {
                return grayInput;
            }

            if (rp?.TryGetInput<HObject>(RunOutputKeys.RawImage, out var rawInput) == true && rawInput != null)
            {
                return rawInput;
            }

            return rp?.GrayImage ?? rp?.RawImage ?? rp?.HOutput;
        }

        /// <summary>
        /// OpenCV Mat 版本：从 RunParameter 获取 Mat 输入。
        /// 优先读 MatGrayImage / MatOutput，没有时回退到 HObject 字段并自动桥接转换。
        /// 返回空 Mat 表示无可用输入。
        /// </summary>
        public static Mat GetInputMat(IHandle handle)
        {
            var rp = handle.RunParameter;
            if (rp == null) return null;

            // 1. 优先 OpenCV 字段
            var mat = rp.MatGrayImage ?? rp.MatOutput;
            if (mat != null && !mat.Empty()) return mat;

            // 2. 回退 Halcon 字段 + 桥接
            var hobj = GetInputImage(handle);
            if (hobj != null && HalconRuntimeProbe.IsAvailable)
                return MatHObjectConverter.HObjectToMat(hobj);

            return null;
        }

        /// <summary>
        /// 从算子的 RunParameter 中获取形状模板 ID：
        /// 优先读取命名输入 shape_model.id，未绑定时回退到 Outputs 中的同名输出。
        /// </summary>
        public static HTuple GetInputModelId(IHandle handle)
        {
            var rp = handle.RunParameter;
            if (rp?.TryGetInput<HTuple>(RunOutputKeys.ShapeModelId, out var modelIdInput) == true && modelIdInput != null)
            {
                return modelIdInput;
            }

            return rp?.GetOutput<HTuple>(RunOutputKeys.ShapeModelId);
        }

        /// <summary>
        /// 从算子的 RunParameter 中获取形状模板轮廓：
        /// 优先读取命名输入 shape_model.contours，未绑定时回退到 Outputs 中的同名输出。
        /// </summary>
        public static HObject GetInputModelContours(IHandle handle)
        {
            var rp = handle.RunParameter;
            if (rp?.TryGetInput<HObject>(RunOutputKeys.ShapeModelContours, out var contoursInput) == true && contoursInput != null)
            {
                return contoursInput;
            }

            return rp?.GetOutput<HObject>(RunOutputKeys.ShapeModelContours);
        }
    }
}
