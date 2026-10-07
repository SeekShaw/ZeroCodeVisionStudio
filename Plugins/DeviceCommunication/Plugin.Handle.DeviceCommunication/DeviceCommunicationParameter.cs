using System.ComponentModel;
using HalconVisionStudio.Shared;
using HalconVisionStudio.Shared.Communication;

namespace Plugin.Handle.DeviceCommunication
{
    public enum CommunicationTextEncoding
    {
        UTF8,
        ASCII,
        GBK,
        Hex
    }

    public enum ModbusWriteKind
    {
        HoldingRegister,
        Coil
    }

    public enum ModbusReadKind
    {
        HoldingRegister,
        InputRegister,
        Coil,
        DiscreteInput
    }

    public class TcpSendParameter : BindableBase, IHandleParameter
    {
        private string driverName = "";
        private string payload = "OK";
        private CommunicationTextEncoding encoding = CommunicationTextEncoding.UTF8;

        [DisplayName("通讯驱动")]
        [Description("从通讯驱动中心选择已配置的长连接驱动（TCP/UDP/串口/MQTT 等），连接参数在「工具→通讯驱动管理」中维护")]
        [DriverOptions(CommDriverType.TcpClient, CommDriverType.TcpServer, CommDriverType.Udp, CommDriverType.Serial, CommDriverType.Loopback, CommDriverType.Mqtt, CommDriverType.WebSocket)]
        public string DriverName { get => driverName; set => SetProperty(ref driverName, value); }

        [DisplayName("发送内容")]
        [Description("支持 $Key$ 上游插值；Hex 编码时填十六进制字符串")]
        public string Payload { get => payload; set => SetProperty(ref payload, value); }

        [DisplayName("编码")]
        public CommunicationTextEncoding Encoding { get => encoding; set => SetProperty(ref encoding, value); }

        public IHandleParameter Clone() => new TcpSendParameter { DriverName = DriverName, Payload = Payload, Encoding = Encoding };

        public void Reset()
        {
            DriverName = "";
            Payload = "OK";
            Encoding = CommunicationTextEncoding.UTF8;
        }
    }

    public class TcpReceiveParameter : BindableBase, IHandleParameter
    {
        private string driverName = "";
        private CommunicationTextEncoding encoding = CommunicationTextEncoding.UTF8;
        private int timeoutMs = 3000;

        [DisplayName("通讯驱动")]
        [Description("从通讯驱动中心选择已配置的长连接驱动，接收该驱动缓冲区中的数据")]
        [DriverOptions(CommDriverType.TcpClient, CommDriverType.TcpServer, CommDriverType.Udp, CommDriverType.Serial, CommDriverType.Loopback, CommDriverType.Mqtt, CommDriverType.WebSocket)]
        public string DriverName { get => driverName; set => SetProperty(ref driverName, value); }

        [DisplayName("编码")]
        public CommunicationTextEncoding Encoding { get => encoding; set => SetProperty(ref encoding, value); }

        [DisplayName("超时(ms)")]
        public int TimeoutMs { get => timeoutMs; set => SetProperty(ref timeoutMs, value); }

        public IHandleParameter Clone() => new TcpReceiveParameter
        {
            DriverName = DriverName,
            Encoding = Encoding,
            TimeoutMs = TimeoutMs
        };

        public void Reset()
        {
            DriverName = "";
            Encoding = CommunicationTextEncoding.UTF8;
            TimeoutMs = 3000;
        }
    }

    public class SerialSendParameter : BindableBase, IHandleParameter
    {
        private string driverName = "";
        private string payload = "OK";
        private CommunicationTextEncoding encoding = CommunicationTextEncoding.UTF8;

        [DisplayName("通讯驱动")]
        [Description("从通讯驱动中心选择串口驱动（串口号/波特率等在驱动管理中维护）")]
        [DriverOptions(CommDriverType.Serial, CommDriverType.Loopback)]
        public string DriverName { get => driverName; set => SetProperty(ref driverName, value); }

        [DisplayName("发送内容")]
        [Description("支持 $Key$ 上游插值；Hex 编码时填十六进制字符串")]
        public string Payload { get => payload; set => SetProperty(ref payload, value); }

        [DisplayName("编码")]
        public CommunicationTextEncoding Encoding { get => encoding; set => SetProperty(ref encoding, value); }

        public IHandleParameter Clone() => new SerialSendParameter
        {
            DriverName = DriverName,
            Payload = Payload,
            Encoding = Encoding
        };

        public void Reset()
        {
            DriverName = "";
            Payload = "OK";
            Encoding = CommunicationTextEncoding.UTF8;
        }
    }

    public class ModbusTcpWriteParameter : BindableBase, IHandleParameter
    {
        private string driverName = "";
        private ModbusWriteKind writeKind = ModbusWriteKind.HoldingRegister;
        private ushort address;
        private ushort value;
        private bool coilValue;

        [DisplayName("通讯驱动")]
        [Description("从通讯驱动中心选择 Modbus TCP / Modbus RTU 主站驱动（从站地址在驱动管理中维护）")]
        [DriverOptions(CommDriverType.ModbusTcp, CommDriverType.ModbusRtu)]
        public string DriverName { get => driverName; set => SetProperty(ref driverName, value); }

        [DisplayName("写入类型")]
        public ModbusWriteKind WriteKind { get => writeKind; set => SetProperty(ref writeKind, value); }

        [DisplayName("地址")]
        public ushort Address { get => address; set => SetProperty(ref address, value); }

        [DisplayName("寄存器值")]
        [Description("支持 $Key$ 上游插值")]
        public ushort Value { get => value; set => SetProperty(ref this.value, value); }

        [DisplayName("线圈值")]
        public bool CoilValue { get => coilValue; set => SetProperty(ref coilValue, value); }

        public IHandleParameter Clone() => new ModbusTcpWriteParameter
        {
            DriverName = DriverName,
            WriteKind = WriteKind,
            Address = Address,
            Value = Value,
            CoilValue = CoilValue
        };

        public void Reset()
        {
            DriverName = "";
            WriteKind = ModbusWriteKind.HoldingRegister;
            Address = 0;
            Value = 0;
            CoilValue = false;
        }
    }

    public class ModbusTcpReadParameter : BindableBase, IHandleParameter
    {
        private string driverName = "";
        private ModbusReadKind readKind = ModbusReadKind.HoldingRegister;
        private ushort address;
        private ushort count = 1;

        [DisplayName("通讯驱动")]
        [Description("从通讯驱动中心选择 Modbus TCP / Modbus RTU 主站驱动（从站地址在驱动管理中维护）")]
        [DriverOptions(CommDriverType.ModbusTcp, CommDriverType.ModbusRtu)]
        public string DriverName { get => driverName; set => SetProperty(ref driverName, value); }

        [DisplayName("读取类型")]
        public ModbusReadKind ReadKind { get => readKind; set => SetProperty(ref readKind, value); }

        [DisplayName("起始地址")]
        public ushort Address { get => address; set => SetProperty(ref address, value); }

        [DisplayName("数量")]
        public ushort Count { get => count; set => SetProperty(ref count, value); }

        public IHandleParameter Clone() => new ModbusTcpReadParameter
        {
            DriverName = DriverName,
            ReadKind = ReadKind,
            Address = Address,
            Count = Count
        };

        public void Reset()
        {
            DriverName = "";
            ReadKind = ModbusReadKind.HoldingRegister;
            Address = 0;
            Count = 1;
        }
    }
}
