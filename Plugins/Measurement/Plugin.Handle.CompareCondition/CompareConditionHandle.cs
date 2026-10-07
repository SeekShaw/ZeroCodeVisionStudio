using System.Collections;
using System.Globalization;
using System.Text;
using HalconVisionStudio.Shared;
using HalconDotNet;
using Plugin.Handle.CompareCondition.Views;

namespace Plugin.Handle.CompareCondition
{
    [Handle("条件比较", "CompareCondition", "支持自定义比较项，并可将每个比较项绑定到其他节点输出。", Common.HandleType_Measurement)]
    public class CompareConditionHandle : ParameterHandleBase<CompareConditionParameter>, IHandleDataSlots
    {
        public const string LeftInputKeyPrefix = "compare.left.";
        public const string RightInputKeyPrefix = "compare.right.";

        public CompareConditionHandle(IDialogService dialogService) : base(dialogService)
        {
            Parameter.EnsureDefaultItems();
        }

        protected override string DialogName => nameof(CompareConditionView);

        public IReadOnlyList<RunSlot> InputSlots
        {
            get
            {
                Parameter.EnsureBindingKeys();
                var slots = new List<RunSlot>();
                for (var i = 0; i < Parameter.Conditions.Count; i++)
                {
                    var condition = Parameter.Conditions[i];
                    slots.Add(new RunSlot(GetLeftInputKey(condition), GetLeftInputName(i, condition), typeof(object)));

                    if (condition.RightValueMode == CompareRightValueMode.Input)
                    {
                        slots.Add(new RunSlot(GetRightInputKey(condition), GetRightInputName(i, condition), typeof(object)));
                    }
                }

                return slots;
            }
        }

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "比较结果", typeof(bool)),
            new RunSlot(RunOutputKeys.ResultMessage, "比较明细", typeof(string))
        };

        public override void Handle()
        {
            if (RunParameter == null)
            {
                SetRunParameter(null);
            }

            var enabledConditions = Parameter.Conditions
                .Select((condition, index) => new CompareConditionContext(condition, index))
                .Where(x => x.Condition.Enabled)
                .ToList();

            if (enabledConditions.Count == 0)
            {
                SetResult(false, "未启用任何比较项。");
                return;
            }

            var details = new List<string>();
            var result = Parameter.LogicOperator == LogicOperator.And;

            foreach (var context in enabledConditions)
            {
                var conditionResult = EvaluateCondition(context.Condition, context.Index, out var detail);
                details.Add(detail);

                if (Parameter.LogicOperator == LogicOperator.And)
                {
                    result &= conditionResult;
                    if (!result)
                    {
                        break;
                    }
                }
                else
                {
                    result |= conditionResult;
                    if (result)
                    {
                        break;
                    }
                }
            }

            var message = BuildResultMessage(result, details);
            SetResult(result, message);
        }

        private static string GetLeftInputKey(CompareConditionItem condition)
        {
            return $"{LeftInputKeyPrefix}{GetBindingKey(condition)}";
        }

        private static string GetRightInputKey(CompareConditionItem condition)
        {
            return $"{RightInputKeyPrefix}{GetBindingKey(condition)}";
        }

        private static string GetBindingKey(CompareConditionItem condition)
        {
            if (string.IsNullOrWhiteSpace(condition.BindingKey))
            {
                condition.BindingKey = Guid.NewGuid().ToString("N");
            }

            return condition.BindingKey;
        }

        private bool EvaluateCondition(CompareConditionItem condition, int index, out string detail)
        {
            var leftValue = ReadInputValue(GetLeftInputKey(condition));
            var rightValue = condition.RightValueMode == CompareRightValueMode.Input
                ? ReadInputValue(GetRightInputKey(condition))
                : condition.ReferenceValue;

            var name = GetConditionName(index, condition);
            var result = CompareValues(condition, leftValue, rightValue);
            detail = $"{name}: {FormatValue(leftValue)} {GetCompareSymbol(condition.CompareType)} {FormatValue(rightValue)} => {(result ? "OK" : "NG")}";
            return result;
        }

        private object ReadInputValue(string key)
        {
            if (RunParameter?.TryGetInput<object>(key, out var inputValue) == true)
            {
                return inputValue;
            }

            return RunParameter?.GetOutput<object>(key);
        }

        private static bool CompareValues(CompareConditionItem condition, object leftValue, object rightValue)
        {
            return condition.ValueType switch
            {
                CompareValueType.Number => CompareNumbers(condition.CompareType, leftValue, rightValue),
                CompareValueType.Text => CompareText(condition.CompareType, leftValue, rightValue),
                CompareValueType.Boolean => CompareBoolean(condition.CompareType, leftValue, rightValue),
                _ => CompareAuto(condition.CompareType, leftValue, rightValue)
            };
        }

        private static bool CompareAuto(CompareType compareType, object leftValue, object rightValue)
        {
            if (compareType is CompareType.Contains or CompareType.NotContains or CompareType.IsEmpty or CompareType.IsNotEmpty)
            {
                return CompareText(compareType, leftValue, rightValue);
            }

            if (TryConvertDouble(leftValue, out _) && TryConvertDouble(rightValue, out _))
            {
                return CompareNumbers(compareType, leftValue, rightValue);
            }

            if (TryConvertBool(leftValue, out _) && TryConvertBool(rightValue, out _))
            {
                return CompareBoolean(compareType, leftValue, rightValue);
            }

            return CompareText(compareType, leftValue, rightValue);
        }

        private static bool CompareNumbers(CompareType compareType, object leftValue, object rightValue)
        {
            if (!TryConvertDouble(leftValue, out var left) || !TryConvertDouble(rightValue, out var right))
            {
                return false;
            }

            return compareType switch
            {
                CompareType.Equal => Math.Abs(left - right) < 0.0000001,
                CompareType.NotEqual => Math.Abs(left - right) >= 0.0000001,
                CompareType.GreaterThan => left > right,
                CompareType.LessThan => left < right,
                CompareType.GreaterThanOrEqual => left >= right,
                CompareType.LessThanOrEqual => left <= right,
                _ => false
            };
        }

        private static bool CompareText(CompareType compareType, object leftValue, object rightValue)
        {
            var left = ConvertToText(leftValue);
            var right = ConvertToText(rightValue);
            var comparison = StringComparison.OrdinalIgnoreCase;
            var compareResult = string.Compare(left, right, comparison);

            return compareType switch
            {
                CompareType.Equal => compareResult == 0,
                CompareType.NotEqual => compareResult != 0,
                CompareType.GreaterThan => compareResult > 0,
                CompareType.LessThan => compareResult < 0,
                CompareType.GreaterThanOrEqual => compareResult >= 0,
                CompareType.LessThanOrEqual => compareResult <= 0,
                CompareType.Contains => left?.IndexOf(right ?? string.Empty, comparison) >= 0,
                CompareType.NotContains => left?.IndexOf(right ?? string.Empty, comparison) < 0,
                CompareType.IsEmpty => string.IsNullOrWhiteSpace(left),
                CompareType.IsNotEmpty => !string.IsNullOrWhiteSpace(left),
                _ => false
            };
        }

        private static bool CompareBoolean(CompareType compareType, object leftValue, object rightValue)
        {
            if (!TryConvertBool(leftValue, out var left) || !TryConvertBool(rightValue, out var right))
            {
                return false;
            }

            return compareType switch
            {
                CompareType.Equal => left == right,
                CompareType.NotEqual => left != right,
                _ => false
            };
        }

        private void SetResult(bool result, string message)
        {
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.ResultMessage, message);
            RunParameter.ResultMessage = message;
        }

        private string BuildResultMessage(bool result, IList<string> details)
        {
            var status = result ? "条件成立" : "条件不成立";
            if (!Parameter.OutputDetailText || details.Count == 0)
            {
                return status;
            }

            return status + Environment.NewLine + string.Join(Environment.NewLine, details);
        }

        private static string GetLeftInputName(int index, CompareConditionItem condition)
        {
            return $"{GetConditionName(index, condition)} - 左值";
        }

        private static string GetRightInputName(int index, CompareConditionItem condition)
        {
            return $"{GetConditionName(index, condition)} - 右值";
        }

        private static string GetConditionName(int index, CompareConditionItem condition)
        {
            return string.IsNullOrWhiteSpace(condition?.Name)
                ? $"比较项{index + 1}"
                : condition.Name.Trim();
        }

        private static string GetCompareSymbol(CompareType compareType)
        {
            return compareType switch
            {
                CompareType.Equal => "==",
                CompareType.NotEqual => "!=",
                CompareType.GreaterThan => ">",
                CompareType.LessThan => "<",
                CompareType.GreaterThanOrEqual => ">=",
                CompareType.LessThanOrEqual => "<=",
                CompareType.Contains => "包含",
                CompareType.NotContains => "不包含",
                CompareType.IsEmpty => "为空",
                CompareType.IsNotEmpty => "不为空",
                _ => compareType.ToString()
            };
        }

        private static bool TryConvertDouble(object value, out double result)
        {
            value = UnwrapValue(value);
            switch (value)
            {
                case null:
                    result = 0;
                    return false;
                case double number:
                    result = number;
                    return true;
                case float number:
                    result = number;
                    return true;
                case decimal number:
                    result = (double)number;
                    return true;
                case int number:
                    result = number;
                    return true;
                case long number:
                    result = number;
                    return true;
                case short number:
                    result = number;
                    return true;
                case byte number:
                    result = number;
                    return true;
                case string text:
                    return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out result) ||
                        double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out result);
                case IFormattable formattable:
                    return double.TryParse(formattable.ToString(null, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
                default:
                    return double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.CurrentCulture, out result);
            }
        }

        private static bool TryConvertBool(object value, out bool result)
        {
            value = UnwrapValue(value);
            switch (value)
            {
                case null:
                    result = false;
                    return false;
                case bool flag:
                    result = flag;
                    return true;
                case string text:
                    if (bool.TryParse(text, out result))
                    {
                        return true;
                    }

                    if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var numericText))
                    {
                        result = Math.Abs(numericText) > 0.0000001;
                        return true;
                    }

                    return false;
                default:
                    if (TryConvertDouble(value, out var number))
                    {
                        result = Math.Abs(number) > 0.0000001;
                        return true;
                    }

                    return bool.TryParse(value.ToString(), out result);
            }
        }

        private static string ConvertToText(object value)
        {
            value = UnwrapValue(value);
            return value switch
            {
                null => string.Empty,
                string text => text,
                HObject hObject => GetHObjectText(hObject),
                HTuple tuple => FormatTuple(tuple),
                double number => number.ToString("0.###", CultureInfo.InvariantCulture),
                float number => number.ToString("0.###", CultureInfo.InvariantCulture),
                decimal number => number.ToString("0.###", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };
        }

        private static object UnwrapValue(object value)
        {
            if (value is HTuple tuple)
            {
                if (tuple.Length == 0)
                {
                    return null;
                }

                return tuple.Length == 1 ? tuple[0].O : tuple;
            }

            return value;
        }

        private static string FormatValue(object value)
        {
            if (value == null)
            {
                return "空";
            }

            if (value is string text)
            {
                return string.IsNullOrWhiteSpace(text) ? "\"\"" : text;
            }

            return ConvertToText(value);
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

                builder.Append(tuple[i]?.ToString());
            }

            if (tuple.Length > length)
            {
                builder.Append(", ...");
            }

            return builder.ToString();
        }

        private readonly record struct CompareConditionContext(CompareConditionItem Condition, int Index);
    }
}
