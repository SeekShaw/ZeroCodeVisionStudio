using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows.Data;
using ZeroCodeVisionStudio.Shared;

namespace ZeroCodeVisionStudio.Core
{
    /// <summary>
    /// 算子注册中心，负责管理应用中所有已注册算子类型的字典，并向工具箱 UI 提供可绑定视图。
    /// 框架在启动时通过 Prism 模块加载机制，自动将插件中的所有算子注册到此中心。
    /// </summary>
    public class ControlCenter : IHandleControl
    {
        /// <summary>键名 -> 注册信息的内部字典，保存所有已注册算子的元数据。</summary>
        private Dictionary<string, IHandleRegisterModel> handles = new Dictionary<string, IHandleRegisterModel>();

        /// <summary>handles.Values 的可绑定 ICollectionView，供工具箱 ListBox 分组展示。</summary>
        private ICollectionView handleView = null;

        /// <summary>
        /// 初始化注册中心，创建基于 handles.Values 的集合视图，供工具箱 UI 绑定。
        /// </summary>
        public ControlCenter()
        {
            handleView = CollectionViewSource.GetDefaultView(handles.Values);
        }

        /// <summary>
        /// 所有已注册算子的可绑定集合视图。
        /// 工具箱 UI 按 HandleType 对此视图分组，展示算子列表。
        /// </summary>
        public ICollectionView HandleView => handleView;

        /// <summary>
        /// 根据键名获取算子注册信息。
        /// </summary>
        /// <param name="key">算子唯一键名。</param>
        /// <returns>对应的 <see cref="IHandleRegisterModel"/> 实例。</returns>
        /// <exception cref="KeyNotFoundException">键名不存在时抛出。</exception>
        public IHandleRegisterModel GetHandle(string key)
        {
            return handles[key];
        }

        /// <summary>
        /// 判断指定键名的算子是否已被注册。
        /// </summary>
        /// <param name="key">算子唯一键名。</param>
        /// <returns>已注册返回 true，否则返回 false。</returns>
        public bool IsRegister(string key)
        {
            return handles.ContainsKey(key);
        }

        /// <summary>
        /// 注册一个算子类型到注册中心。
        /// 每个键名只能注册一次；重复注册会抛出 <see cref="HandleException"/>。
        /// 注册成功后会刷新 HandleView 使工具箱 UI 更新。
        /// </summary>
        /// <param name="key">算子唯一键名，不可为 null 或空字符串。</param>
        /// <param name="handle">算子注册信息对象。</param>
        /// <exception cref="HandleException">
        /// 若 key 已被注册，抛出类型为 <see cref="ExceptionType.E_HANDLE_ALREADY_REGISTERED"/> 的异常。
        /// </exception>
        public void Register(string key, IHandleRegisterModel handle)
        {
            if (string.IsNullOrEmpty(key))
                return;
            if (handles.ContainsKey(key))
            {
                throw new HandleException(ExceptionType.E_HANDLE_ALREADY_REGISTERED);
            }
            else
            {
                handles.Add(key, handle);
            }
            HandleView.Refresh();
        }

        /// <summary>
        /// 取消注册指定键名的算子。
        /// 若键名不存在则静默忽略。
        /// </summary>
        /// <param name="key">算子唯一键名。</param>
        public void Unregister(string key)
        {
            handles.Remove(key);
        }
    }
}
