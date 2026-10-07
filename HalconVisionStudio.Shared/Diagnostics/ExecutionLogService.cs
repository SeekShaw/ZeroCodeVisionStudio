using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace HalconVisionStudio.Shared.Diagnostics
{
    /// <summary>执行日志类别。</summary>
    public enum ExecLogCategory
    {
        /// <summary>流程运行（开始/结束/停止/触发）。</summary>
        Workflow,
        /// <summary>算子节点执行。</summary>
        Node,
        /// <summary>AI 工具调用（内置 Copilot 与 MCP Server 共用）。</summary>
        AiTool,
        /// <summary>AI 大模型请求（对话/失败重试）。</summary>
        AiChat,
        /// <summary>通讯驱动（预留，通讯审计并入）。</summary>
        Comm,
        /// <summary>应用级事件（启动/退出/全局异常）。</summary>
        App
    }

    /// <summary>日志级别。</summary>
    public enum ExecLogLevel
    {
        Info,
        Warn,
        Error
    }

    /// <summary>单条执行日志。</summary>
    public class ExecutionLogEntry
    {
        public DateTime Timestamp { get; init; } = DateTime.Now;
        public ExecLogCategory Category { get; init; }
        public ExecLogLevel Level { get; init; }
        /// <summary>作用域：节点名 / 工具名 / 流程名。</summary>
        public string Scope { get; init; } = "";
        /// <summary>单行摘要。</summary>
        public string Message { get; init; } = "";
        /// <summary>多行详情（异常堆栈/参数等），可空。</summary>
        public string Detail { get; init; } = "";
        /// <summary>耗时（毫秒），可空。</summary>
        public double? DurationMs { get; init; }

        public string CategoryText => Category switch
        {
            ExecLogCategory.Workflow => "流程",
            ExecLogCategory.Node => "算子",
            ExecLogCategory.AiTool => "AI工具",
            ExecLogCategory.AiChat => "AI会话",
            ExecLogCategory.Comm => "通讯",
            _ => "应用"
        };

        public string LevelText => Level switch
        {
            ExecLogLevel.Warn => "警告",
            ExecLogLevel.Error => "错误",
            _ => "信息"
        };

        public string OneLine =>
            $"{Timestamp:HH:mm:ss.fff} [{LevelText}][{CategoryText}] {(string.IsNullOrEmpty(Scope) ? "" : Scope + " · ")}{Message}" +
            (DurationMs.HasValue ? $" (+{DurationMs.Value:F0}ms)" : "");
    }

    /// <summary>
    /// 统一执行日志服务：流程运行 / 算子执行 / AI 工具调用 / 大模型请求。
    /// <para>
    /// - 内存环形缓冲（最近 2000 条）供日志查看器实时显示；
    /// - 异步批量落盘到 %APPDATA%\HalconVisionStudio\logs\exec_日期.log（500ms 刷新）；
    /// - 单文件超 10MB 轮转 .1，启动时清理 14 天前的旧日志；
    /// - 高频场景（连续运行）可对单条日志选择仅内存（toFile:false），失败仍强制落盘。
    /// </para>
    /// </summary>
    public static class ExecutionLogService
    {
        private const int MemoryCapacity = 2000;
        private const int RetainDays = 14;
        private const long MaxFileBytes = 10 * 1024 * 1024;

        // 日志放在应用程序根目录的 logs 文件夹（现场运维直接在 exe 旁就能找到；
        // 开发期位于构建输出目录下）。滚动清理与轮转策略不变。
        private static readonly string LogDir = Path.Combine(AppContext.BaseDirectory, "logs");

        private static readonly ConcurrentQueue<ExecutionLogEntry> pendingWrites = new();
        private static readonly object fileLock = new();
        private static readonly object bufferLock = new();
        private static readonly LinkedList<ExecutionLogEntry> buffer = new();
        private static readonly Timer flushTimer;
        private static int flushRunning;

        /// <summary>新日志产生事件（后台线程触发，UI 订阅方需自行调度到 UI 线程）。</summary>
        public static event Action<ExecutionLogEntry> EntryLogged;

        /// <summary>日志目录（查看器"打开目录"用）。</summary>
        public static string LogDirectory => LogDir;

        static ExecutionLogService()
        {
            try { CleanupOldLogs(); } catch { }
            flushTimer = new Timer(_ => Flush(), null, 500, 500);
        }

        // ==================== 记录入口 ====================

        public static void Log(ExecLogCategory category, ExecLogLevel level, string scope, string message,
            string detail = null, double? durationMs = null, bool toFile = true)
        {
            if (string.IsNullOrEmpty(message) && string.IsNullOrEmpty(detail)) return;

            var entry = new ExecutionLogEntry
            {
                Category = category,
                Level = level,
                Scope = scope ?? "",
                Message = (message ?? "").ReplaceLineEndings(" ⏎ "),
                Detail = Truncate(detail, 4000),
                DurationMs = durationMs
            };

            lock (bufferLock)
            {
                buffer.AddLast(entry);
                while (buffer.Count > MemoryCapacity) buffer.RemoveFirst();
            }

            if (toFile) pendingWrites.Enqueue(entry);

            try { EntryLogged?.Invoke(entry); } catch { }
        }

        /// <summary>流程级日志。</summary>
        public static void Workflow(string message, string scope = null, ExecLogLevel level = ExecLogLevel.Info,
            string detail = null, double? durationMs = null, bool toFile = true)
            => Log(ExecLogCategory.Workflow, level, scope, message, detail, durationMs, toFile);

        /// <summary>算子节点日志。</summary>
        public static void Node(string message, string scope, ExecLogLevel level = ExecLogLevel.Info,
            string detail = null, double? durationMs = null, bool toFile = true)
            => Log(ExecLogCategory.Node, level, scope, message, detail, durationMs, toFile);

        /// <summary>AI 工具调用日志（参数摘要自动截断）。</summary>
        public static void AiTool(string tool, string argsJson, double durationMs, bool success,
            string message, string detail = null)
            => Log(ExecLogCategory.AiTool, success ? ExecLogLevel.Info : ExecLogLevel.Error, tool,
                success ? message : $"执行失败：{message}",
                detail ?? (string.IsNullOrWhiteSpace(argsJson) ? null : "参数: " + Truncate(argsJson, 600)),
                durationMs);

        /// <summary>AI 大模型请求日志。</summary>
        public static void AiChat(string message, ExecLogLevel level = ExecLogLevel.Info, string detail = null)
            => Log(ExecLogCategory.AiChat, level, null, message, detail);

        /// <summary>应用级日志。</summary>
        public static void App(string message, ExecLogLevel level = ExecLogLevel.Info, string detail = null)
            => Log(ExecLogCategory.App, level, null, message, detail);

        /// <summary>异常日志（含完整堆栈）。</summary>
        public static void Error(ExecLogCategory category, string scope, string message, Exception ex)
            => Log(category, ExecLogLevel.Error, scope, message, ex?.ToString());

        // ==================== 读取 / 落盘 ====================

        /// <summary>当前内存缓冲快照（旧→新）。</summary>
        public static ExecutionLogEntry[] Snapshot()
        {
            lock (bufferLock) return buffer.ToArray();
        }

        /// <summary>立即落盘全部待写日志（应用退出时调用）。</summary>
        public static void Flush()
        {
            if (Interlocked.Exchange(ref flushRunning, 1) != 0) return;
            try
            {
                var sb = new StringBuilder();
                while (pendingWrites.TryDequeue(out var entry))
                {
                    AppendEntry(sb, entry);
                }
                if (sb.Length > 0) WriteFile(sb.ToString());
            }
            catch { }
            finally
            {
                Interlocked.Exchange(ref flushRunning, 0);
            }
        }

        private static void AppendEntry(StringBuilder sb, ExecutionLogEntry entry)
        {
            sb.Append(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"))
              .Append(" [").Append(entry.LevelText).Append("][").Append(entry.CategoryText).Append("] ");
            if (!string.IsNullOrEmpty(entry.Scope)) sb.Append('(').Append(entry.Scope).Append(") ");
            sb.Append(entry.Message);
            if (entry.DurationMs.HasValue) sb.Append($" (+{entry.DurationMs.Value:F0}ms)");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(entry.Detail))
            {
                foreach (var line in entry.Detail.Split('\n'))
                    sb.Append("    ").Append(line.TrimEnd('\r')).AppendLine();
            }
        }

        private static void WriteFile(string text)
        {
            lock (fileLock)
            {
                try
                {
                    Directory.CreateDirectory(LogDir);
                    var path = Path.Combine(LogDir, $"exec_{DateTime.Now:yyyyMMdd}.log");

                    // 单文件轮转：超限把当前文件挪为 .1（覆盖旧 .1）。
                    var info = new FileInfo(path);
                    if (info.Exists && info.Length > MaxFileBytes)
                    {
                        File.Delete(path + ".1");
                        File.Move(path, path + ".1");
                    }

                    File.AppendAllText(path, text, new UTF8Encoding(false));
                }
                catch
                {
                    // 磁盘满/占用等：丢弃本批，绝不让日志影响主流程
                }
            }
        }

        private static void CleanupOldLogs()
        {
            var dir = new DirectoryInfo(LogDir);
            if (!dir.Exists) return;
            var cutoff = DateTime.Now.AddDays(-RetainDays);
            foreach (var file in dir.GetFiles("exec_*.log*"))
            {
                try
                {
                    if (file.LastWriteTime < cutoff) file.Delete();
                }
                catch { }
            }
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text;
            return text.Substring(0, max) + $"…(已截断，共 {text.Length} 字符)";
        }
    }
}
