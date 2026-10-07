using System.ComponentModel;
using HalconVisionStudio.Shared;
using HalconVisionStudio.Shared.Communication;

namespace Plugin.Handle.DeviceCommunication
{
    /// <summary>Modbus 功能选择。</summary>
    public enum ModbusFunction
    {
        [Description("读保持寄存器(03)")] ReadHolding = 0,
        [Description("读线圈(01)")] ReadCoils = 1,
        [Description("写单寄存器(06)")] WriteSingleRegister = 2,
        [Description("写单线圈(05)")] WriteSingleCoil = 3,
        [Description("写多寄存器(16)")] WriteMultipleRegisters = 4
    }

    /// <summary>
    /// Modbus 读写算子（走通讯驱动中心的长连接主站）：
    /// 支持 Modbus TCP 与 Modbus RTU 两种驱动，按驱动名称自动分派。
    /// 写操作的值支持 $Key$ 上游插值。
    /// </summary>
    public class ModbusDriverParam : IHandleParameter
    {
        [DisplayName("驱动名称")]
        [DriverOptions(CommDriverType.ModbusTcp, CommDriverType.ModbusRtu)]
        [Description("通讯驱动中心配置的 Modbus TCP / Modbus RTU 驱动名称")]
        public string DriverName { get; set; } = "";

        [DisplayName("功能")]
        public ModbusFunction Function { get; set; } = ModbusFunction.ReadHolding;

        [DisplayName("起始地址")]
        [Description("寄存器/线圈起始地址（0 起）")]
        public int StartAddress { get; set; } = 0;

        [DisplayName("读/写数量")]
        [Description("读模式的寄存器/线圈个数；写多寄存器模式为写入个数")]
        public int Quantity { get; set; } = 1;

        [DisplayName("写入值")]
        [Description("写单寄存器：数值或 $Key$；写线圈：0/1；写多寄存器：逗号分隔值列表")]
        public string WriteValue { get; set; } = "0";

        public IHandleParameter Clone() => (ModbusDriverParam)MemberwiseClone();

        public void Reset()
        {
            DriverName = "";
            Function = ModbusFunction.ReadHolding;
            StartAddress = 0;
            Quantity = 1;
            WriteValue = "0";
        }
    }

    [Handle("Modbus读写", "ModbusDriver", "通过通讯驱动中心的 Modbus TCP / Modbus RTU 长连接主站读写寄存器与线圈（替代旧版每次建连的 Modbus 算子）。写入支持 $Key$ 上游值插值。", "设备通讯")]
    public class ModbusDriverHandle : ParameterHandleBase<ModbusDriverParam>
    {
        public ModbusDriverHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public override void Handle()
        {
            var p = Parameter;
            if (string.IsNullOrWhiteSpace(p?.DriverName))
            {
                throw new HandleException(ExceptionType.E_PARAMETER_INVALID,
                    "ModbusDriver 未配置驱动名称。请先在「工具 → 通讯驱动管理」创建 Modbus TCP 或 Modbus RTU 驱动。");
            }

            var driver = CommunicationCenter.GetDriver(p.DriverName);
            if (driver == null)
            {
                throw new HandleException(ExceptionType.E_PARAMETER_INVALID,
                    $"未找到名为 [{p.DriverName}] 的通讯驱动。");
            }

            if (!driver.Connect())
            {
                throw new HandleException(ExceptionType.E_NO_VALID_INPUT,
                    $"Modbus 驱动 [{p.DriverName}] 连接失败：{driver.LastError}");
            }

            var address = (ushort)Math.Clamp(p.StartAddress, 0, 65535);
            var quantity = (ushort)Math.Clamp(p.Quantity, 1, 125);

            switch (p.Function)
            {
                case ModbusFunction.ReadHolding:
                {
                    var values = AsTcp(driver)?.ReadHoldingRegisters(address, quantity)
                                 ?? AsRtu(driver)?.ReadHoldingRegisters(address, quantity);
                    if (values == null)
                    {
                        throw new HandleException(ExceptionType.E_NO_VALID_INPUT,
                            $"读保持寄存器失败：{driver.LastError}");
                    }
                    for (var i = 0; i < values.Length; i++)
                        RunParameter.SetOutput($"R{address + i}", values[i]);
                    RunParameter.SetOutput("Values", string.Join(",", values));
                    RunParameter.ResultMessage = $"读取保持寄存器 [{address}..{address + quantity - 1}]：{string.Join(", ", values)}";
                    break;
                }

                case ModbusFunction.ReadCoils:
                {
                    var coils = AsTcp(driver)?.ReadCoils(address, quantity)
                                ?? AsRtu(driver)?.ReadCoils(address, quantity);
                    if (coils == null)
                    {
                        throw new HandleException(ExceptionType.E_NO_VALID_INPUT,
                            $"读线圈失败：{driver.LastError}");
                    }
                    RunParameter.SetOutput("Values", string.Join(",", coils.Select(c => c ? 1 : 0)));
                    RunParameter.ResultMessage = $"读取线圈 [{address}..{address + quantity - 1}]：{string.Join(", ", coils.Select(c => c ? 1 : 0))}";
                    break;
                }

                case ModbusFunction.WriteSingleRegister:
                {
                    var value = (ushort)Math.Clamp((int)double.Parse(Interpolate(p.WriteValue.Trim())), 0, 65535);
                    var ok = AsTcp(driver)?.WriteSingleRegister(address, value)
                             ?? AsRtu(driver)?.WriteSingleRegister(address, value);
                    if (ok != true)
                    {
                        throw new HandleException(ExceptionType.E_NO_VALID_INPUT,
                            $"写寄存器 {address} 失败：{driver.LastError}");
                    }
                    RunParameter.ResultMessage = $"已写入寄存器 {address} = {value}";
                    break;
                }

                case ModbusFunction.WriteSingleCoil:
                {
                    var raw = Interpolate(p.WriteValue.Trim());
                    var on = raw is "1" or "true" or "True" or "ON";
                    var ok = AsTcp(driver)?.WriteSingleCoil(address, on)
                             ?? AsRtu(driver)?.WriteSingleCoil(address, on);
                    if (ok != true)
                    {
                        throw new HandleException(ExceptionType.E_NO_VALID_INPUT,
                            $"写线圈 {address} 失败：{driver.LastError}");
                    }
                    RunParameter.ResultMessage = $"已写入线圈 {address} = {(on ? 1 : 0)}";
                    break;
                }

                case ModbusFunction.WriteMultipleRegisters:
                {
                    var parts = Interpolate(p.WriteValue).Split(',', StringSplitOptions.TrimEntries);
                    var values = parts.Select(x => (ushort)Math.Clamp((int)double.Parse(x), 0, 65535)).ToArray();
                    var ok = AsTcp(driver)?.WriteMultipleRegisters(address, values)
                             ?? AsRtu(driver)?.WriteMultipleRegisters(address, values);
                    if (ok != true)
                    {
                        throw new HandleException(ExceptionType.E_NO_VALID_INPUT,
                            $"写多寄存器 {address}..{address + values.Length - 1} 失败：{driver.LastError}");
                    }
                    RunParameter.ResultMessage = $"已写入 {values.Length} 个寄存器 [{address}..]: {string.Join(", ", values)}";
                    break;
                }
            }
        }

        private static ModbusTcpDriver AsTcp(ICommunicationDriver driver) => driver as ModbusTcpDriver;

        private static ModbusRtuDriver AsRtu(ICommunicationDriver driver) => driver as ModbusRtuDriver;

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
