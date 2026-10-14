using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;
using ZeroCodeVisionStudio.Shared.Communication;

namespace Plugin.Handle.DeviceCommunication
{
    /// <summary>PLC 数据类型（按位宽选择，与地址起始字节配套）。</summary>
    public enum PlcValueType
    {
        [Description("位(Bit)")] Bit = 0,
        [Description("16位有符号(Int16)")] Int16 = 1,
        [Description("16位无符号(Word)")] UInt16 = 2,
        [Description("32位有符号(DInt)")] Int32 = 3,
        [Description("32位无符号(DWord)")] UInt32 = 4,
        [Description("32位浮点(Float)")] Float = 5
    }

    /// <summary>PLC 值类型 → 驱动线名。</summary>
    internal static class PlcValueTypeMixin
    {
        public static string ToWireName(this PlcValueType t) => t switch
        {
            PlcValueType.Bit => "bit",
            PlcValueType.Int16 => "int16",
            PlcValueType.UInt16 => "uint16",
            PlcValueType.Int32 => "int32",
            PlcValueType.UInt32 => "uint32",
            _ => "float"
        };
    }

    /// <summary>
    /// PLC 读取参数。
    /// </summary>
    public class PlcReadParam : IHandleParameter
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

        public IHandleParameter Clone() => (PlcReadParam)MemberwiseClone();

        public void Reset()
        {
            DriverName = "";
            Address = "D100";
            ValueType = PlcValueType.UInt16;
        }
    }

    /// <summary>
    /// PLC 读取算子：通过通讯驱动中心的长连接 PLC 驱动（西门子 S7 / 三菱 MC / 欧姆龙 FINS）
    /// 读取位/字/双字/浮点值。输出 Value（原始类型）与 ValueText（字符串，供下游 $ValueText$ 插值）。
    /// </summary>
    [Handle("PLC读取", "PlcRead", "通过通讯驱动中心的长连接 PLC 驱动（西门子 S7 / 三菱 MC / 欧姆龙 FINS）读取位/字/双字/浮点值。输出 Value 与 ValueText。", "设备通讯", OperatorBackend = OperatorBackend.Hybrid)]
    public class PlcReadHandle : ParameterHandleBase<PlcReadParam>
    {
        public PlcReadHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public override void Handle()
        {
            var p = Parameter;
            if (string.IsNullOrWhiteSpace(p?.DriverName))
            {
                throw new HandleException(ExceptionType.E_PARAMETER_INVALID,
                    "PlcRead 未配置驱动名称。请先在「工具 → 通讯驱动管理」创建 西门子S7 / 三菱MC / 欧姆龙FINS 驱动。");
            }
            if (string.IsNullOrWhiteSpace(p.Address))
            {
                throw new HandleException(ExceptionType.E_PARAMETER_INVALID, "PlcRead 未配置 PLC 地址。");
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

            object value;
            try
            {
                value = plc.ReadValue(p.Address.Trim(), p.ValueType.ToWireName());
            }
            catch (FormatException fex)
            {
                throw new HandleException(ExceptionType.E_PARAMETER_INVALID, $"PLC 地址无效：{fex.Message}");
            }

            if (value == null)
            {
                throw new HandleException(ExceptionType.E_NO_VALID_INPUT,
                    $"读取 [{p.Address}] 失败：{driver.LastError}");
            }

            RunParameter.SetOutput("Value", value);
            RunParameter.SetOutput("ValueText", value.ToString());
            RunParameter.ResultMessage = $"读取 {p.Address} = {value}（{p.ValueType}）";
        }
    }
}
