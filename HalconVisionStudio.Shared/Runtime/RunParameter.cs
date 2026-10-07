using HalconDotNet;
using System.Collections.Generic;
using System.Linq;

namespace HalconVisionStudio.Shared
{
    /// <summary>
    /// 算子运行参数类。
    /// 作为流程节点间的数据载体，在上下游节点之间传递图像、区域及各类命名数据。
    /// <para>
    /// 数据组织方式：
    /// <list type="bullet">
    ///   <item><description><see cref="HOutput"/>：主要输出，串联节点时默认传递的 HObject（图像或区域）。</description></item>
    ///   <item><description><see cref="RawImage"/>：相机采集的原始彩色图像。</description></item>
    ///   <item><description><see cref="GrayImage"/>：灰度图像。</description></item>
    ///   <item><description><see cref="Outputs"/>：命名输出字典，新增算子可通过键值对输出自定义数据，无需修改此类。</description></item>
    ///   <item><description><see cref="Inputs"/>：命名输入字典，用于接收上游通过绑定传递的特定数据。</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public class RunParameter : IHandleParameter
    {
        private HObject hOutput;
        private HObject grayImage;
        private HObject rawImage;

        /// <summary>
        /// 从上游共享而来的资源引用集合（<see cref="CloneShared"/> / MergeFrom(deepClone:false) 填充）。
        /// 集合内的 HObject/HTuple 生命周期由上游节点管理，本实例在 Reset 或字段替换时不会释放它们；
        /// 本实例自己新生成的输出资源则正常释放，避免共享模式下的内存泄漏。
        /// </summary>
        private HashSet<object> sharedResources;

        private bool IsSharedResource(object value) =>
            sharedResources != null && sharedResources.Contains(value);

        private void RegisterSharedResource(object value)
        {
            if (value is HObject || value is HTuple)
            {
                sharedResources ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
                sharedResources.Add(value);
            }
        }

        /// <summary>
        /// 主要 HObject 输出，流程节点间串联传递的核心图像/区域/XLD 数据。
        /// 下游节点在执行前会优先从此属性获取输入图像。
        /// </summary>
        public HObject HOutput
        {
            get => hOutput;
            set => SetHObjectField(ref hOutput, value);
        }

        /// <summary>灰度图像，一般由 RGB 转灰度等预处理算子写入。</summary>
        public HObject GrayImage
        {
            get => grayImage;
            set => SetHObjectField(ref grayImage, value);
        }

        /// <summary>相机采集的原始（彩色）图像，一般由图像输入类算子写入。</summary>
        public HObject RawImage
        {
            get => rawImage;
            set => SetHObjectField(ref rawImage, value);
        }

        /// <summary>
        /// 命名输出字典。
        /// 新算子可通过 <see cref="SetOutput"/> 写入任意类型的输出数据，
        /// 下游节点通过 <see cref="TryGetOutput{TValue}"/> 或 <see cref="GetOutput{TValue}"/> 按键名读取。
        /// </summary>
        public Dictionary<string, object> Outputs { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 命名输入字典。
        /// 通过输入绑定从上游节点接收到的指定数据存放在此处，
        /// 算子执行时从此字典读取特定输入项。
        /// </summary>
        public Dictionary<string, object> Inputs { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 结果消息，显示在状态栏和执行记录中。
        /// 可用于输出检测结果摘要、测量数值等简短文本信息。
        /// </summary>
        public string ResultMessage { get; set; }

        /// <summary>
        /// 业务执行结果（软失败标记）。
        /// null 或 true = 执行成功；false = 算子正常返回但业务失败（如通讯发送失败、接收超时）。
        /// 与抛异常的硬失败不同：软失败不中断流程，仅节点状态与执行记录标记为失败，
        /// 下游节点仍可继续执行并按输出键（如 communication.success）分支处理。
        /// </summary>
        public bool? Success { get; set; }

        /// <summary>
        /// 设置或更新命名输出项。
        /// 若 key 为空则忽略；若 value 为 null 则移除对应的输出项。
        /// </summary>
        /// <param name="key">输出项键名，建议使用 <see cref="RunOutputKeys"/> 中的常量。</param>
        /// <param name="value">输出值；传入 null 时删除该键。</param>
        public void SetOutput(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            Outputs.TryGetValue(key, out var oldValue);
            if (ReferenceEquals(oldValue, value))
            {
                return;
            }

            if (value == null)
            {
                Outputs.Remove(key);
                DisposeDetachedResource(oldValue);
                return;
            }

            Outputs[key] = value;
            DisposeDetachedResource(oldValue);
        }

        /// <summary>
        /// 主输入约定：把绑定写入 output 键的主图像（HObject）同步到三个主输入字段
        /// （HOutput / RawImage / GrayImage）——算子守卫按多种顺序读取主图像，统一回填保证兼容。
        /// 引用同时登记进共享集合：本实例 Reset 时不会释放上游持有的图像。
        /// </summary>
        public void AdoptMainInput(HObject mainImage)
        {
            if (mainImage == null || !mainImage.IsInitialized()) return;
            hOutput ??= mainImage;
            rawImage ??= mainImage;
            grayImage ??= mainImage;
            RegisterSharedResource(hOutput);
            RegisterSharedResource(rawImage);
            RegisterSharedResource(grayImage);
        }

        /// <summary>
        /// 设置或更新命名输入项。
        /// 若 key 为空则忽略；若 value 为 null 则移除对应的输入项。
        /// </summary>
        /// <param name="key">输入项键名。</param>
        /// <param name="value">输入值；传入 null 时删除该键。</param>
        public void SetInput(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            Inputs.TryGetValue(key, out var oldValue);
            if (ReferenceEquals(oldValue, value))
            {
                return;
            }

            if (value == null)
            {
                Inputs.Remove(key);
                DisposeDetachedResource(oldValue);
                return;
            }

            Inputs[key] = value;
            DisposeDetachedResource(oldValue);
        }

        /// <summary>
        /// 尝试从 <see cref="Inputs"/> 中按键名获取强类型输入值。
        /// </summary>
        /// <typeparam name="TValue">期望的值类型。</typeparam>
        /// <param name="key">输入项键名。</param>
        /// <param name="value">成功时返回对应值，失败时返回类型默认值。</param>
        /// <returns>键名存在且类型匹配时返回 true。</returns>
        public bool TryGetInput<TValue>(string key, out TValue value)
        {
            if (Inputs.TryGetValue(key, out var rawValue) && rawValue is TValue typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// 尝试从 <see cref="Outputs"/> 中按键名获取强类型输出值。
        /// </summary>
        /// <typeparam name="TValue">期望的值类型。</typeparam>
        /// <param name="key">输出项键名。</param>
        /// <param name="value">成功时返回对应值，失败时返回类型默认值。</param>
        /// <returns>键名存在且类型匹配时返回 true。</returns>
        public bool TryGetOutput<TValue>(string key, out TValue value)
        {
            if (Outputs.TryGetValue(key, out var rawValue) && rawValue is TValue typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// 从 <see cref="Outputs"/> 中按键名获取强类型输出值，键不存在时返回指定默认值。
        /// </summary>
        /// <typeparam name="TValue">期望的值类型。</typeparam>
        /// <param name="key">输出项键名。</param>
        /// <param name="fallback">键不存在或类型不匹配时返回的默认值。</param>
        /// <returns>对应的输出值，或 fallback。</returns>
        public TValue GetOutput<TValue>(string key, TValue fallback = default)
        {
            return TryGetOutput<TValue>(key, out var value) ? value : fallback;
        }

        /// <summary>
        /// 将 source 的数据合并到当前实例。
        /// 用于多个上游节点向同一下游节点传递数据时的参数合并场景。
        /// </summary>
        /// <param name="source">要合并的源参数对象。</param>
        /// <param name="overwrite">为 true 时以 source 数据覆盖当前数据；为 false 时仅填充当前为 null 的字段。</param>
        /// <param name="deepClone">为 false 时直接共享 source 的 HObject/HTuple 引用（登记进共享集合，不由本实例释放）。</param>
        public void MergeFrom(RunParameter source, bool overwrite = true, bool deepClone = true)
        {
            if (source == null)
            {
                return;
            }

            var clonedValues = deepClone
                ? new Dictionary<object, object>(ReferenceEqualityComparer.Instance)
                : null;

            object Take(object value)
            {
                if (deepClone)
                {
                    return CloneOutputValue(value, clonedValues);
                }

                RegisterSharedResource(value);
                return value;
            }

            if ((overwrite || HOutput == null) && source.HOutput != null)
            {
                HOutput = (HObject)Take(source.HOutput);
            }

            if ((overwrite || GrayImage == null) && source.GrayImage != null)
            {
                GrayImage = (HObject)Take(source.GrayImage);
            }

            if ((overwrite || RawImage == null) && source.RawImage != null)
            {
                RawImage = (HObject)Take(source.RawImage);
            }

            ResultMessage = overwrite || string.IsNullOrWhiteSpace(ResultMessage) ? source.ResultMessage : ResultMessage;

            foreach (var item in source.Outputs)
            {
                if (overwrite || !Outputs.ContainsKey(item.Key))
                {
                    SetOutput(item.Key, Take(item.Value));
                }
            }

            foreach (var item in source.Inputs)
            {
                if (overwrite || !Inputs.ContainsKey(item.Key))
                {
                    SetInput(item.Key, Take(item.Value));
                }
            }
        }

        /// <summary>
        /// 深拷贝当前 RunParameter，所有 HObject/HTuple 数据均独立克隆。
        /// 框架在向下游节点传递参数前调用此方法，防止节点间数据相互影响。
        /// </summary>
        /// <returns>深拷贝后的新 RunParameter 实例。</returns>
        public IHandleParameter Clone()
        {
            var clonedValues = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
            return new RunParameter
            {
                HOutput = (HObject)CloneOutputValue(HOutput, clonedValues),
                GrayImage = (HObject)CloneOutputValue(GrayImage, clonedValues),
                RawImage = (HObject)CloneOutputValue(RawImage, clonedValues),
                Outputs = CloneOutputs(clonedValues),
                Inputs = CloneInputs(clonedValues),
                ResultMessage = ResultMessage,
            };
        }

        /// <summary>
        /// 创建共享引用的参数副本：HObject/HTuple 直接引用原对象（零拷贝）。
        /// HALCON 算子消费输入对象时只会创建新对象而不会修改输入，因此流程引擎向下游节点
        /// 传递参数时使用本方法可避免每条连线深拷贝整幅图像的性能开销。
        /// 共享资源的生命周期由上游节点管理（登记进共享集合，副本 Reset 时不释放）；
        /// 副本自身新生成的输出资源仍由副本正常持有与释放。
        /// </summary>
        public RunParameter CloneShared()
        {
            var clone = new RunParameter
            {
                hOutput = hOutput,
                grayImage = grayImage,
                rawImage = rawImage,
                ResultMessage = ResultMessage,
                Success = Success,
                Outputs = new Dictionary<string, object>(Outputs),
                Inputs = new Dictionary<string, object>(Inputs),
            };

            // 登记全部共享引用：Reset/替换时不释放，所有权仍在上游。
            clone.RegisterSharedResource(hOutput);
            clone.RegisterSharedResource(grayImage);
            clone.RegisterSharedResource(rawImage);
            foreach (var value in Outputs.Values) clone.RegisterSharedResource(value);
            foreach (var value in Inputs.Values) clone.RegisterSharedResource(value);

            return clone;
        }

        /// <summary>
        /// 释放本实例自有的 HObject 资源并清空数据，将对象恢复到空白状态。
        /// 在流程重置或节点销毁时调用，防止 HALCON 内存泄漏。
        /// 上游共享而来的资源（共享集合）不会被释放。
        /// </summary>
        public void Reset()
        {
            var resources = new HashSet<object>(ReferenceEqualityComparer.Instance);
            AddDisposableResource(resources, hOutput);
            AddDisposableResource(resources, grayImage);
            AddDisposableResource(resources, rawImage);

            foreach (var value in Outputs.Values)
            {
                AddDisposableResource(resources, value);
            }

            foreach (var value in Inputs.Values)
            {
                AddDisposableResource(resources, value);
            }

            foreach (var value in resources)
            {
                if (!IsSharedResource(value))
                {
                    DisposeResource(value);
                }
            }

            hOutput = null;
            grayImage = null;
            rawImage = null;
            sharedResources = null;
            Outputs.Clear();
            Inputs.Clear();
            ResultMessage = null;
            Success = null;
        }

        /// <summary>克隆 Outputs 字典，对每个值执行深拷贝。</summary>
        private Dictionary<string, object> CloneOutputs(Dictionary<object, object> clonedValues)
        {
            var clone = new Dictionary<string, object>();
            foreach (var item in Outputs)
            {
                clone[item.Key] = CloneOutputValue(item.Value, clonedValues);
            }

            return clone;
        }

        /// <summary>克隆 Inputs 字典，对每个值执行深拷贝。</summary>
        private Dictionary<string, object> CloneInputs(Dictionary<object, object> clonedValues)
        {
            var clone = new Dictionary<string, object>();
            foreach (var item in Inputs)
            {
                clone[item.Key] = CloneOutputValue(item.Value, clonedValues);
            }

            return clone;
        }

        /// <summary>
        /// 克隆单个输出值：HObject 和 HTuple 执行深拷贝，其他值类型直接返回引用。
        /// </summary>
        private static object CloneOutputValue(object value, Dictionary<object, object> clonedValues)
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
                HObject hObject => CacheClone(clonedValues, value, hObject.Clone()),
                HTuple tuple => CacheClone(clonedValues, value, tuple.Clone()),
                _ => value
            };
        }

        private static object CacheClone(Dictionary<object, object> clonedValues, object source, object clone)
        {
            clonedValues?.Add(source, clone);
            return clone;
        }

        private void SetHObjectField(ref HObject field, HObject value)
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            var oldValue = field;
            field = value;
            DisposeDetachedResource(oldValue);
        }

        private void DisposeDetachedResource(object value)
        {
            // 上游共享而来的资源不由本实例释放（所有权在上游节点）。
            if (value is not HObject && value is not HTuple || IsResourceReferenced(value) || IsSharedResource(value))
            {
                return;
            }

            DisposeResource(value);
        }

        private bool IsResourceReferenced(object resource)
        {
            return ReferenceEquals(hOutput, resource) ||
                ReferenceEquals(grayImage, resource) ||
                ReferenceEquals(rawImage, resource) ||
                Outputs.Values.Any(value => ReferenceEquals(value, resource)) ||
                Inputs.Values.Any(value => ReferenceEquals(value, resource));
        }

        private static void AddDisposableResource(HashSet<object> resources, object value)
        {
            if (value is HObject || value is HTuple)
            {
                resources.Add(value);
            }
        }

        /// <summary>
        /// 如果 value 是 HObject 则调用 Dispose 释放 HALCON 内存，防止内存泄漏。
        /// </summary>
        private static void DisposeResource(object value)
        {
            switch (value)
            {
                case HObject hObject:
                    hObject.Dispose();
                    break;
                case HTuple tuple:
                    tuple.Dispose();
                    break;
            }
        }
    }
}
