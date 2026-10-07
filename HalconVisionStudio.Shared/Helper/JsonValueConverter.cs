using System;
using System.Text.Json;

namespace HalconVisionStudio.Shared.Helper
{
    /// <summary>
    /// JSON 元素到目标类型的通用转换（AI 工具参数设置与方案参数恢复共用）。
    /// 支持：基元（含字符串数字/布尔的宽容解析）、枚举（字符串名）、
    /// 数组 → List&lt;T&gt; / T[] / ObservableCollection&lt;T&gt;（如 ReadImage 的 ImageFiles）、
    /// 对象 → Dictionary&lt;string, TValue&gt;。
    /// </summary>
    public static class JsonValueConverter
    {
        public static object Convert(JsonElement element, Type targetType)
        {
            targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            // 数组 → 集合
            if (element.ValueKind == JsonValueKind.Array && targetType != typeof(string))
            {
                Type itemType = null;
                if (targetType.IsArray) itemType = targetType.GetElementType();
                else if (targetType.IsGenericType) itemType = targetType.GetGenericArguments()[0];

                if (itemType != null)
                {
                    var list = (System.Collections.IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(itemType));
                    foreach (var item in element.EnumerateArray())
                        list.Add(Convert(item, itemType));

                    if (targetType.IsArray)
                    {
                        var array = Array.CreateInstance(itemType, list.Count);
                        list.CopyTo(array, 0);
                        return array;
                    }
                    if (targetType.IsInstanceOfType(list)) return list;
                    return Activator.CreateInstance(targetType, list); // ObservableCollection<T> 等带 (IList) 构造的集合
                }
            }

            // 对象 → Dictionary<string, TValue>
            if (element.ValueKind == JsonValueKind.Object && targetType.IsGenericType &&
                targetType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.Dictionary<,>) &&
                targetType.GetGenericArguments()[0] == typeof(string))
            {
                var valueType = targetType.GetGenericArguments()[1];
                var dict = Activator.CreateInstance(targetType);
                var add = targetType.GetMethod("Add");
                foreach (var property in element.EnumerateObject())
                    add.Invoke(dict, new[] { property.Name, Convert(property.Value, valueType) });
                return dict;
            }

            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    if (targetType == typeof(int)) return element.GetInt32();
                    if (targetType == typeof(long)) return element.GetInt64();
                    if (targetType == typeof(double)) return element.GetDouble();
                    if (targetType == typeof(float)) return element.GetSingle();
                    if (targetType == typeof(decimal)) return element.GetDecimal();
                    if (targetType == typeof(short)) return element.GetInt16();
                    if (targetType == typeof(byte)) return element.GetByte();
                    if (targetType == typeof(uint)) return element.GetUInt32();
                    break;

                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (targetType == typeof(bool)) return element.GetBoolean();
                    break;

                case JsonValueKind.String:
                    var text = element.GetString();
                    if (targetType == typeof(string)) return text;
                    if (targetType.IsEnum)
                        return Enum.Parse(targetType, text, ignoreCase: true);
                    if (targetType == typeof(double) && double.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
                    if (targetType == typeof(int) && int.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var i)) return i;
                    if (targetType == typeof(float) && float.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var f)) return f;
                    if (targetType == typeof(long) && long.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var l)) return l;
                    if (targetType == typeof(bool) && bool.TryParse(text, out var b)) return b;
                    break;
            }

            // 兜底：原样字符串（如对象赋给 string）
            return element.ToString();
        }
    }
}
