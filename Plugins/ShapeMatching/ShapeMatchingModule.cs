using System;
using System.Linq;
using System.Reflection;
using ZeroCodeVisionStudio.Core;
using ZeroCodeVisionStudio.Shared;
using Prism.Ioc;
using Prism.Modularity;

namespace Plugin.Handle.ShapeMatching
{
    /// <summary>
    /// ShapeMatching 算子分类模块。
    /// 自动扫描并注册本程序集中包含的所有算子，并配置所需对话框。
    /// </summary>
    public class ShapeMatchingModule : IModule
    {
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterDialog<Plugin.Handle.CreateShapeModel.Views.CreateShapeModelView, Plugin.Handle.CreateShapeModel.ViewModels.CreateShapeModelViewModel>("CreateShapeModelView");
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            var center = containerProvider.Resolve<IHandleControl>();

            // 自动扫描当前程序集中所有带有 HandleAttribute 的算子进行注册
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
