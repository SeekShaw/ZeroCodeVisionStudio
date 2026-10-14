using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ZeroCodeVisionStudio.Shared;
using ZeroCodeVisionStudio.Shared.Communication;

namespace Plugin.Handle.DeviceCommunication
{
    /// <summary>
    /// 遗留通讯算子基类（TCP发送/TCP接收/串口发送/Modbus读写）：
    /// 已全部迁移至通讯驱动中心 —— 参数只选择驱动名称，
    /// 连接参数（IP/端口/串口/从站地址）统一在「工具→通讯驱动管理」维护，长连接复用。
    /// </summary>
    public abstract class CommunicationHandleBase<TParameter> : ParameterHandleBase<TParameter>, IHandleDataSlots
        where TParameter : IHandleParameter, new()
    {
        public const string HostInputKey = "communication.host";
        public const string PortInputKey = "communication.port";
        public const string PayloadInputKey = "communication.payload";
        public const string TimeoutInputKey = "communication.timeout";
        public const string SuccessOutputKey = "communication.success";
        public const string MessageOutputKey = "communication.message";
        public const string BytesSentOutputKey = "communication.bytesSent";
        public const string ReceivedDataOutputKey = "communication.receivedData";

        protected CommunicationHandleBase(IDialogService dialogService) : base(dialogService)
        {
        }

        public virtual IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(PayloadInputKey, "发送内容", typeof(object)),
            new RunSlot(TimeoutInputKey, "超时(ms)", typeof(int))
        };

        public virtual IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(SuccessOutputKey, "是否成功", typeof(bool)),
            new RunSlot(MessageOutputKey, "通讯消息", typeof(string)),
            new RunSlot(BytesSentOutputKey, "发送字节数", typeof(int))
        };

        protected string ReadString(string key, string fallback)
        {
            if (RunParameter?.TryGetInput<object>(key, out var value) == true && value != null)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            return fallback;
        }

        protected int ReadInt(string key, int fallback)
        {
            if (RunParameter?.TryGetInput<object>(key, out var value) == true && value != null)
            {
                if (value is IConvertible convertible)
                {
                    return convertible.ToInt32(CultureInfo.InvariantCulture);
                }

                if (int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }

            return fallback;
        }

        protected ushort ReadUShort(string key, ushort fallback)
        {
            var value = ReadInt(key, fallback);
            if (value < 0 || value > ushort.MaxValue)
            {
                throw new InvalidOperationException($"{key} 必须在 0 到 {ushort.MaxValue} 之间。");
            }

            return (ushort)value;
        }

        protected bool ReadBool(string key, bool fallback)
        {
            if (RunParameter?.TryGetInput<object>(key, out var value) == true && value != null)
            {
                if (value is bool boolean)
                {
                    return boolean;
                }

                var text = value.ToString();
                if (bool.TryParse(text, out var parsedBool))
                {
                    return parsedBool;
                }

                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt))
                {
                    return parsedInt != 0;
                }
            }

            return fallback;
        }

        protected static byte[] EncodePayload(string payload, CommunicationTextEncoding encoding)
        {
            payload ??= string.Empty;
            return encoding switch
            {
                CommunicationTextEncoding.ASCII => Encoding.ASCII.GetBytes(payload),
                CommunicationTextEncoding.GBK => Encoding.GetEncoding("GBK").GetBytes(payload),
                CommunicationTextEncoding.Hex => ParseHex(payload),
                _ => Encoding.UTF8.GetBytes(payload)
            };
        }

        protected static string DecodePayload(byte[] bytes, int count, CommunicationTextEncoding encoding)
        {
            if (bytes == null || count <= 0) return string.Empty;
            return encoding switch
            {
                CommunicationTextEncoding.ASCII => Encoding.ASCII.GetString(bytes, 0, count),
                CommunicationTextEncoding.GBK => Encoding.GetEncoding("GBK").GetString(bytes, 0, count),
                CommunicationTextEncoding.Hex => BitConverter.ToString(bytes, 0, count).Replace("-", " "),
                _ => Encoding.UTF8.GetString(bytes, 0, count)
            };
        }

        protected void SetResult(bool success, string message, int bytesSent)
        {
            // 业务结果标记：false 时节点状态显示"失败"（软失败，不中断流程）
            RunParameter.Success = success;
            RunParameter.SetOutput(SuccessOutputKey, success);
            RunParameter.SetOutput(MessageOutputKey, message);
            RunParameter.SetOutput(BytesSentOutputKey, bytesSent);
            RunParameter.ResultMessage = message;
        }

        /// <summary>
        /// 按参数中的驱动名取驱动中心实例并确保连接。
        /// 返回 null 时已写入失败结果（未配置驱动/连接失败），调用方直接 return。
        /// </summary>
        protected ICommunicationDriver ResolveConnectedDriver(string driverName)
        {
            if (string.IsNullOrWhiteSpace(driverName))
            {
                SetResult(false, "未选择通讯驱动：请到「工具→通讯驱动管理」配置后在参数下拉中选择。", 0);
                return null;
            }

            var driver = CommunicationCenter.GetDriver(driverName);
            if (driver == null)
            {
                SetResult(false, $"未找到通讯驱动 [{driverName}]，请到「工具→通讯驱动管理」检查。", 0);
                return null;
            }

            if (!driver.IsConnected && !driver.Connect())
            {
                SetResult(false, $"驱动 [{driverName}] 连接失败：{driver.LastError}", 0);
                return null;
            }

            return driver;
        }

        private static byte[] ParseHex(string hex)
        {
            var normalized = new string((hex ?? string.Empty).Where(Uri.IsHexDigit).ToArray());
            if (normalized.Length % 2 != 0)
            {
                throw new InvalidOperationException("Hex 发送内容必须是偶数个十六进制字符。");
            }

            var bytes = new byte[normalized.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(normalized.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        /// <summary>获取异常的根消息（解开 AggregateException 等包装）。</summary>
        protected static string GetRootMessage(Exception ex)
        {
            while (ex is AggregateException aggregate && aggregate.InnerException != null)
            {
                ex = aggregate.InnerException;
            }

            return ex.Message;
        }
    }

    [Handle("TCP发送", "TcpSend", "通过通讯驱动中心的长连接驱动（TCP/UDP/串口/MQTT 等）发送文本或十六进制数据。发送内容支持 $Key$ 上游插值。", "设备通讯", OperatorBackend = OperatorBackend.Hybrid)]
    public class TcpSendHandle : CommunicationHandleBase<TcpSendParameter>
    {
        public TcpSendHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public override void Handle()
        {
            var payload = ReadString(PayloadInputKey, Parameter.Payload);
            byte[] bytes;
            try
            {
                bytes = EncodePayload(payload, Parameter.Encoding);
            }
            catch (Exception ex)
            {
                SetResult(false, $"发送内容编码失败：{ex.Message}", 0);
                return;
            }

            var driver = ResolveConnectedDriver(Parameter.DriverName);
            if (driver == null) return;

            try
            {
                var ok = driver.Send(bytes);
                SetResult(ok,
                    ok ? $"已通过 [{Parameter.DriverName}] 发送 {bytes.Length} 字节" : $"发送失败：{driver.LastError}",
                    ok ? bytes.Length : 0);
            }
            catch (Exception ex)
            {
                SetResult(false, $"发送失败：{GetRootMessage(ex)}", 0);
            }
        }
    }

    [Handle("TCP接收", "TcpReceive", "通过通讯驱动中心的长连接驱动接收数据（阻塞等待至超时），按编码转为文本输出。", "设备通讯", OperatorBackend = OperatorBackend.Hybrid)]
    public class TcpReceiveHandle : CommunicationHandleBase<TcpReceiveParameter>
    {
        public TcpReceiveHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public override IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(TimeoutInputKey, "超时(ms)", typeof(int))
        };

        public override IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(SuccessOutputKey, "是否成功", typeof(bool)),
            new RunSlot(ReceivedDataOutputKey, "接收文本", typeof(string)),
            new RunSlot(MessageOutputKey, "通讯消息", typeof(string))
        };

        public override void Handle()
        {
            var timeout = ReadInt(TimeoutInputKey, Parameter.TimeoutMs);
            var driver = ResolveConnectedDriver(Parameter.DriverName);
            if (driver == null) return;

            try
            {
                var data = driver.WaitForData(Math.Max(0, timeout));
                if (data.Length == 0)
                {
                    var message = $"在 {timeout}ms 内未从 [{Parameter.DriverName}] 接收到任何数据。";
                    RunParameter.Success = false;
                    RunParameter.SetOutput(SuccessOutputKey, false);
                    RunParameter.SetOutput(ReceivedDataOutputKey, string.Empty);
                    RunParameter.SetOutput(MessageOutputKey, message);
                    RunParameter.ResultMessage = message;
                    return;
                }

                var receivedText = DecodePayload(data, data.Length, Parameter.Encoding);
                RunParameter.SetOutput(SuccessOutputKey, true);
                RunParameter.SetOutput(ReceivedDataOutputKey, receivedText);
                var okMessage = $"接收成功 ({data.Length} 字节): {receivedText}";
                RunParameter.SetOutput(MessageOutputKey, okMessage);
                RunParameter.ResultMessage = okMessage;
            }
            catch (Exception ex)
            {
                var message = $"接收失败：{GetRootMessage(ex)}";
                RunParameter.SetOutput(SuccessOutputKey, false);
                RunParameter.SetOutput(ReceivedDataOutputKey, string.Empty);
                RunParameter.SetOutput(MessageOutputKey, message);
                RunParameter.ResultMessage = message;
            }
        }
    }

    [Handle("串口发送", "SerialSend", "通过通讯驱动中心的串口驱动发送文本或十六进制数据（串口参数在驱动管理中维护）。", "设备通讯", OperatorBackend = OperatorBackend.Hybrid)]
    public class SerialSendHandle : CommunicationHandleBase<SerialSendParameter>
    {
        public SerialSendHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public override void Handle()
        {
            var payload = ReadString(PayloadInputKey, Parameter.Payload);
            byte[] bytes;
            try
            {
                bytes = EncodePayload(payload, Parameter.Encoding);
            }
            catch (Exception ex)
            {
                SetResult(false, $"发送内容编码失败：{ex.Message}", 0);
                return;
            }

            var driver = ResolveConnectedDriver(Parameter.DriverName);
            if (driver == null) return;

            try
            {
                var ok = driver.Send(bytes);
                SetResult(ok,
                    ok ? $"已通过串口 [{Parameter.DriverName}] 发送 {bytes.Length} 字节" : $"串口发送失败：{driver.LastError}",
                    ok ? bytes.Length : 0);
            }
            catch (Exception ex)
            {
                SetResult(false, $"串口发送失败：{GetRootMessage(ex)}", 0);
            }
        }
    }

    [Handle("Modbus写入", "ModbusTcpWrite", "通过通讯驱动中心的 Modbus TCP / RTU 主站驱动写单个保持寄存器或单个线圈（从站地址在驱动管理中维护）。", "设备通讯", OperatorBackend = OperatorBackend.Hybrid)]
    public class ModbusTcpWriteHandle : CommunicationHandleBase<ModbusTcpWriteParameter>
    {
        public const string AddressInputKey = "communication.modbus.address";
        public const string ValueInputKey = "communication.modbus.value";
        public const string CoilValueInputKey = "communication.modbus.coilValue";

        public ModbusTcpWriteHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public override IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(AddressInputKey, "地址", typeof(int)),
            new RunSlot(ValueInputKey, "寄存器值", typeof(int)),
            new RunSlot(CoilValueInputKey, "线圈值", typeof(bool))
        };

        public override void Handle()
        {
            var address = ReadUShort(AddressInputKey, Parameter.Address);
            var registerValue = ReadUShort(ValueInputKey, Parameter.Value);
            var coilValue = ReadBool(CoilValueInputKey, Parameter.CoilValue);

            var driver = ResolveConnectedDriver(Parameter.DriverName);
            if (driver == null) return;

            var isCoil = Parameter.WriteKind == ModbusWriteKind.Coil;
            try
            {
                bool ok;
                if (isCoil)
                {
                    ok = (driver as ModbusTcpDriver)?.WriteSingleCoil(address, coilValue)
                         ?? (driver as ModbusRtuDriver)?.WriteSingleCoil(address, coilValue)
                         ?? throw new InvalidOperationException($"驱动 [{Parameter.DriverName}] 不是 Modbus 类型");
                }
                else
                {
                    ok = (driver as ModbusTcpDriver)?.WriteSingleRegister(address, registerValue)
                         ?? (driver as ModbusRtuDriver)?.WriteSingleRegister(address, registerValue)
                         ?? throw new InvalidOperationException($"驱动 [{Parameter.DriverName}] 不是 Modbus 类型");
                }

                SetResult(ok,
                    ok ? $"已写入 {(isCoil ? "线圈" : "寄存器")} {address} = {(isCoil ? (coilValue ? 1 : 0) : registerValue)}" : $"Modbus 写入失败：{driver.LastError}",
                    0);
            }
            catch (Exception ex)
            {
                SetResult(false, $"Modbus 写入失败：{GetRootMessage(ex)}", 0);
            }
        }
    }

    [Handle("Modbus读取", "ModbusTcpRead", "通过通讯驱动中心的 Modbus TCP / RTU 主站驱动读取保持寄存器或线圈（从站地址在驱动管理中维护）。", "设备通讯", OperatorBackend = OperatorBackend.Hybrid)]
    public class ModbusTcpReadHandle : CommunicationHandleBase<ModbusTcpReadParameter>
    {
        public const string AddressInputKey = "communication.modbus.address";
        public const string CountInputKey = "communication.modbus.count";
        public const string ValuesOutputKey = "communication.modbus.values";

        public ModbusTcpReadHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public override IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(AddressInputKey, "起始地址", typeof(int)),
            new RunSlot(CountInputKey, "读取数量", typeof(int))
        };

        public override IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(SuccessOutputKey, "是否成功", typeof(bool)),
            new RunSlot(ValuesOutputKey, "读取结果", typeof(int[])),
            new RunSlot(MessageOutputKey, "通讯消息", typeof(string))
        };

        public override void Handle()
        {
            var address = ReadUShort(AddressInputKey, Parameter.Address);
            var count = (ushort)Math.Max(1, ReadInt(CountInputKey, Parameter.Count));

            if (Parameter.ReadKind is ModbusReadKind.InputRegister or ModbusReadKind.DiscreteInput)
            {
                SetResult(false, "输入寄存器/离散输入功能码暂未支持：请选择保持寄存器或线圈，或改用「Modbus读写」算子。", 0);
                return;
            }

            var driver = ResolveConnectedDriver(Parameter.DriverName);
            if (driver == null) return;

            var isCoil = Parameter.ReadKind == ModbusReadKind.Coil;
            try
            {
                int[] valArr;
                if (isCoil)
                {
                    var coils = (driver as ModbusTcpDriver)?.ReadCoils(address, count)
                                ?? (driver as ModbusRtuDriver)?.ReadCoils(address, count)
                                ?? throw new InvalidOperationException($"驱动 [{Parameter.DriverName}] 不是 Modbus 类型");
                    valArr = coils?.Select(c => c ? 1 : 0).ToArray();
                }
                else
                {
                    var regs = (driver as ModbusTcpDriver)?.ReadHoldingRegisters(address, count)
                               ?? (driver as ModbusRtuDriver)?.ReadHoldingRegisters(address, count)
                               ?? throw new InvalidOperationException($"驱动 [{Parameter.DriverName}] 不是 Modbus 类型");
                    valArr = regs?.Select(r => (int)r).ToArray();
                }

                if (valArr == null)
                {
                    SetResult(false, $"Modbus 读取失败：{driver.LastError}", 0);
                    return;
                }

                RunParameter.SetOutput(SuccessOutputKey, true);
                RunParameter.SetOutput(ValuesOutputKey, valArr);
                var message = $"Modbus 读取 [{address}..{address + count - 1}] 成功：{string.Join(", ", valArr)}";
                RunParameter.SetOutput(MessageOutputKey, message);
                RunParameter.ResultMessage = message;
            }
            catch (Exception ex)
            {
                var message = $"Modbus 读取失败：{GetRootMessage(ex)}";
                RunParameter.SetOutput(SuccessOutputKey, false);
                RunParameter.SetOutput(ValuesOutputKey, Array.Empty<int>());
                RunParameter.SetOutput(MessageOutputKey, message);
                RunParameter.ResultMessage = message;
            }
        }
    }
}
