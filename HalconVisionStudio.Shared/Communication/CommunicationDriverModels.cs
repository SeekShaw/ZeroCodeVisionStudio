using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HalconVisionStudio.Shared.Communication
{
    /// <summary>通讯驱动类型。</summary>
    public enum CommDriverType
    {
        TcpClient,
        TcpServer,
        Udp,
        Serial,
        Loopback,
        ModbusTcp,
        ModbusRtu,
        Mqtt,
        WebSocket,
        S7,
        MelsecMc,
        Fins
    }

    /// <summary>
    /// 通讯驱动配置基类。
    /// 所有驱动配置集中持久化（%APPDATA%\HalconVisionStudio\communication_drivers.json），
    /// 通讯算子按 <see cref="Name"/> 引用，改配置无需改流程。
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(TcpClientDriverConfig), "tcpClient")]
    [JsonDerivedType(typeof(TcpServerDriverConfig), "tcpServer")]
    [JsonDerivedType(typeof(UdpDriverConfig), "udp")]
    [JsonDerivedType(typeof(SerialDriverConfig), "serial")]
    [JsonDerivedType(typeof(LoopbackDriverConfig), "loopback")]
    [JsonDerivedType(typeof(ModbusTcpDriverConfig), "modbusTcp")]
    [JsonDerivedType(typeof(ModbusRtuDriverConfig), "modbusRtu")]
    [JsonDerivedType(typeof(MqttDriverConfig), "mqtt")]
    [JsonDerivedType(typeof(WebSocketDriverConfig), "ws")]
    [JsonDerivedType(typeof(S7DriverConfig), "s7")]
    [JsonDerivedType(typeof(McDriverConfig), "melsecMc")]
    [JsonDerivedType(typeof(FinsDriverConfig), "fins")]
    [JsonDerivedType(typeof(LoopbackDriverConfig), "loopback")]
    public abstract class CommDriverConfig
    {
        /// <summary>唯一标识（新建时生成，持久化保留）。</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>驱动名称：通讯算子引用此名称（同批配置内唯一）。</summary>
        public string Name { get; set; } = "新驱动";

        /// <summary>备注说明（用途/设备）。</summary>
        public string Description { get; set; } = "";

        /// <summary>配置修订号：每次保存递增，用于驱动实例缓存判断是否需要重建连接。</summary>
        public int Revision { get; set; }

        /// <summary>应用启动时自动建立连接。</summary>
        public bool AutoConnect { get; set; } = false;

        /// <summary>收到数据时触发指定流程执行（事件驱动工位节拍：PLC 给信号→拍照检测）。</summary>
        public bool TriggerEnabled { get; set; } = false;

        /// <summary>接收触发执行的流程名（空=当前活动流程）。</summary>
        public string TriggerWorkflowName { get; set; } = "";

        /// <summary>驱动类型。</summary>
        public abstract CommDriverType Type { get; }

        /// <summary>参数摘要（列表展示用）。</summary>
        public abstract string Summary { get; }
    }

    /// <summary>TCP 客户端配置：主动连接远端设备（PLC/传感器/上位机）。</summary>
    public class TcpClientDriverConfig : CommDriverConfig
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 8080;
        /// <summary>连接超时（毫秒）。</summary>
        public int ConnectTimeoutMs { get; set; } = 3000;
        /// <summary>发送时若连接已断开是否自动重连。</summary>
        public bool AutoReconnect { get; set; } = true;

        public override CommDriverType Type => CommDriverType.TcpClient;
        public override string Summary => $"{Host}:{Port}";
    }

    /// <summary>TCP 服务端配置：监听端口，接受多个客户端（下位机主动连入）。</summary>
    public class TcpServerDriverConfig : CommDriverConfig
    {
        /// <summary>监听端口。</summary>
        public int ListenPort { get; set; } = 9000;
        /// <summary>最大客户端数。</summary>
        public int MaxClients { get; set; } = 4;

        public override CommDriverType Type => CommDriverType.TcpServer;
        public override string Summary => $"监听 0.0.0.0:{ListenPort}";
    }

    /// <summary>UDP 配置：绑定本地端口收，向远端发（广播支持 255.255.255.255）。</summary>
    public class UdpDriverConfig : CommDriverConfig
    {
        /// <summary>本地绑定端口（收）。</summary>
        public int LocalPort { get; set; } = 9001;
        /// <summary>远端地址（发）。</summary>
        public string RemoteHost { get; set; } = "127.0.0.1";
        /// <summary>远端端口（发）。</summary>
        public int RemotePort { get; set; } = 9002;

        public override CommDriverType Type => CommDriverType.Udp;
        public override string Summary => $"本地:{LocalPort} → {RemoteHost}:{RemotePort}";
    }

    /// <summary>串口配置。</summary>
    public class SerialDriverConfig : CommDriverConfig
    {
        /// <summary>校验位。</summary>
        public enum SerialParity
        {
            无校验 = 0,
            奇校验 = 1,
            偶校验 = 2
        }

        /// <summary>停止位。</summary>
        public enum SerialStopBits
        {
            一位 = 1,
            两位 = 2
        }

        public string PortName { get; set; } = "COM1";
        public int BaudRate { get; set; } = 9600;
        public SerialParity Parity { get; set; } = SerialParity.无校验;
        public int DataBits { get; set; } = 8;
        public SerialStopBits StopBits { get; set; } = SerialStopBits.一位;

        public override CommDriverType Type => CommDriverType.Serial;
        public override string Summary => $"{PortName} @{BaudRate},{DataBits},{Parity},{StopBits}";

        /// <summary>校验位索引（界面 ComboBox SelectedIndex 绑定桥接，不持久化）。</summary>
        [JsonIgnore]
        public int ParityIndex
        {
            get => (int)Parity;
            set { if (value >= 0) Parity = (SerialParity)value; }
        }

        /// <summary>停止位索引（界面桥接，不持久化）。</summary>
        [JsonIgnore]
        public int StopBitsIndex
        {
            get => (int)StopBits - 1;
            set { if (value >= 0) StopBits = (SerialStopBits)(value + 1); }
        }
    }

    /// <summary>虚拟回环驱动：发送的数据立即进入接收缓冲（无硬件开发调试流程用）。</summary>
    public class LoopbackDriverConfig : CommDriverConfig
    {
        /// <summary>模拟接收延迟（毫秒）。</summary>
        public int DelayMs { get; set; } = 0;

        public override CommDriverType Type => CommDriverType.Loopback;
        public override string Summary => "发送即接收（无硬件调试用）";
    }

    /// <summary>Modbus TCP 主站驱动：长连接事务化读写保持寄存器/线圈。</summary>
    public class ModbusTcpDriverConfig : CommDriverConfig
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 502;
        /// <summary>从站单元号（UnitId）。</summary>
        public byte UnitId { get; set; } = 1;
        public int ConnectTimeoutMs { get; set; } = 3000;
        public int ResponseTimeoutMs { get; set; } = 2000;

        public override CommDriverType Type => CommDriverType.ModbusTcp;
        public override string Summary => $"{Host}:{Port} 单元{UnitId}";
    }

    /// <summary>Modbus RTU 主站驱动：独立串口 + CRC16。</summary>
    public class ModbusRtuDriverConfig : CommDriverConfig
    {
        public string PortName { get; set; } = "COM1";
        public int BaudRate { get; set; } = 9600;
        public SerialDriverConfig.SerialParity Parity { get; set; } = SerialDriverConfig.SerialParity.无校验;
        public int DataBits { get; set; } = 8;
        public SerialDriverConfig.SerialStopBits StopBits { get; set; } = SerialDriverConfig.SerialStopBits.一位;
        /// <summary>从站地址。</summary>
        public byte SlaveId { get; set; } = 1;
        public int ResponseTimeoutMs { get; set; } = 500;

        public override CommDriverType Type => CommDriverType.ModbusRtu;
        public override string Summary => $"{PortName}@{BaudRate} 从站{SlaveId}";

        /// <summary>校验位索引桥接（界面绑定）。</summary>
        [JsonIgnore]
        public int ParityIndex
        {
            get => (int)Parity;
            set { if (value >= 0) Parity = (SerialDriverConfig.SerialParity)value; }
        }

        /// <summary>停止位索引桥接（界面绑定）。</summary>
        [JsonIgnore]
        public int StopBitsIndex
        {
            get => (int)StopBits - 1;
            set { if (value >= 0) StopBits = (SerialDriverConfig.SerialStopBits)(value + 1); }
        }
    }

    /// <summary>MQTT 客户端驱动：发布到 Topic；订阅 Topic 的消息进入接收缓冲。</summary>
    public class MqttDriverConfig : CommDriverConfig
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 1883;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string ClientId { get; set; } = "halcon-vision-studio";
        /// <summary>发布主题（发送数据的目标 Topic）。</summary>
        public string PublishTopic { get; set; } = "vision/result";
        /// <summary>订阅主题（# 通配），订阅到的消息进入接收缓冲。</summary>
        public string SubscribeTopic { get; set; } = "vision/command";

        public override CommDriverType Type => CommDriverType.Mqtt;
        public override string Summary => $"{Host}:{Port} 发:{PublishTopic} 订:{SubscribeTopic}";
    }

    /// <summary>WebSocket 客户端驱动：连接 ws/wss 服务端，收发文本/二进制帧。</summary>
    public class WebSocketDriverConfig : CommDriverConfig
    {
        /// <summary>服务端地址（ws:// 或 wss://）。</summary>
        public string Url { get; set; } = "ws://127.0.0.1:8080/";

        public override CommDriverType Type => CommDriverType.WebSocket;
        public override string Summary => Url;
    }
}
