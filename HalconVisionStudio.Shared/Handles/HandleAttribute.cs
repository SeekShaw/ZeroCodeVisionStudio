using System;

namespace HalconVisionStudio.Shared
{
    /// <summary>
    /// 算子特性标记，用于声明算子插件的元数据。
    /// 插件程序集加载时通过反射读取此特性，自动将算子注册到控件中心与工具箱。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class HandleAttribute : Attribute
    {
        /// <summary>
        /// 初始化算子特性，指定算子的注册元数据。
        /// </summary>
        /// <param name="handleName">算子显示名称（工具箱和节点卡片标题）。</param>
        /// <param name="key">算子唯一标识键，用于注册与方案序列化/反序列化索引。</param>
        /// <param name="handleDescription">算子详细功能描述，在 ToolTip 中展示。</param>
        /// <param name="handleType">算子分类名称，取自 <see cref="Common"/> 中的 HandleType_* 常量。</param>
        public HandleAttribute(string handleName, string key, string handleDescription, string handleType)
        {
            HandleName = handleName;
            Key = key;
            HandleDescription = handleDescription;
            HandleType = handleType;
            // 默认图标为圆环矢量路径
            IconData = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z";
        }

        /// <summary>算子分类名称，对应 <see cref="Common"/> 中的 HandleType_* 常量。</summary>
        public string HandleType { get; }

        /// <summary>算子唯一标识键，全局唯一，用于插件注册和方案反序列化匹配。</summary>
        public string Key { get; set; }

        /// <summary>算子显示名称（工具箱列表与画布节点卡片默认标题）。</summary>
        public string HandleName { get; set; }

        /// <summary>算子详细功能描述，用于鼠标悬浮提示 ToolTip。</summary>
        public string HandleDescription { get; set; }

        /// <summary>
        /// 矢量图标 Geometry 路径字符串（SVG Path 格式）。
        /// 若算子未单独定义几何图标，则使用此字段解析为矢量几何图形，默认为圆形图标。
        /// </summary>
        public string IconData { get; set; }
    }
}
