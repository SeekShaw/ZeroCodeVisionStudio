using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 算子参数属性适配器，通过反射将参数对象的单个属性包装为可供 WPF 数据绑定的 ViewModel 项。
    /// 支持通过 Range 特性声明数值范围：带范围的数值参数自动使用滑条编辑器并在越界时阻止赋值。
    /// </summary>
    public class ParameterPropertyItem : BindableBase
    {
        private readonly object source;
        private readonly PropertyInfo property;
        private string errorMessage;

        public ParameterPropertyItem(object source, PropertyInfo property)
        {
            this.source = source;
            this.property = property;
            var displayName = property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;
            Name = string.IsNullOrWhiteSpace(displayName) ? property.Name : displayName;
            Description = property.GetCustomAttribute<DescriptionAttribute>()?.Description;

            var fileDialogAttr = property.GetCustomAttribute<FileDialogAttribute>();
            if (fileDialogAttr != null)
            {
                EditorKind = fileDialogAttr.DialogType == FileDialogType.Save
                    ? ParameterEditorKind.FileSave
                    : ParameterEditorKind.FileOpen;
                FileFilter = fileDialogAttr.Filter;
                FileDefaultExtension = fileDialogAttr.DefaultExtension;
            }
            else
            {
                var driverAttr = property.GetCustomAttribute<DriverOptionsAttribute>();
                if (driverAttr != null && property.PropertyType == typeof(string))
                {
                    // 通讯驱动选择：候选项来自驱动中心内允许类型的已配置驱动。
                    EditorKind = ParameterEditorKind.DriverSelect;
                    Options = LoadDriverNames(driverAttr.AllowedTypes);
                }
                else if (property.GetCustomAttribute<VariableNameAttribute>() != null && property.PropertyType == typeof(string))
                {
                    // 全局流程变量名：候选来自当前全部变量，可编辑输入新名。
                    EditorKind = ParameterEditorKind.VariableName;
                    var nameOptions = new List<object>();
                    foreach (var candidate in WorkflowVariableStore.GetCandidates())
                    {
                        nameOptions.Add(candidate.Display);
                    }
                    Options = nameOptions.ToArray();
                }
                else
                {
                    LoadRange();
                    EditorKind = GetEditorKind(property.PropertyType);
                }
            }

            Options ??= property.PropertyType.IsEnum ? Enum.GetValues(property.PropertyType).Cast<object>().ToArray() : Array.Empty<object>();
        }

        /// <summary>该参数当前绑定的源描述（"节点名 · 输出" / "[全局] 变量"），空=未绑定。由对话框 VM 维护。</summary>
        public string BindingSourceText
        {
            get => bindingSourceText;
            set
            {
                if (SetProperty(ref bindingSourceText, value))
                {
                    RaisePropertyChanged(nameof(HasBinding));
                }
            }
        }

        private string bindingSourceText;

        /// <summary>是否已绑定（控制参数行绑定图标的实心态）。</summary>
        public bool HasBinding => !string.IsNullOrWhiteSpace(BindingSourceText);

        public string Name { get; }
        public string Description { get; }
        public ParameterEditorKind EditorKind { get; }
        public object[] Options { get; }

        /// <summary>文件对话框过滤器。</summary>
        public string FileFilter { get; }

        /// <summary>文件默认扩展名。</summary>
        public string FileDefaultExtension { get; }

        /// <summary>是否声明了有效范围（Range 特性）。</summary>
        public bool HasRange { get; private set; }

        /// <summary>范围下限（含）。</summary>
        public double RangeMinimum { get; private set; }

        /// <summary>范围上限（含）。</summary>
        public double RangeMaximum { get; private set; }

        /// <summary>滑条步进（默认按跨度自动取整步长，整数参数恒为 1）。</summary>
        public double RangeStep { get; private set; } = 1;

        /// <summary>是否整数数值参数。</summary>
        public bool IsInteger { get; private set; }

        /// <summary>范围提示文本（无范围时为空）。</summary>
        public string RangeHint => HasRange
            ? $"{FormatNumber(RangeMinimum)} ~ {FormatNumber(RangeMaximum)}"
            : string.Empty;

        public object Value
        {
            get => property.GetValue(source);
            set
            {
                try
                {
                    var converted = ConvertValue(value);

                    if (HasRange && IsNumericValue(converted))
                    {
                        var number = Convert.ToDouble(converted, CultureInfo.InvariantCulture);
                        if (number < RangeMinimum || number > RangeMaximum)
                        {
                            // 越界：不写入底层属性、只提示错误；
                            // 不回弹 TextBox 内容，避免"想输入 10 却在打 1 时被顶回旧值"。
                            ErrorMessage = $"{Name} 的取值范围为 [{FormatNumber(RangeMinimum)}, {FormatNumber(RangeMaximum)}]，当前输入 {FormatNumber(number)} 越界。";
                            return;
                        }
                    }

                    property.SetValue(source, converted);
                    ErrorMessage = string.Empty;
                    RaisePropertyChanged();
                }
                catch (Exception ex)
                {
                    // 转换失败（如输入过程中的 "-"、"0." 等中间态）：仅提示，不回弹输入框内容。
                    ErrorMessage = ex.Message;
                }
            }
        }

        public string ErrorMessage
        {
            get => errorMessage;
            private set => SetProperty(ref errorMessage, value);
        }

        /// <summary>从通讯驱动中心加载允许类型的驱动名列表（下拉候选）。</summary>
        private static object[] LoadDriverNames(Communication.CommDriverType[] allowed)
        {
            try
            {
                return Communication.CommunicationCenter.Configs
                    .Where(c => allowed.Length == 0 || allowed.Contains(c.Type))
                    .Select(c => c.Name)
                    .Cast<object>()
                    .ToArray();
            }
            catch
            {
                return Array.Empty<object>();
            }
        }

        /// <summary>当前项是否存在校验错误（对话框关闭前统一检查）。</summary>
        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        public void Refresh()
        {
            RaisePropertyChanged(nameof(Value));
        }

        /// <summary>
        /// 读取属性上的 Range 特性（按特性名反射识别，兼容 System.ComponentModel.DataAnnotations.RangeAttribute
        /// 与自定义同名特性），数值参数据此启用滑条编辑器与越界校验。
        /// </summary>
        private void LoadRange()
        {
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            IsInteger = type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) ||
                type == typeof(ushort) || type == typeof(int) || type == typeof(uint) || type == typeof(long);

            if (!IsNumericType(type)) return;

            foreach (var attr in property.GetCustomAttributes(inherit: false))
            {
                var attrType = attr.GetType();
                if (attrType.Name != "RangeAttribute") continue;

                var minProp = attrType.GetProperty("Minimum");
                var maxProp = attrType.GetProperty("Maximum");
                if (minProp == null || maxProp == null) continue;

                try
                {
                    var min = Convert.ToDouble(minProp.GetValue(attr), CultureInfo.InvariantCulture);
                    var max = Convert.ToDouble(maxProp.GetValue(attr), CultureInfo.InvariantCulture);
                    if (max <= min) continue;

                    HasRange = true;
                    RangeMinimum = min;
                    RangeMaximum = max;

                    // 整数参数步进 1；浮点按跨度量级取 0.01/0.001 等。
                    RangeStep = IsInteger ? 1 : (max - min) > 20 ? 0.1 : (max - min) > 2 ? 0.01 : 0.001;
                    return;
                }
                catch
                {
                    // 特性参数异常时忽略范围约束。
                }
            }
        }

        private static bool IsNumericType(Type type)
        {
            return type == typeof(byte) || type == typeof(sbyte) ||
                type == typeof(short) || type == typeof(ushort) ||
                type == typeof(int) || type == typeof(uint) ||
                type == typeof(long) || type == typeof(ulong) ||
                type == typeof(float) || type == typeof(double) || type == typeof(decimal);
        }

        private static bool IsNumericValue(object value)
        {
            return value is byte || value is sbyte || value is short || value is ushort ||
                value is int || value is uint || value is long || value is ulong ||
                value is float || value is double || value is decimal;
        }

        private string FormatNumber(double value) => IsInteger
            ? Convert.ToInt64(value).ToString()
            : value.ToString("0.###", CultureInfo.CurrentCulture);

        private object ConvertValue(object value)
        {
            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (value == null)
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            if (targetType.IsInstanceOfType(value))
                return value;

            if (targetType.IsEnum)
                return value is string text ? Enum.Parse(targetType, text) : Enum.ToObject(targetType, value);

            if (targetType == typeof(string))
                return value.ToString();

            if (targetType == typeof(bool))
                return Convert.ToBoolean(value, CultureInfo.CurrentCulture);

            var converter = TypeDescriptor.GetConverter(targetType);
            if (converter.CanConvertFrom(value.GetType()))
                return converter.ConvertFrom(null, CultureInfo.CurrentCulture, value);

            if (value is string valueText)
                return Convert.ChangeType(valueText, targetType, CultureInfo.CurrentCulture);

            return Convert.ChangeType(value, targetType, CultureInfo.CurrentCulture);
        }

        private ParameterEditorKind GetEditorKind(Type propertyType)
        {
            var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            if (type.IsEnum) return ParameterEditorKind.Enum;
            if (type == typeof(bool)) return ParameterEditorKind.Boolean;
            if (HasRange) return ParameterEditorKind.Slider;
            return ParameterEditorKind.Text;
        }
    }
}
