using System;
using System.IO;
using System.Text.Json;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 工作区持久化设置（存 %APPDATA%/ZeroCodeVisionStudio/workspace.json）。
    /// 与方案文件分离 — 这里放的是"软件级"设置，不随方案走。
    /// </summary>
    public static class AppWorkspaceSettings
    {
        private static string ConfigFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ZeroCodeVisionStudio",
            "workspace.json");

        /// <summary>启动时左侧工具箱是否展开（true=展开显示算子列表，false=收起只显示工具条）。</summary>
        public static bool ToolboxStartExpanded { get; private set; } = true;

        public static event EventHandler ToolboxStartExpandedChanged;

        public static void Initialize()
        {
            Load();
        }

        public static void SetToolboxStartExpanded(bool expanded)
        {
            if (ToolboxStartExpanded == expanded) return;
            ToolboxStartExpanded = expanded;
            Save();
            ToolboxStartExpandedChanged?.Invoke(null, EventArgs.Empty);
        }

        private static void Load()
        {
            try
            {
                var path = ConfigFilePath;
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    ToolboxStartExpanded = root.TryGetProperty(nameof(ToolboxStartExpanded), out var v) && v.GetBoolean();
                }
            }
            catch
            {
                // 读失败就用默认值（展开），不要阻塞启动
            }
        }

        private static void Save()
        {
            try
            {
                var path = ConfigFilePath;
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(new
                {
                    ToolboxStartExpanded
                });
                File.WriteAllText(path, json);
            }
            catch
            {
                // 写失败静默吞（UAC/权限问题等）
            }
        }
    }
}
