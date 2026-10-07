using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using HalconVisionStudio.Shared;
using HalconDotNet;
using Plugin.Handle.DisplayInfo.Views;

namespace Plugin.Handle.DisplayInfo
{
    /// <summary>
    /// 组合固定文本和上游节点输出，并将结果显示到右侧显示窗口叠加层。
    /// 动态显示项数量由参数窗口维护，输入来源绑定会按显示项数量自动增减。
    /// </summary>
    [Handle("显示信息", "DisplayInfo", "组合文本和上游节点输出，并在右侧显示窗口中叠加显示。", Common.HandleType_ResultOutput)]
    public class DisplayInfoHandle : ParameterHandleBase<DisplayInfoParameter>, IHandleDataSlots
    {
        public const string BackgroundInputKey = "display.background";
        public const string ContentInputKey = "display.content";
        public const string ItemInputKeyPrefix = "display.item.";

        public DisplayInfoHandle(IDialogService dialogService) : base(dialogService)
        {
            Parameter.EnsureDefaultItems();
        }

        protected override string DialogName => nameof(DisplayInfoView);

        public IReadOnlyList<RunSlot> InputSlots
        {
            get
            {
                var slots = new List<RunSlot>
                {
                    new RunSlot(BackgroundInputKey, "背景图像/区域", typeof(HObject)),
                    new RunSlot(ContentInputKey, "显示内容", typeof(object))
                };

                for (var i = 0; i < Parameter.Items.Count; i++)
                {
                    var item = Parameter.Items[i];
                    slots.Add(new RunSlot(GetItemInputKey(i), GetItemDisplayName(i, item), typeof(object)));
                }

                return slots;
            }
        }

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出图像/区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.DisplayText, "显示文本", typeof(string))
        };

        public override void Handle()
        {
            var background = GetBackgroundObject();
            if (background != null)
            {
                RunParameter.HOutput = background;
                RunParameter.SetOutput(RunOutputKeys.Output, background);
            }

            var displayText = BuildDisplayText();
            RunParameter.SetOutput(RunOutputKeys.DisplayText, displayText);
            RunParameter.SetOutput(RunOutputKeys.DisplayTextColor, NormalizeColor(Parameter.TextColor, "#F8FAFC"));
            RunParameter.SetOutput(RunOutputKeys.DisplayBackgroundColor, NormalizeColor(Parameter.BackgroundColor, "#B30F172A"));
            RunParameter.ResultMessage = displayText;

            RaiseImageDisplay(background);
        }

        public static string GetItemInputKey(int index)
        {
            return $"{ItemInputKeyPrefix}{index + 1}";
        }

        private HObject GetBackgroundObject()
        {
            if (RunParameter?.TryGetInput<HObject>(BackgroundInputKey, out var inputObject) == true && inputObject != null)
            {
                return inputObject;
            }

            return RunParameter?.HOutput ?? RunParameter?.GrayImage ?? RunParameter?.RawImage;
        }

        private string BuildDisplayText()
        {
            var lines = new List<string>();
            AddLine(lines, Parameter.HeaderText);
            AddLine(lines, Parameter.FreeText);
            AddLine(lines, FormatValue(ReadInputValue(ContentInputKey)));

            for (var i = 0; i < Parameter.Items.Count; i++)
            {
                var item = Parameter.Items[i];
                AddItemLine(lines, item, ReadInputValue(GetItemInputKey(i)));
            }

            if (lines.Count == 0)
            {
                lines.Add("无显示内容");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private object ReadInputValue(string key)
        {
            if (RunParameter?.TryGetInput<object>(key, out var inputValue) == true)
            {
                return inputValue;
            }

            return RunParameter?.GetOutput<object>(key);
        }

        private static string GetItemDisplayName(int index, DisplayInfoItemParameter item)
        {
            var title = string.IsNullOrWhiteSpace(item?.Title) ? $"显示项{index + 1}" : item.Title.Trim();
            return $"显示值{index + 1} - {title}";
        }

        private void AddItemLine(ICollection<string> lines, DisplayInfoItemParameter item, object value)
        {
            var segments = new List<string>();
            AddSegment(segments, item.PrefixText);

            var valueText = value == null ? NormalizeText(item.EmptyText) : FormatValue(value);
            if (string.IsNullOrWhiteSpace(valueText))
            {
                valueText = NormalizeText(item.EmptyText);
            }

            if (item.IncludeLabel && !string.IsNullOrWhiteSpace(item.Title))
            {
                segments.Add($"{NormalizeText(item.Title)}：{valueText}");
            }
            else
            {
                AddSegment(segments, valueText);
            }

            if (segments.Count > 0)
            {
                lines.Add(string.Join(" ", segments.Where(x => !string.IsNullOrWhiteSpace(x))));
            }
        }

        private static void AddLine(ICollection<string> lines, string text)
        {
            var normalized = NormalizeText(text);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                lines.Add(normalized);
            }
        }

        private static void AddSegment(ICollection<string> segments, string text)
        {
            var normalized = NormalizeText(text);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                segments.Add(normalized);
            }
        }

        private static string NormalizeText(string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : text.Replace("\\n", Environment.NewLine).Trim();
        }

        private static string NormalizeColor(string colorText, string fallback)
        {
            return string.IsNullOrWhiteSpace(colorText) ? fallback : colorText.Trim();
        }

        private static string FormatValue(object value)
        {
            return value switch
            {
                null => string.Empty,
                string text => NormalizeText(text),
                HObject hObject => GetHObjectText(hObject),
                HTuple tuple => FormatTuple(tuple),
                double number => number.ToString("0.###", CultureInfo.InvariantCulture),
                float number => number.ToString("0.###", CultureInfo.InvariantCulture),
                decimal number => number.ToString("0.###", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };
        }

        private static string GetHObjectText(HObject hObject)
        {
            try
            {
                HOperatorSet.GetObjClass(hObject, out var objectClass);
                return objectClass.S switch
                {
                    "image" => "图像",
                    "region" => "区域",
                    "xld_cont" => "XLD轮廓",
                    _ => $"HALCON对象({objectClass.S})"
                };
            }
            catch
            {
                return "HALCON对象";
            }
        }

        private static string FormatTuple(HTuple tuple)
        {
            if (tuple == null || tuple.Length == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            var length = Math.Min(tuple.Length, 8);
            for (var i = 0; i < length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                var element = tuple[i];
                var formattedValue = FormatTupleElement(element);
                builder.Append(formattedValue);
            }

            if (tuple.Length > length)
            {
                builder.Append(", ...");
            }

            return builder.ToString();
        }

        private static string FormatTupleElement(HTuple element)
        {
            if (element == null || element.Length == 0)
            {
                return string.Empty;
            }

            try
            {
                var obj = element.O;
                if (obj is string str)
                {
                    return str;
                }
                if (obj is double dbl)
                {
                    return dbl.ToString("0.###", CultureInfo.InvariantCulture);
                }
                if (obj is int intVal)
                {
                    return intVal.ToString();
                }
                if (obj is long lng)
                {
                    return lng.ToString();
                }
                if (obj is float flt)
                {
                    return flt.ToString("0.###", CultureInfo.InvariantCulture);
                }
                return obj.ToString();
            }
            catch
            {
                return element.ToString();
            }
        }
    }
}
