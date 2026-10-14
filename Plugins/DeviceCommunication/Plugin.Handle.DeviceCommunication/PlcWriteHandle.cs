using System.ComponentModel;
using System.Globalization;
using ZeroCodeVisionStudio.Shared;
using ZeroCodeVisionStudio.Shared.Communication;

namespace Plugin.Handle.DeviceCommunication
{
    /// <summary>
    /// PLC 写入参数。
    /// </summary>
    public class PlcWriteParam : IHandleParameter
    {
        [DisplayName("驱动名称")]
        [DriverOptions(CommDriverType.S7, CommDriverType.MelsecMc, CommDriverType.Fins)]
        [Description("通讯驱动中心配置的 PLC 驱动名称（西门子S7 / 三菱MC / 欧姆龙FINS）")]
        public string DriverName { get; set; } = "";

        [DisplayName("PLC 地址")]
        [Description("西门子：DB10.DBW0 / MW100 / M100.3 / I50 / Q80；三菱：D100 / W100 / M100 / X1A；欧姆龙：D100 / W100 / H100 / CIO100 / CIO100.05")]
        public string Address { get; set; } = "D100";

        [DisplayName("数据类型")]
        public PlcValueType ValueType { get; set; } = PlcValueType.UInt16;

        [DisplayName("写入值")]
        [Description("数值或 $Key$ 上游插值；Bit 类型接受 0/1/true/false/ON/OFF")]
        public string WriteValue { get; set; } = "0";

        public IHandleParameter Clone() => (PlcWriteParam)MemberwiseClone();

        public void Reset()
        {
            DriverName = "";
            Address = "D100";
            ValueType = PlcValueType.UInt16;
            WriteValue = "0";
        }
    }

    /// <summary>
    /// PLC 写入算子：通过通讯驱动中心的长连接 PLC 驱动（西门子 S7 / 三菱 MC / 欧姆龙 FINS）
    /// 写入位/字/双字/浮点值，写入值支持 $Key$ 上游插值。
    /// </summary>
    [Handle("PLC写入", "PlcWrite", "通过通讯驱动中心的长连接 PLC 驱动（西门子 S7 / 三菱 MC / 欧姆龙 FINS）写入位/字/双字/浮点值。写入值支持 $Key$ 上游值插值。", "设备通讯", OperatorBackend = OperatorBackend.Hybrid)]
    public class PlcWriteHandle : ParameterHandleBase<PlcWriteParam>
    {
        public PlcWriteHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public override void Handle()
        {
            var p = Parameter;
            if (string.IsNullOrWhiteSpace(p?.DriverName))
            {
                throw new HandleException(ExceptionType.E_PARAMETER_INVALID,
                    "PlcWrite 未配置驱动名称。请先在「工具 → 通讯驱动管理」创建 西门子S7 / 三菱MC / 欧姆龙FINS 驱动。");
            }
            if (string.IsNullOrWhiteSpace(p.Address))
            {
                throw new HandleException(ExceptionType.E_PARAMETER_INVALID, "PlcWrite 未配置 PLC 地址。");
            }

            var driver = CommunicationCenter.GetDriver(p.DriverName);
            if (driver == null)
            {
                throw new HandleException(ExceptionType.E_PARAMETER_INVALID,
                    $"未找到名为 [{p.DriverName}] 的通讯驱动。");
            }

            if (driver is not IPlcValueDriver plc)
            {
                throw new HandleException(ExceptionType.E_PARAMETER_INVALID,
                    $"驱动 [{p.DriverName}]（{driver.Config.Type}）不支持 PLC 值读写，请使用 西门子S7 / 三菱MC / 欧姆龙FINS 类型驱动。");
            }

            if (!driver.Connect())
            {
                throw new HandleException(ExceptionType.E_NO_VALID_INPUT,
                    $"PLC 驱动 [{p.DriverName}] 连接失败：{driver.LastError}");
            }

            var raw = Interpolate(p.WriteValue.Trim());
            object value;
            if (p.ValueType == PlcValueType.Bit)
            {
                value = raw is "1" or "0" or "true" or "false" or "True" or "False" or "ON" or "OFF" or "on" or "off"
                    ? raw is "1" or "true" or "True" or "ON" or "on"
                    : double.Parse(raw, CultureInfo.InvariantCulture);
            }
            else
            {
                value = double.Parse(raw, CultureInfo.InvariantCulture);
            }

            bool ok;
            try
            {
                ok = plc.WriteValue(p.Address.Trim(), p.ValueType.ToWireName(), value);
            }
            catch (FormatException fex)
            {
                throw new HandleException(ExceptionType.E_PARAMETER_INVALID, $"PLC 地址无效：{fex.Message}");
            }

            if (!ok)
            {
                throw new HandleException(ExceptionType.E_NO_VALID_INPUT,
                    $"写入 [{p.Address}] 失败：{driver.LastError}");
            }

            RunParameter.SetOutput("WrittenValue", value.ToString());
            RunParameter.ResultMessage = $"已写入 {p.Address} = {value}（{p.ValueType}）";
        }

        /// <summary>$Key$ 插值（上游输出替换）。</summary>
        private string Interpolate(string template)
        {
            if (string.IsNullOrEmpty(template) || !template.Contains('$') || RunParameter == null)
                return template;

            foreach (var kv in RunParameter.Outputs)
                template = template.Replace("$" + kv.Key + "$", kv.Value?.ToString() ?? "");
            foreach (var kv in RunParameter.Inputs)
                template = template.Replace("$" + kv.Key + "$", kv.Value?.ToString() ?? "");
            return template;
        }
    }
}
