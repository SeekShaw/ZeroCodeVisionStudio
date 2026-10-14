using System;
using System.Linq;
using ZeroCodeVisionStudio.Core;
using ZeroCodeVisionStudio.Shared;
using Prism.Ioc;
using Prism.Modularity;

namespace Plugin.Handle.Logic
{
    /// <summary>
    /// 流程逻辑算子模块：逻辑运算 / 计数器 / 延时等待 / 变量存取 / 条件分流 / 边沿触发 / 抽检判定。
    /// 自动扫描并注册本程序集中所有算子。
    /// </summary>
    public class LogicModule : IModule
    {
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            var center = containerProvider.Resolve<IHandleControl>();

            var handleTypes = GetType().Assembly.GetTypes()
                .Where(t => typeof(IHandle).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                .OrderBy(t => t.Name);

            foreach (var type in handleTypes)
            {
                try
                {
                    var model = new HandleRegisterModel(type);
                    if (!string.IsNullOrWhiteSpace(model.Key))
                    {
                        center.Register(model.Key, model);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"注册算子 {type.FullName} 失败: {ex.Message}");
                }
            }
        }
    }
}
