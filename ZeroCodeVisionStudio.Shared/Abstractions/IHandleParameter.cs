using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 算子参数接口。所有可配置的算子参数类均需实现此接口。
    /// 框架通过此接口在方案序列化/反序列化时对参数进行克隆和重置。
    /// </summary>
    public interface IHandleParameter
    {
        /// <summary>
        /// 将参数恢复到默认初始状态。
        /// 通常在流程重置或参数对话框"重置"操作时调用。
        /// </summary>
        void Reset();

        /// <summary>
        /// 创建当前参数对象的深拷贝。
        /// 框架在向下游节点传递 RunParameter 时会调用此方法，确保节点间参数相互隔离。
        /// </summary>
        /// <returns>深拷贝后的参数实例。</returns>
        IHandleParameter Clone();
    }
}
