using System;
using System.Linq;
using System.Reflection;
using HalconVisionStudio.Core;
using HalconVisionStudio.Shared;
using Prism.Ioc;
using Prism.Modularity;

namespace Plugin.Handle.Measurement
{
    /// <summary>
    /// Measurement 算子分类模块。
    /// 自动扫描并注册本程序集中包含的所有算子，并配置所需对话框。
    /// </summary>
    public class MeasurementModule : IModule
    {
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterDialog<Plugin.Handle.AngleMeasure.Views.AngleLLView, Plugin.Handle.AngleMeasure.ViewModels.AngleLLViewModel>("AngleLLView");
            containerRegistry.RegisterDialog<Plugin.Handle.AngleMeasure.Views.AngleLXView, Plugin.Handle.AngleMeasure.ViewModels.AngleLXViewModel>("AngleLXView");
            containerRegistry.RegisterDialog<Plugin.Handle.CompareCondition.Views.CompareConditionView, Plugin.Handle.CompareCondition.ViewModels.CompareConditionViewModel>("CompareConditionView");
            containerRegistry.RegisterDialog<Plugin.Handle.ContourMeasure.Views.ContourMeasureView, Plugin.Handle.ContourMeasure.ViewModels.ContourMeasureViewModel>("ContourMeasureView");
            containerRegistry.RegisterDialog<Plugin.Handle.DefectMeasure.Views.DefectMeasureView, Plugin.Handle.DefectMeasure.ViewModels.DefectMeasureViewModel>("DefectMeasureView");
            containerRegistry.RegisterDialog<Plugin.Handle.DistanceMeasure.Views.DistancePPView, Plugin.Handle.DistanceMeasure.ViewModels.DistancePPViewModel>("DistancePPView");
            containerRegistry.RegisterDialog<Plugin.Handle.DistanceMeasure.Views.DistancePLView, Plugin.Handle.DistanceMeasure.ViewModels.DistancePLViewModel>("DistancePLView");
            containerRegistry.RegisterDialog<Plugin.Handle.DistanceMeasure.Views.DistanceContoursView, Plugin.Handle.DistanceMeasure.ViewModels.DistanceContoursViewModel>("DistanceContoursView");
            containerRegistry.RegisterDialog<Plugin.Handle.ShapeFeature.Views.ShapeFeatureView, Plugin.Handle.ShapeFeature.ViewModels.ShapeFeatureViewModel>("ShapeFeatureView");
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
