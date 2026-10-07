using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HalconVisionStudio.Services
{
    /// <summary>最近打开方案（MRU）菜单项。</summary>
    public class SchemeMenuItem
    {
        /// <summary>显示名（文件名去扩展名）。</summary>
        public string DisplayName { get; set; }
        /// <summary>方案文件完整路径。</summary>
        public string FullPath { get; set; }
        /// <summary>说明（示例方案的用途描述）。</summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// 最近打开方案记录：应用目录 config/recent_schemes.json 持久化，最多 10 条，
    /// 加载时剔除已不存在的文件。打开方案与另存方案成功时登记。
    /// </summary>
    public static class RecentSchemeService
    {
        private const int MaxEntries = 10;

        private static string StorePath =>
            Path.Combine(AppContext.BaseDirectory, "config", "recent_schemes.json");

        public static List<string> Load()
        {
            try
            {
                if (!File.Exists(StorePath)) return new List<string>();
                var list = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(StorePath)) ?? new List<string>();
                // 剔除已被删除的文件，保持菜单可用性
                return list.FindAll(File.Exists);
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>登记一次打开（置顶去重，超出上限截断）。</summary>
        public static void Record(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            try
            {
                var list = Load();
                list.RemoveAll(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase));
                list.Insert(0, path);
                if (list.Count > MaxEntries) list.RemoveRange(MaxEntries, list.Count - MaxEntries);
                Directory.CreateDirectory(Path.GetDirectoryName(StorePath));
                File.WriteAllText(StorePath, JsonSerializer.Serialize(list));
            }
            catch
            {
                // 记录失败不影响主流程
            }
        }
    }
}
