using System.IO;
using System.Reflection;
using Prism.Modularity;

namespace ZeroCodeVisionStudio.Bootstrap
{
    /// <summary>
    /// 插件模块注册器——自动扫描应用程序目录并注册所有符合命名规范的 Prism 模块。
    /// <para>
    /// 约定：插件程序集文件名必须匹配 <c>Plugin.Handle.*.dll</c> 模式，
    /// 且程序集中至少有一个具体类（非抽象）实现了 <see cref="IModule"/> 接口。
    /// </para>
    /// <remarks>
    /// 扫描顺序为程序集名称 → 类型全名，保证模块注册顺序的稳定性（避免因文件系统枚举顺序不同导致差异）。
    /// 若程序集加载失败（如缺少依赖），该程序集将被跳过，不会阻止其他模块的注册。
    /// </remarks>
    /// </summary>
    internal static class PluginModuleRegistry
    {
        /// <summary>插件程序集文件名通配符，仅扫描符合此模式的 DLL 文件。</summary>
        private const string PluginAssemblyPattern = "Plugin.Handle.*.dll";

        /// <summary>
        /// 扫描并注册所有插件模块到 Prism 的模块目录，同时通过回调报告注册进度。
        /// <para>
        /// 进度值范围为 0.2 ~ 0.9，按已注册模块数 / 总模块数线性插值计算。
        /// </para>
        /// </summary>
        /// <param name="moduleCatalog">Prism 模块目录，用于接收模块类型注册。</param>
        /// <param name="reportProgress">可选的进度回调，参数为（进度 0~1，状态描述文本）。</param>
        public static void RegisterModules(IModuleCatalog moduleCatalog, Action<double, string> reportProgress = null)
        {
            var moduleTypes = DiscoverModuleTypes().ToArray();
            if (moduleTypes.Length == 0)
            {
                reportProgress?.Invoke(0.9, "No plugin modules found.");
                return;
            }

            for (var index = 0; index < moduleTypes.Length; index++)
            {
                var moduleType = moduleTypes[index];
                reportProgress?.Invoke(
                    0.2 + 0.7 * (index + 1) / moduleTypes.Length,
                    $"Registering {moduleType.Name}...");
                moduleCatalog.AddModule(moduleType);
            }
        }

        /// <summary>
        /// 在应用程序基目录中发现所有符合插件命名约定的模块类型。
        /// 结果按程序集名称、类型全名排序，保证注册顺序的稳定性。
        /// </summary>
        /// <returns>所有发现的具体 <see cref="IModule"/> 实现类型的枚举序列。</returns>
        private static IEnumerable<Type> DiscoverModuleTypes()
        {
            var baseDirectory = AppContext.BaseDirectory;
            return Directory
                .EnumerateFiles(baseDirectory, PluginAssemblyPattern, SearchOption.TopDirectoryOnly)
                .SelectMany(LoadModuleTypes)
                .OrderBy(type => type.Assembly.GetName().Name)
                .ThenBy(type => type.FullName);
        }

        /// <summary>
        /// 加载指定程序集并从中提取所有 <see cref="IModule"/> 的具体实现类型。
        /// <para>
        /// 若程序集加载失败（如缺少依赖），通过 <c>yield break</c> 静默跳过。
        /// 若 <see cref="Assembly.GetExportedTypes"/> 抛出 <see cref="ReflectionTypeLoadException"/>，
        /// 则取出已能加载的类型继续处理。
        /// </para>
        /// </summary>
        /// <param name="assemblyPath">程序集文件的绝对路径。</param>
        /// <returns>程序集中所有可用的 <see cref="IModule"/> 具体实现类型。</returns>
        private static IEnumerable<Type> LoadModuleTypes(string assemblyPath)
        {
            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFrom(assemblyPath);
            }
            catch
            {
                yield break;
            }

            Type[] exportedTypes;
            try
            {
                exportedTypes = assembly.GetExportedTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                exportedTypes = ex.Types.Where(type => type != null).ToArray();
            }

            foreach (var type in exportedTypes)
            {
                if (type is { IsAbstract: false, IsClass: true } &&
                    typeof(IModule).IsAssignableFrom(type))
                {
                    yield return type;
                }
            }
        }
    }
}
