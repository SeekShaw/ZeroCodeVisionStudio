using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HalconVisionStudio.Shared.Diagnostics
{
    /// <summary>
    /// 异常信息统一格式化。
    /// 目标：任何到达用户的错误信息都必须可定位——什么异常、消息是什么、可能什么原因、从哪里抛出。
    /// Brief() 用于紧凑展示位（节点状态/执行记录）；Detail() 提供异常类型、内部异常链、项目堆栈帧与排查提示。
    /// </summary>
    public static class ExceptionFormatter
    {
        /// <summary>
        /// 简明单行：异常类型 + 消息（算子框架异常的消息本身已是结构化中文说明，直接透传）。
        /// </summary>
        public static string Brief(Exception ex)
        {
            if (ex == null) return "未知异常（异常对象为空）";

            var text = ex is HandleException
                ? ex.Message
                : $"{ex.GetType().Name}: {ex.Message}";

            if (text.Length > 600) text = text[..600] + "…";
            return text;
        }

        /// <summary>
        /// 诊断详情块（多行，可直接追加在简明消息之后）：
        /// 异常类型 / 内部异常链 / 可能原因提示 / 项目堆栈帧（过滤框架内部帧）。
        /// </summary>
        public static string Detail(Exception ex, int maxFrames = 6)
        {
            if (ex == null) return string.Empty;

            var sb = new StringBuilder();
            sb.Append("异常类型：").Append(ex.GetType().FullName);

            // 内部异常链（最多 3 层）
            var inner = ex.InnerException;
            var depth = 0;
            while (inner != null && depth < 3)
            {
                sb.AppendLine().Append("  └ 内部异常：").Append(inner.GetType().Name).Append("：").Append(inner.Message);
                inner = inner.InnerException;
                depth++;
            }

            var hint = Hint(ex);
            if (!string.IsNullOrEmpty(hint))
                sb.AppendLine().Append("可能原因：").Append(hint);

            var frames = ProjectFrames(ex, maxFrames);
            if (frames.Count > 0)
            {
                sb.AppendLine().Append("抛出位置：");
                foreach (var frame in frames)
                    sb.AppendLine().Append("  ").Append(frame);
            }

            return sb.ToString();
        }

        /// <summary>Brief + Detail 的组合（中间以空行分隔），用于结果区/对话框等大文本展示位。</summary>
        public static string Describe(Exception ex) =>
            Brief(ex) + Environment.NewLine + Environment.NewLine + Detail(ex);

        /// <summary>常见"难懂"异常的排查提示；无提示返回空。</summary>
        private static string Hint(Exception ex)
        {
            switch (ex)
            {
                case NullReferenceException:
                    return "空引用：代码访问了为空的对象。视觉流程中最常见原因是输入数据为空——上游节点未执行、连线断开或数据槽键不匹配，请检查该节点的输入连线与上游执行状态。";
                case ArgumentOutOfRangeException:
                case IndexOutOfRangeException:
                    return "索引越界：目标集合为空或元素数量少于预期（如轮廓/区域/匹配结果数量为 0），请确认上游输出有效。";
                case FormatException:
                    return "格式转换失败：字符串无法转为目标类型（常见于参数值或通讯数据含非法字符）。";
                case InvalidOperationException:
                    return "对象处于无效状态（如连接已断开、资源已释放），请检查连接/资源状态后重试。";
                default:
                    return ex is HandleException ? null : null;
            }
        }

        /// <summary>
        /// 提取用户关心的堆栈帧：优先项目代码帧（Plugin./HalconVisionStudio 命名空间），
        /// 一个都没有时退回原始前几帧。PDB 缺失时行号显示为省略。
        /// </summary>
        private static System.Collections.Generic.List<string> ProjectFrames(Exception ex, int maxFrames)
        {
            var result = new System.Collections.Generic.List<string>();
            if (ex.StackTrace == null) return result;

            var rawFrames = ex.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            var projectPrefixes = new[] { "Plugin.", "HalconVisionStudio" };

            foreach (var line in rawFrames)
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("at ", StringComparison.Ordinal)) continue;

                var isProjectFrame = projectPrefixes.Any(p => trimmed.Contains(p, StringComparison.Ordinal));
                if (!isProjectFrame) continue;

                // "   at Plugin.X.Y() in C:\\path\\File.cs:line 123" → 紧凑化为 "Plugin.X.Y() (File.cs:123)"
                var compact = Regex.Replace(trimmed, @"^at\s+(.*?)\s+in\s+.*?\\?([^\\/:]+):\s*line\s+(\d+)$", "$1 ($2:$3)");
                compact = Regex.Replace(compact, @"^at\s+(.*?)\s+in\s+.*?\\?([^\\/:]+)$", "$1 ($2)");
                if (compact == trimmed) compact = trimmed[3..]; // 无文件信息的帧去掉 "at " 前缀
                result.Add(compact);
                if (result.Count >= maxFrames) break;
            }

            if (result.Count == 0)
            {
                // 没有项目帧（如纯框架异常）：给出原始前 3 帧兜底
                foreach (var line in rawFrames.Take(3))
                    result.Add(line.Trim());
            }
            return result;
        }
    }
}
