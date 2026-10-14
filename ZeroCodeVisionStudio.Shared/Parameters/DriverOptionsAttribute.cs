using System;
using ZeroCodeVisionStudio.Shared.Communication;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 标记字符串参数属性为"通讯驱动选择"编辑器：
    /// 参数对话框中渲染为下拉框，候选项为通讯驱动中心内指定类型的已配置驱动，
    /// 用户无需手填驱动名，也不可能选错类型。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class DriverOptionsAttribute : Attribute
    {
        /// <summary>允许选择的驱动类型（空数组 = 全部类型）。</summary>
        public CommDriverType[] AllowedTypes { get; }

        /// <summary>
        /// 初始化驱动选择特性。
        /// </summary>
        /// <param name="allowedTypes">允许的驱动类型列表，如 [DriverOptions(CommDriverType.TcpClient, CommDriverType.Serial)]。</param>
        public DriverOptionsAttribute(params CommDriverType[] allowedTypes)
        {
            AllowedTypes = allowedTypes ?? Array.Empty<CommDriverType>();
        }
    }
}
