using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 算子注册信息接口。
    /// 描述一个算子类型的元数据：唯一键名、显示名称、分类、说明和图标。
    /// 框架通过此接口填充工具箱列表，以及在 ControlCenter 中查找算子类型。
    /// </summary>
    public interface IHandleRegisterModel
    {
        /// <summary>算子对应的 CLR 类型（实现 <see cref="IHandle"/> 的具体类）。</summary>
        Type Handle { get; }

        /// <summary>算子的详细描述，显示在工具箱的提示信息中。</summary>
        string HandleDescription { get; }

        /// <summary>算子显示名称，显示在工具箱列表和流程节点标题中。</summary>
        string HandleName { get; }

        /// <summary>
        /// 算子分类，用于工具箱按类别分组。
        /// 取值参见 <see cref="Common"/> 中的 HandleType_* 常量。
        /// </summary>
        string HandleType { get; }

        /// <summary>
        /// 算子的唯一注册键名。
        /// 对应 <see cref="HandleAttribute.Key"/>，同时用于方案文件的序列化与反序列化。
        /// </summary>
        string Key { get; }

        /// <summary>算子图标的 Geometry 路径数据，用于在工具箱和节点卡片上渲染图标。</summary>
        Geometry IconGeometry { get; }

        /// <summary>
        /// 算子后端实现分类（OpenCV / Halcon / Hybrid / Unknown）。
        /// 用于工具箱分组显示和运行时依赖检查。
        /// </summary>
        OperatorBackend Backend { get; }
    }
}
