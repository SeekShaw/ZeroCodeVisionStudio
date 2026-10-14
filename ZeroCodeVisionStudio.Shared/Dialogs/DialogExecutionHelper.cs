using System;
using System.Collections.Generic;
using System.Diagnostics;
using ZeroCodeVisionStudio.Shared.Diagnostics;
using System.Linq;

namespace ZeroCodeVisionStudio.Shared
{
    public static class DialogExecutionHelper
    {
        public static string TryExecuteUpstreamThenSelf(IHandle handle)
        {
            if (handle == null)
            {
                return "Handle is null and cannot be executed.";
            }

            TryExecuteChain(handle, new HashSet<IHandle>(), out var message);
            return message;
        }

        private static bool TryExecuteChain(IHandle handle, HashSet<IHandle> visited, out string message)
        {
            if (handle == null)
            {
                message = "Handle is null.";
                return false;
            }

            if (!visited.Add(handle))
            {
                message = "执行完成（检测到循环依赖，已跳过）。";
                return true;
            }

            var runModel = handle.ContextRunModel;
            if (runModel?.InputBindings == null || runModel.InputBindings.Count == 0)
            {
                return ExecuteSelf(handle, out message);
            }

            var upstreamNodes = runModel.InputBindings
                .Where(b => b.SourceNode != null)
                .Select(b => b.SourceNode)
                .Distinct()
                .ToList();

            if (upstreamNodes.Count == 0)
            {
                return ExecuteSelf(handle, out message);
            }

            foreach (var upstream in upstreamNodes)
            {
                var upstreamHandle = upstream.Handle;
                if (upstreamHandle == null)
                {
                    continue;
                }

                var param = upstreamHandle.RunParameter;
                if (HasSignificantData(param))
                {
                    continue;
                }

                if (!TryExecuteChain(upstreamHandle, visited, out var upstreamMsg))
                {
                    message = $"上游节点执行失败：{upstreamMsg}";
                    return false;
                }
            }

            ApplyBindings(handle);
            return ExecuteSelf(handle, out message);
        }

        private static bool ExecuteSelf(IHandle handle, out string message)
        {
            // 兜底：节点从未在流程中执行过时 RunParameter 可能为空（SetRunParameter(null) 的历史路径），
            // 预览执行前补一个全新实例，避免算子在 Handle() 内空引用。
            handle?.SetRunParameter(handle.RunParameter ?? new RunParameter());

            var stopwatch = Stopwatch.StartNew();
            SetLastElapsed(handle, TimeSpan.Zero);
            try
            {
                handle.Execute();
                stopwatch.Stop();
                SetLastElapsed(handle, stopwatch.Elapsed);

                var softFailed = handle.RunParameter?.Success == false;
                handle.RunParameter?.SetOutput(RunOutputKeys.ExecutionSuccess, !softFailed);
                handle.RunParameter?.SetOutput(RunOutputKeys.ExecutionStatus, softFailed ? "失败" : "成功");
                var elapsedMessage = $"{(softFailed ? "执行失败（业务）" : "执行完成")}，耗时：{ExecutionTimeFormatter.Format(stopwatch.Elapsed)}。";
                message = string.IsNullOrWhiteSpace(handle.RunParameter?.ResultMessage)
                    ? elapsedMessage
                    : $"{elapsedMessage}{Environment.NewLine}{handle.RunParameter.ResultMessage}";
                return true;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                SetLastElapsed(handle, stopwatch.Elapsed);

                var nodeTitle = handle?.ContextRunModel?.DisplayName ?? handle?.GetType().Name ?? "未知节点";
                ExecutionLogService.Error(ExecLogCategory.Node, nodeTitle, $"执行失败：{ExceptionFormatter.Brief(ex)}", ex);
                handle?.RunParameter?.SetOutput(RunOutputKeys.ExecutionSuccess, false);
                handle?.RunParameter?.SetOutput(RunOutputKeys.ExecutionStatus, "失败");
                message = $"节点 [{nodeTitle}] 执行失败，耗时：{ExecutionTimeFormatter.Format(stopwatch.Elapsed)}。{Environment.NewLine}{ExceptionFormatter.Describe(ex)}";
                return false;
            }
        }

        private static void SetLastElapsed(IHandle handle, TimeSpan elapsed)
        {
            if (handle?.ContextRunModel != null)
            {
                handle.ContextRunModel.LastElapsed = elapsed;
            }
        }

        private static void ApplyBindings(IHandle handle)
        {
            var runModel = handle.ContextRunModel;
            if (runModel == null)
            {
                return;
            }

            var parameter = new RunParameter();
            handle.SetRunParameter(parameter);
            var clonedValues = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);

            foreach (var binding in runModel.InputBindings)
            {
                var sourceParam = binding.SourceNode?.Handle?.RunParameter;
                if (sourceParam == null || string.IsNullOrWhiteSpace(binding.SourceOutputKey))
                {
                    continue;
                }

                var value = GetSlotValue(sourceParam, binding.SourceOutputKey);
                if (value == null)
                {
                    continue;
                }

                if (binding.IsParameterBinding)
                {
                    SetParameterProperty(handle.HandleParameter, binding.TargetParameterName, value);
                }
                else
                {
                    SetSlotValue(parameter, binding.TargetInputKey, value, clonedValues);
                }
            }
        }

        private static object GetSlotValue(RunParameter parameter, string slotKey)
        {
            if (parameter == null || string.IsNullOrWhiteSpace(slotKey))
            {
                return null;
            }

            return slotKey switch
            {
                RunOutputKeys.Output => parameter.HOutput ?? parameter.GetOutput<object>(RunOutputKeys.Output),
                RunOutputKeys.RawImage => parameter.RawImage ?? parameter.GetOutput<object>(RunOutputKeys.RawImage),
                RunOutputKeys.GrayImage => parameter.GrayImage ?? parameter.GetOutput<object>(RunOutputKeys.GrayImage),
                RunOutputKeys.Region => parameter.GetOutput<object>(RunOutputKeys.Region) ?? parameter.HOutput,
                RunOutputKeys.Contour => parameter.GetOutput<object>(RunOutputKeys.Contour) ?? parameter.HOutput,
                RunOutputKeys.Xld => parameter.GetOutput<object>(RunOutputKeys.Xld) ?? parameter.HOutput,
                RunOutputKeys.ResultMessage => parameter.ResultMessage,
                _ => parameter.GetOutput<object>(slotKey)
            };
        }

        private static void SetSlotValue(RunParameter parameter, string slotKey, object value, Dictionary<object, object> clonedValues)
        {
            if (value == null)
            {
                return;
            }

            value = CloneSlotValue(value, clonedValues);
            parameter.SetInput(slotKey, value);
            parameter.SetOutput(slotKey, value);

            switch (slotKey)
            {
                case RunOutputKeys.Output:
                    parameter.HOutput = value as HalconDotNet.HObject;
                    break;
                case RunOutputKeys.RawImage:
                    parameter.RawImage = value as HalconDotNet.HObject;
                    parameter.HOutput ??= value as HalconDotNet.HObject;
                    break;
                case RunOutputKeys.GrayImage:
                    parameter.GrayImage = value as HalconDotNet.HObject;
                    parameter.HOutput ??= value as HalconDotNet.HObject;
                    break;
                case RunOutputKeys.Region:
                    parameter.HOutput ??= value as HalconDotNet.HObject;
                    break;
                case RunOutputKeys.Xld:
                case RunOutputKeys.Contour:
                    parameter.HOutput ??= value as HalconDotNet.HObject;
                    break;
            }
        }

        private static object CloneSlotValue(object value, Dictionary<object, object> clonedValues)
        {
            if (value == null)
            {
                return null;
            }

            if (clonedValues != null && clonedValues.TryGetValue(value, out var clonedValue))
            {
                return clonedValue;
            }

            return value switch
            {
                HalconDotNet.HObject hObject => CacheClone(clonedValues, value, hObject.Clone()),
                HalconDotNet.HTuple tuple => CacheClone(clonedValues, value, tuple.Clone()),
                _ => value
            };
        }

        private static object CacheClone(Dictionary<object, object> clonedValues, object source, object clone)
        {
            clonedValues?.Add(source, clone);
            return clone;
        }

        private static void SetParameterProperty(IHandleParameter param, string propName, object value)
        {
            if (param == null || string.IsNullOrWhiteSpace(propName) || value == null)
            {
                return;
            }

            var prop = param.GetType().GetProperty(propName);
            if (prop == null || !prop.CanWrite)
            {
                return;
            }

            try
            {
                var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                if (value is HalconDotNet.HTuple tuple && tuple.Length > 0)
                {
                    value = tuple[0].O;
                }

                if (targetType.IsInstanceOfType(value))
                {
                    prop.SetValue(param, value);
                }
                else if (targetType.IsEnum && value is string enumValue)
                {
                    prop.SetValue(param, Enum.Parse(targetType, enumValue));
                }
                else
                {
                    prop.SetValue(param, Convert.ChangeType(value, targetType));
                }
            }
            catch
            {
            }
        }

        private static bool HasSignificantData(RunParameter parameter)
        {
            return parameter != null &&
                (parameter.HOutput != null ||
                 parameter.RawImage != null ||
                 parameter.GrayImage != null ||
                 parameter.Outputs.Count > 0);
        }
    }
}
