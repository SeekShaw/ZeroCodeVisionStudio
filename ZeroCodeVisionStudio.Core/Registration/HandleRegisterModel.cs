using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Windows.Media;
using ZeroCodeVisionStudio.Shared;

namespace ZeroCodeVisionStudio.Core
{
    /// <summary>
    /// 算子注册信息实现类。
    /// 在构造时通过反射读取算子类上的 <see cref="HandleAttribute"/>，提取算子的元数据（名称、键名、分类、描述、图标）。
    /// 同时尝试实例化算子以获取其自定义 IconGeometry，以支持代码方式设置图标。
    /// </summary>
    public class HandleRegisterModel : IHandleRegisterModel
    {
        /// <summary>
        /// 初始化算子注册信息，从算子 CLR 类型上读取 <see cref="HandleAttribute"/> 并解析图标。
        /// </summary>
        /// <param name="handle">实现 <see cref="IHandle"/> 的算子 CLR 类型。</param>
        public HandleRegisterModel(Type handle)
        {
            Handle = handle;
            var attr = handle.GetCustomAttribute<HandleAttribute>();
            if (attr != null)
            {
                HandleDescription = attr.HandleDescription;
                HandleName = attr.HandleName;
                HandleType = attr.HandleType;
                Key = attr.Key;
                Backend = attr.OperatorBackend;
                IconGeometry = ResolveIconGeometry(handle, attr.IconData);
            }
            else
            {
                Backend = OperatorBackend.Unknown;
                IconGeometry = ResolveIconGeometry(handle, null);
            }
        }

        /// <summary>
        /// 获取默认图标的 Geometry 路径数据字符串（圆形）。
        /// 当算子未设置自定义图标时使用。
        /// </summary>
        private string GetDefaultIconData()
        {
            return "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z";
        }

        /// <summary>
        /// 解析算子图标：优先尝试实例化算子获取代码定义的图标，失败则解析 Attribute 中的路径字符串。
        /// </summary>
        /// <param name="handle">算子 CLR 类型。</param>
        /// <param name="attributeIconData">来自 <see cref="HandleAttribute.IconData"/> 的路径字符串，可为 null。</param>
        /// <returns>解析成功的 <see cref="Geometry"/> 对象。</returns>
        private Geometry ResolveIconGeometry(Type handle, string attributeIconData)
        {
            var handleIcon = TryCreateHandleIcon(handle);
            if (handleIcon != null)
            {
                return handleIcon;
            }

            return Geometry.Parse(attributeIconData ?? GetDefaultIconData());
        }

        /// <summary>
        /// 尝试实例化算子并调用 <see cref="IHandle.IconGeometry"/> 获取自定义图标。
        /// 若算子构造需要真实 IDialogService（无法以 null 构造），则返回 null。
        /// </summary>
        /// <param name="handle">算子 CLR 类型。</param>
        /// <returns>算子自定义图标，若无法获取则返回 null。</returns>
        private Geometry TryCreateHandleIcon(Type handle)
        {
            try
            {
                if (typeof(IHandle).IsAssignableFrom(handle) &&
                    Activator.CreateInstance(handle, new object[] { null }) is IHandle instance)
                {
                    var icon = instance.IconGeometry;
                    instance.Dispose();
                    return icon;
                }
            }
            catch
            {
                // 部分算子构造函数需要真实的 IDialogService，此处可安全忽略异常
            }

            return null;
        }

        /// <summary>返回算子显示名称，方便调试和 ListBox ItemTemplate 的默认文本。</summary>
        public override string ToString()
        {
            return HandleName;
        }

        /// <summary>算子对应的 CLR 类型（实现 <see cref="IHandle"/>）。</summary>
        public Type Handle { get; private set; }

        /// <summary>算子的详细描述文本，显示在工具提示中。</summary>
        public string HandleDescription { get; private set; }

        /// <summary>算子的显示名称，用于工具箱列表和节点卡片标题。</summary>
        public string HandleName { get; private set; }

        /// <summary>算子分类名称，用于工具箱按类别分组（见 <see cref="Common"/> 中的 HandleType_* 常量）。</summary>
        public string HandleType { get; private set; }

        /// <summary>算子唯一注册键名，用于注册中心查找和方案序列化/反序列化。</summary>
        public string Key { get; private set; }

        /// <summary>算子图标的 Geometry 对象，用于在工具箱和节点卡片中渲染图标。</summary>
        public Geometry IconGeometry { get; private set; }

        /// <summary>算子后端实现分类（OpenCV / Halcon / Hybrid / Unknown）。</summary>
        public OperatorBackend Backend { get; private set; }
    }
}
