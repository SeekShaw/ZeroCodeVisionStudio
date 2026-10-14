using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 算子框架异常类。
    /// 封装框架内部的结构化错误，通过 <see cref="ExceptionType"/> 枚举精确标识错误原因，
    /// 便于调用方根据错误类型执行不同的处理策略。
    /// </summary>
    public class HandleException : Exception
    {
        /// <summary>
        /// 使用指定的错误类型初始化算子框架异常。
        /// </summary>
        /// <param name="type">错误原因，见 <see cref="ExceptionType"/> 枚举。</param>
        public HandleException(ExceptionType type)
        {
            this.Type = type;
            this.CustomMessage = GetDefaultMessage(type);
        }

        /// <summary>
        /// 使用指定的错误类型和详细消息初始化算子框架异常。
        /// </summary>
        /// <param name="type">错误原因，见 <see cref="ExceptionType"/> 枚举。</param>
        /// <param name="message">详细的错误描述信息。</param>
        public HandleException(ExceptionType type, string message)
        {
            this.Type = type;
            this.CustomMessage = message;
        }

        /// <summary>异常消息，返回自定义消息或错误类型的枚举名称字符串。</summary>
        public override string Message => CustomMessage ?? Type.ToString();

        /// <summary>错误类型，标识此异常的具体原因。</summary>
        public ExceptionType Type { get; }

        /// <summary>自定义错误消息。</summary>
        private string CustomMessage { get; }

        /// <summary>
        /// 获取错误类型的默认中文描述。
        /// </summary>
        private static string GetDefaultMessage(ExceptionType type)
        {
            return type switch
            {
                ExceptionType.E_IMAGE_NULL => "输入图像为空，请检查上游节点是否已执行并输出图像。",
                ExceptionType.E_REGION_NULL => "输入区域为空，请检查阈值分割或区域处理节点是否已执行。",
                ExceptionType.E_FILE_NOT_FOUND => "指定的文件未找到",
                ExceptionType.E_MODEL_ID_NULL => "模板 ID 为空，请先执行创建或读取模板算子。",
                ExceptionType.E_CONTOUR_NULL => "输入轮廓为空，请检查边缘检测或模板轮廓节点是否已执行。",
                ExceptionType.E_TUPLE_NULL => "输入数据为空",
                ExceptionType.E_PARAMETER_INVALID => "参数设置无效",
                ExceptionType.E_HANDLE_ALREADY_REGISTERED => "算子已注册",
                ExceptionType.E_UPSTREAM_NOT_EXECUTED => "上游节点未执行，请先执行上游节点。",
                ExceptionType.E_NO_VALID_INPUT => "没有有效的输入数据。",
                _ => type.ToString()
            };
        }
    }
}
