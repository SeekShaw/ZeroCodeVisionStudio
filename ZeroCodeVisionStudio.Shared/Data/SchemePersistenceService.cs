using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ZeroCodeVisionStudio.Shared.Data
{
    /// <summary>
    /// 方案持久化服务，提供视觉工程方案数据的 JSON 序列化/反序列化与文件系统存储。
    /// <para>
    /// 默认存储目录：我的文档/ZeroCodeVisionStudio/Schemes/
    /// 文件格式：UTF-8 编码的 JSON，使用小驼峰命名，枚举值以字符串格式持久化。
    /// </para>
    /// </summary>
    public static class SchemePersistenceService
    {
        /// <summary>
        /// JSON 序列化选项：
        /// - 格式化缩进排版（便于人工阅读）
        /// - 属性名采用小驼峰规范（camelCase）
        /// - null 值不写入 JSON
        /// - 枚举采用字符串格式序列化（如 "on" 而非 0）
        /// </summary>
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        /// <summary>
        /// 方案文件的默认存储目录（位于当前用户的“我的文档/ZeroCodeVisionStudio/Schemes/”）。
        /// 目录不存在时在调用 <see cref="SaveAsync"/> 时会自动创建。
        /// </summary>
        public static string DefaultSchemeDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ZeroCodeVisionStudio", "Schemes");

        /// <summary>
        /// 异步将视觉方案序列化为 JSON 并写入指定文件路径。
        /// 父级目录不存在时会自动创建。
        /// </summary>
        /// <param name="filePath">目标文件路径（建议以 .json 为扩展名）。</param>
        /// <param name="scheme">要保存的方案数据对象。</param>
        public static async Task SaveAsync(string filePath, SchemeData scheme)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(scheme, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);
        }

        /// <summary>
        /// 从指定文件路径异步读取并反序列化方案数据。
        /// </summary>
        /// <param name="filePath">方案文件绝对路径。</param>
        /// <returns>反序列化后的 <see cref="SchemeData"/> 实例；若文件内容为空则返回新建的空方案。</returns>
        /// <exception cref="FileNotFoundException">文件不存在时抛出。</exception>
        /// <summary>同步保存（UI 线程构造期等不可阻塞等待 async 的场景使用）。</summary>
        public static void Save(string filePath, SchemeData scheme)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(filePath, JsonSerializer.Serialize(scheme, JsonOptions));
        }

        public static async Task<SchemeData> LoadAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"方案文件未找到：{filePath}");
            }

            return await Task.Run(() =>
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<SchemeData>(json, JsonOptions) ?? new SchemeData();
            });
        }

        /// <summary>
        /// 根据方案名称获取默认的完整文件路径（位于 <see cref="DefaultSchemeDirectory"/> 下）。
        /// 名称中的非法文件名字符会自动被替换为下划线。
        /// </summary>
        /// <param name="schemeName">方案名称。</param>
        /// <returns>默认文件路径（如 .../Schemes/我的方案.json）。</returns>
        public static string GetDefaultFilePath(string schemeName)
        {
            return Path.Combine(DefaultSchemeDirectory, $"{SanitizeFileName(schemeName)}.json");
        }

        /// <summary>
        /// 列出 <see cref="DefaultSchemeDirectory"/> 目录下所有已保存的方案文件路径。
        /// 若目录不存在则返回空数组。
        /// </summary>
        /// <returns>方案 JSON 文件的完整路径数组。</returns>
        public static string[] ListSavedSchemes()
        {
            if (!Directory.Exists(DefaultSchemeDirectory))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(DefaultSchemeDirectory, "*.json");
        }

        /// <summary>
        /// 将字符串中的非法文件名字符替换为下划线。
        /// 若处理后为空，则返回 "untitled"。
        /// </summary>
        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return string.IsNullOrWhiteSpace(name) ? "untitled" : name;
        }
    }
}
