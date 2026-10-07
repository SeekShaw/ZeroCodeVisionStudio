using HalconDotNet;
using HalconVisionStudio.Shared;

namespace HalconVisionStudio.UI.Controls
{
    /// <summary>
    /// 工作流输入绑定下拉列表的辅助类——构建上游节点的输出选项并判断类型兼容性。
    /// <para>
    /// 使用场景：当用户在"绑定配置"面板中为某个节点的输入槽选择数据来源时，
    /// 调用 <see cref="BuildOutputOptions"/> 为该绑定生成所有可选的上游输出槽列表。
    /// </para>
    /// </summary>
    public static class WorkflowBindingHelper
    {
        /// <summary>
        /// 构建指定输入绑定可选的上游输出选项列表，列表第一项始终为"未绑定"占位项。
        /// <para>
        /// 遍历流程中除目标节点自身以外的所有节点，逐一枚举其输出槽，
        /// 并通过 <see cref="IsCompatible"/> 过滤出与目标绑定类型兼容的槽，生成对应的 <see cref="WorkflowOutputOption"/>。
        /// </para>
        /// </summary>
        /// <param name="nodes">流程中所有节点的枚举序列。</param>
        /// <param name="targetNode">目标节点（自身排除在外，不可绑定到自己）。</param>
        /// <param name="targetBinding">目标输入绑定，用于类型兼容性过滤；为 null 时不做过滤。</param>
        /// <returns>包含"未绑定"选项和所有兼容上游输出槽选项的枚举序列。</returns>
        public static IEnumerable<WorkflowOutputOption> BuildOutputOptions(
            IEnumerable<IHandleRunModel> nodes,
            IHandleRunModel targetNode,
            WorkflowInputBinding targetBinding = null)
        {
            yield return new WorkflowOutputOption(null, null, "未绑定");

            if (nodes == null)
            {
                yield break;
            }

            foreach (var node in nodes.Where(x => x != null && !ReferenceEquals(x, targetNode)))
            {
                foreach (var slot in GetOutputSlots(node))
                {
                    if (!IsCompatible(targetBinding?.TargetInputType, slot.ValueType))
                    {
                        continue;
                    }

                    yield return new WorkflowOutputOption(
                        node,
                        slot.Key,
                        $"{node.DisplayName} / {slot.DisplayName}",
                        slot.ValueType);
                }
            }
        }

        /// <summary>
        /// 获取指定节点所有可输出的数据槽列表（静态声明的槽 + 运行时动态槽的并集）。
        /// <para>
        /// 优先级：
        /// <list type="number">
        ///   <item>从 <see cref="IHandleDataSlots.OutputSlots"/> 获取静态声明的输出槽。</item>
        ///   <item>始终追加 <see cref="RunOutputKeys.ResultMessage"/>（结果信息）槽。</item>
        ///   <item>若没有 <see cref="RunOutputKeys.Output"/> 槽，则在最前面插入默认的"输出图像/区域"槽。</item>
        ///   <item>从 <see cref="RunParameter"/> 的 RawImage、GrayImage 和 Outputs 字典追加运行时动态槽。</item>
        /// </list>
        /// 最终结果按键名去重，保留第一个出现的槽定义。
        /// </para>
        /// </summary>
        /// <param name="node">要查询的节点。</param>
        /// <returns>去重后的输出槽集合。</returns>
        public static IEnumerable<RunSlot> GetOutputSlots(IHandleRunModel node)
        {
            var slots = new List<RunSlot>();
            if (node?.Handle is IHandleDataSlots dataSlots && dataSlots.OutputSlots?.Count > 0)
            {
                slots.AddRange(dataSlots.OutputSlots);
            }

            AddSlot(slots, RunOutputKeys.ResultMessage, "结果信息", typeof(string));

            // 执行结果为全节点常驻输出：引擎每轮执行后写入，下游可绑定（条件如 $执行结果$ == "失败"）
            AddSlot(slots, RunOutputKeys.ExecutionSuccess, "是否成功", typeof(bool));
            AddSlot(slots, RunOutputKeys.ExecutionStatus, "执行结果", typeof(string));

            if (slots.All(x => x.Key != RunOutputKeys.Output))
            {
                slots.Insert(0, new RunSlot(RunOutputKeys.Output, "输出图像/区域", typeof(HObject)));
            }

            var parameter = node?.Handle?.RunParameter;
            if (parameter != null)
            {
                AddRuntimeSlot(slots, RunOutputKeys.RawImage, parameter.RawImage);
                AddRuntimeSlot(slots, RunOutputKeys.GrayImage, parameter.GrayImage);

                foreach (var output in parameter.Outputs)
                {
                    AddRuntimeSlot(slots, output.Key, output.Value);
                }
            }

            return slots
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .GroupBy(x => x.Key)
                .Select(x => x.First());
        }

        /// <summary>
        /// 判断目标输入类型与上游输出类型是否兼容，用于过滤不适用的绑定选项。
        /// <para>兼容规则：</para>
        /// <list type="bullet">
        ///   <item>任一类型为 null 时视为兼容（宽松模式）。</item>
        ///   <item><paramref name="targetType"/> 可由 <paramref name="outputType"/> 赋值（继承关系兼容）。</item>
        ///   <item><see cref="HTuple"/> 对 <see cref="HTuple"/> 互相兼容。</item>
        ///   <item>数值类型与数值类型，或数值类型与 <see cref="HTuple"/> 兼容。</item>
        ///   <item><see langword="string"/> 与 <see cref="HTuple"/> 兼容。</item>
        /// </list>
        /// </summary>
        /// <param name="targetType">目标输入槽的期望类型；为 null 表示不限制。</param>
        /// <param name="outputType">上游输出槽的实际类型；为 null 表示不限制。</param>
        /// <returns>两种类型兼容时返回 true，否则返回 false。</returns>
        public static bool IsCompatible(Type targetType, Type outputType)
        {
            if (targetType == null || outputType == null)
            {
                return true;
            }

            targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            outputType = Nullable.GetUnderlyingType(outputType) ?? outputType;

            if (targetType.IsAssignableFrom(outputType))
            {
                return true;
            }

            if (targetType == typeof(HTuple) && outputType == typeof(HTuple))
            {
                return true;
            }

            if (IsNumericType(targetType) && (IsNumericType(outputType) || outputType == typeof(HTuple)))
            {
                return true;
            }

            return targetType == typeof(string) && outputType == typeof(HTuple);
        }

        /// <summary>
        /// 若运行时值不为 null 且键名在集合中尚不存在，则追加一个动态槽。
        /// </summary>
        private static void AddRuntimeSlot(ICollection<RunSlot> slots, string key, object value)
        {
            if (value == null || string.IsNullOrWhiteSpace(key) || slots.Any(x => x.Key == key))
            {
                return;
            }

            slots.Add(new RunSlot(key, GetOutputDisplayName(key), value.GetType()));
        }

        /// <summary>
        /// 将输出键名映射为中文可读显示名称，未匹配时直接返回键名本身。
        /// </summary>
        private static string GetOutputDisplayName(string key)
        {
            return key switch
            {
                RunOutputKeys.Output => "输出图像/区域",
                RunOutputKeys.RawImage => "原始图像",
                RunOutputKeys.GrayImage => "灰度图像",
                RunOutputKeys.Region => "区域",
                RunOutputKeys.Xld => "XLD轮廓",
                RunOutputKeys.DefectCount => "缺陷数量",
                RunOutputKeys.TotalDefectArea => "缺陷总面积",
                RunOutputKeys.ShapeModelId => "形状模板ID",
                RunOutputKeys.ShapeModelContours => "形状模板轮廓",
                RunOutputKeys.MatchRows => "匹配行坐标",
                RunOutputKeys.MatchColumns => "匹配列坐标",
                RunOutputKeys.MatchAngles => "匹配角度",
                RunOutputKeys.MatchScores => "匹配分数",
                RunOutputKeys.DisplayText => "显示文本",
                RunOutputKeys.DisplayTextColor => "显示文字颜色",
                RunOutputKeys.DisplayBackgroundColor => "显示背景颜色",
                RunOutputKeys.ResultMessage => "结果信息",
                RunOutputKeys.SmallestCircleRow => "最小外接圆行坐标",
                RunOutputKeys.SmallestCircleColumn => "最小外接圆列坐标",
                RunOutputKeys.SmallestCircleRadius => "最小外接圆半径",
                RunOutputKeys.SmallestRectangle1Row1 => "最小外接矩形 Row1",
                RunOutputKeys.SmallestRectangle1Column1 => "最小外接矩形 Column1",
                RunOutputKeys.SmallestRectangle1Row2 => "最小外接矩形 Row2",
                RunOutputKeys.SmallestRectangle1Column2 => "最小外接矩形 Column2",
                RunOutputKeys.SmallestRectangle2Row => "最小方向矩形行坐标",
                RunOutputKeys.SmallestRectangle2Column => "最小方向矩形列坐标",
                RunOutputKeys.SmallestRectangle2Phi => "最小方向矩形角度",
                RunOutputKeys.SmallestRectangle2Length1 => "最小方向矩形 Length1",
                RunOutputKeys.SmallestRectangle2Length2 => "最小方向矩形 Length2",
                RunOutputKeys.InnerCircleRow => "最大内接圆行坐标",
                RunOutputKeys.InnerCircleColumn => "最大内接圆列坐标",
                RunOutputKeys.InnerCircleRadius => "最大内接圆半径",
                RunOutputKeys.InnerRectangle1Row1 => "最大内接矩形 Row1",
                RunOutputKeys.InnerRectangle1Column1 => "最大内接矩形 Column1",
                RunOutputKeys.InnerRectangle1Row2 => "最大内接矩形 Row2",
                RunOutputKeys.InnerRectangle1Column2 => "最大内接矩形 Column2",
                _ => key
            };
        }

        /// <summary>
        /// 判断类型是否为数值类型（byte/short/int/long/float/double/decimal，支持可空类型拆箱）。
        /// </summary>
        private static bool IsNumericType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type == typeof(byte) ||
                type == typeof(short) ||
                type == typeof(int) ||
                type == typeof(long) ||
                type == typeof(float) ||
                type == typeof(double) ||
                type == typeof(decimal);
        }

        /// <summary>
        /// 若键名在集合中尚不存在，则追加一个带指定类型的静态槽。
        /// </summary>
        private static void AddSlot(ICollection<RunSlot> slots, string key, string displayName, Type valueType)
        {
            if (string.IsNullOrWhiteSpace(key) || slots.Any(x => x.Key == key))
            {
                return;
            }

            slots.Add(new RunSlot(key, displayName, valueType));
        }
    }
}
