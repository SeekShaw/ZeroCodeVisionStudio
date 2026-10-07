using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace HalconVisionStudio.Shared
{
    /// <summary>
    /// 算子注册中心接口。
    /// 负责管理所有可用算子类型的注册信息，并向工具箱 UI 提供可绑定的视图数据源。
    /// </summary>
    public interface IHandleControl
    {
        /// <summary>
        /// 注册一个算子类型。
        /// </summary>
        /// <param name="key">算子的唯一键名，通常与 <see cref="HandleAttribute.Key"/> 一致。</param>
        /// <param name="handleType">算子注册信息对象，包含类型、名称、描述和图标等元数据。</param>
        /// <exception cref="HandleException">若 key 已被注册，则抛出 <see cref="ExceptionType.E_HANDLE_ALREADY_REGISTERED"/> 异常。</exception>
        void Register(string key, IHandleRegisterModel handleType);

        /// <summary>
        /// 判断指定键名的算子是否已被注册。
        /// </summary>
        /// <param name="key">算子唯一键名。</param>
        /// <returns>已注册返回 true，否则返回 false。</returns>
        bool IsRegister(string key);

        /// <summary>
        /// 取消注册指定键名的算子。
        /// </summary>
        /// <param name="key">算子唯一键名。</param>
        void Unregister(string key);

        /// <summary>
        /// 根据键名获取算子注册信息。
        /// </summary>
        /// <param name="key">算子唯一键名。</param>
        /// <returns>对应的 <see cref="IHandleRegisterModel"/> 实例。</returns>
        IHandleRegisterModel GetHandle(string key);

        /// <summary>
        /// 所有已注册算子的可绑定集合视图，供工具箱 UI 分组、过滤和展示。
        /// </summary>
        ICollectionView HandleView { get; }
    }
}
