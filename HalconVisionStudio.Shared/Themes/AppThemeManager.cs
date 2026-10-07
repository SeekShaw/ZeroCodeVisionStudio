using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ControlzEx.Theming;

namespace HalconVisionStudio.Shared
{
    public sealed class ThemeAccentOption
    {
        public ThemeAccentOption(string name, string displayName, Color previewColor)
        {
            Name = name;
            DisplayName = displayName;
            PreviewBrush = new SolidColorBrush(previewColor);
            PreviewBrush.Freeze();
        }

        public string Name { get; }
        public string DisplayName { get; }
        public Brush PreviewBrush { get; }
    }

    public static class AppThemeManager
    {
        private static string currentMode = "Light";
        private static string currentAccent = "Blue";

        public static IReadOnlyList<string> ThemeModes { get; } = new[] { "Light", "Dark" };

        public static IReadOnlyList<ThemeAccentOption> ThemeAccents { get; } = new[]
        {
            new ThemeAccentOption("Blue", "蓝色", Color.FromRgb(0x00, 0x78, 0xD7)),
            new ThemeAccentOption("Red", "红色", Color.FromRgb(0xE5, 0x14, 0x00)),
            new ThemeAccentOption("Green", "绿色", Color.FromRgb(0x60, 0xA9, 0x17)),
            new ThemeAccentOption("Orange", "橙色", Color.FromRgb(0xFA, 0x68, 0x00)),
            new ThemeAccentOption("Purple", "紫色", Color.FromRgb(0x64, 0x71, 0xB9)),
            new ThemeAccentOption("Emerald", "翠绿", Color.FromRgb(0x00, 0x8A, 0x00)),
            new ThemeAccentOption("Teal", "青绿", Color.FromRgb(0x00, 0xAB, 0xA9)),
            new ThemeAccentOption("Cyan", "青色", Color.FromRgb(0x1B, 0xA1, 0xE2)),
            new ThemeAccentOption("Amber", "琥珀", Color.FromRgb(0xF0, 0xA3, 0x0A)),
        };

        public static bool IsDarkTheme => currentMode == "Dark";
        public static string CurrentMode => currentMode;
        public static string CurrentAccent => currentAccent;

        public static event EventHandler<bool> ThemeChanged;

        private static readonly Uri LightTokensUri = new Uri("pack://application:,,,/HalconVisionStudio.UI;component/Themes/Tokens/Colors.Light.xaml", UriKind.Absolute);
        private static readonly Uri DarkTokensUri = new Uri("pack://application:,,,/HalconVisionStudio.UI;component/Themes/Tokens/Colors.Dark.xaml", UriKind.Absolute);

        private static string ConfigFilePath => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HalconVisionStudio",
            "theme.json");

        public static void Initialize()
        {
            var (savedMode, savedAccent) = LoadSavedConfig();
            SetTheme(savedMode, savedAccent);
        }

        public static void SetTheme(bool isDark)
        {
            SetTheme(isDark ? "Dark" : "Light", currentAccent);
        }

        public static void SetTheme(string mode, string accent)
        {
            currentMode = string.Equals(mode, "Dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
            currentAccent = string.IsNullOrWhiteSpace(accent) ? "Blue" : accent;
            ApplyTheme();
            SaveConfig(currentMode, currentAccent);
            ThemeChanged?.Invoke(null, IsDarkTheme);
        }

        public static void ToggleTheme()
        {
            SetTheme(IsDarkTheme ? "Light" : "Dark", currentAccent);
        }

        private static void ApplyTheme()
        {
            var app = Application.Current;
            if (app == null) return;

            // 1. 切换 MahApps 原生主题与 Accent 色
            try
            {
                ThemeManager.Current.ChangeTheme(app, $"{currentMode}.{currentAccent}");
            }
            catch (Exception ex)
            {
                Diagnostics.ExecutionLogService.App($"MahApps 主题切换失败: {ex.Message}", Diagnostics.ExecLogLevel.Error, ex.ToString());
            }

            // 2. 动态替换机器视觉语义色彩令牌 (Colors.Light / Colors.Dark)。
            // 关键：嵌套在子字典里的槽位替换不会向已创建控件的 DynamicResource 传播失效
            // （实测：资源值已变但界面不动）。必须把令牌字典放在 Application 顶层，
            // 并用 Remove + Insert 的集合变更路径（ControlzEx 同款做法）触发全局刷新。
            try
            {
                var targetUri = IsDarkTheme ? DarkTokensUri : LightTokensUri;
                var target = new ResourceDictionary { Source = targetUri };

                var replaced = false;
                for (int i = 0; i < app.Resources.MergedDictionaries.Count; i++)
                {
                    var source = app.Resources.MergedDictionaries[i].Source?.OriginalString;
                    if (source != null &&
                        (source.Contains("Colors.Light.xaml") || source.Contains("Colors.Dark.xaml")))
                    {
                        app.Resources.MergedDictionaries.RemoveAt(i);
                        app.Resources.MergedDictionaries.Insert(i, target);
                        replaced = true;
                        break;
                    }
                }

                if (!replaced)
                {
                    app.Resources.MergedDictionaries.Add(target);
                }
            }
            catch (Exception ex)
            {
                Diagnostics.ExecutionLogService.App($"主题令牌切换失败: {ex.Message}", Diagnostics.ExecLogLevel.Error, ex.ToString());
            }
        }

        private static (string mode, string accent) LoadSavedConfig()
        {
            try
            {
                var path = ConfigFilePath;
                if (System.IO.File.Exists(path))
                {
                    var json = System.IO.File.ReadAllText(path);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    var mode = root.TryGetProperty("Mode", out var m) ? m.GetString() : "Light";
                    var accent = root.TryGetProperty("Accent", out var a) ? a.GetString() : "Blue";
                    return (mode ?? "Light", accent ?? "Blue");
                }
            }
            catch
            {
            }

            return ("Light", "Blue");
        }

        private static void SaveConfig(string mode, string accent)
        {
            try
            {
                var path = ConfigFilePath;
                var dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && !System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }

                var json = System.Text.Json.JsonSerializer.Serialize(new { Mode = mode, Accent = accent });
                System.IO.File.WriteAllText(path, json);
            }
            catch
            {
            }
        }
    }
}
