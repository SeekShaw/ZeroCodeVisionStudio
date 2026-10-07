using System.Collections.Generic;

namespace HalconVisionStudio.Shared
{
    /// <summary>
    /// 数据槽接口。
    /// 实现此接口的算子可以显式声明输入/输出数据槽，替代默认的单输入/单输出模式。
    /// 框架在构建输入绑定列表时，会优先使用 <see cref="InputSlots"/> 而不是默认的"输入图像/区域"槽。
    /// </summary>
    public interface IHandleDataSlots
    {
        /// <summary>
        /// 算子的输入数据槽列表。
        /// 每个槽定义了一个命名输入，包含键名、显示名称和期望的数据类型。
        /// </summary>
        IReadOnlyList<RunSlot> InputSlots { get; }

        /// <summary>
        /// 算子的输出数据槽列表。
        /// 每个槽定义了一个命名输出，用于在下游绑定选择器中展示可用的输出项。
        /// </summary>
        IReadOnlyList<RunSlot> OutputSlots { get; }
    }
}
