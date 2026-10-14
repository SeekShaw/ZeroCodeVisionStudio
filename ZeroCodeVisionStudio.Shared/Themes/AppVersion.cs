using System;
using System.Diagnostics;
using System.Reflection;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 集中的版本信息读取入口。所有界面（SplashScreen / 主界面 / 关于对话框 / 设置页）
    /// 都从这里拿版本字符串，避免硬编码不一致。
    /// </summary>
    public static class AppVersion
    {
        /// <summary>
        /// 读主入口程序集（ZeroCodeVisionStudio.exe）的版本信息，
        /// 而不是 AppVersion 自己所在的 Shared DLL —— 这样界面和 EXE 属性窗口才能完全一致。
        /// </summary>
        private static readonly Lazy<Assembly> _asm = new(() => Assembly.GetEntryAssembly() ?? typeof(AppVersion).Assembly);
        private static readonly Lazy<FileVersionInfo> _fvi = new(() => FileVersionInfo.GetVersionInfo(_asm.Value.Location));

        /// <summary>完整产品版本（含 build metadata），例如 "2.1.1+build0001"。</summary>
        public static string ProductVersion
        {
            get
            {
                var attr = _asm.Value.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                return attr?.InformationalVersion ?? _asm.Value.GetName().Version?.ToString() ?? "0.0.0";
            }
        }

        /// <summary>三段式主版本号（例如 "2.1.1"）。
        /// 如果 InformationalVersion 不是标准版本号（含 +build / -beta 等后缀），则自动截取前三段。</summary>
        public static string ShortVersion
        {
            get
            {
                var raw = ProductVersion;
                var plus = raw.IndexOf('+');
                var dash = raw.IndexOf('-');
                var cut = plus >= 0 && dash >= 0 ? Math.Min(plus, dash)
                         : plus >= 0 ? plus
                         : dash >= 0 ? dash
                         : -1;
                var core = cut > 0 ? raw.Substring(0, cut) : (raw.Length > 0 ? raw : "0.0.0");
                var parts = core.Split('.');
                if (parts.Length >= 3) return $"{parts[0]}.{parts[1]}.{parts[2]}";
                return core;
            }
        }

        /// <summary>统一显示用："Ver 2.1.1.0" 格式，界面和 EXE 属性窗口的 FileVersion 完全一致。</summary>
        public static string Display => $"Ver {FileVersion}";

        /// <summary>Win32 文件版本（4 段数字，例如 "2.1.1.0"）。</summary>
        public static string FileVersion => _fvi.Value.FileVersion ?? "0.0.0.0";

        public static string ProductName => _fvi.Value.ProductName ?? "ZeroCode Vision Studio";
        public static string Company => _fvi.Value.CompanyName ?? "";
        public static string Copyright => _fvi.Value.LegalCopyright ?? "";
    }
}
