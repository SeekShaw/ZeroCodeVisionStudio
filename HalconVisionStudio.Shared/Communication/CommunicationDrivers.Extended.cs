using System;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HalconVisionStudio.Shared.Communication
{
    /// <summary>虚拟回环驱动：发送的数据（可设延迟）立即进入接收缓冲，无硬件即可调试通讯流程。</summary>
    public sealed class LoopbackDriver : CommunicationDriverBase
    {
        private new LoopbackDriverConfig Config => (LoopbackDriverConfig)base.Config;

        public LoopbackDriver(LoopbackDriverConfig config) : base(config) { }

        public override bool IsConnected => true;

        public override bool Connect() => true;

        public override void Disconnect() { }

        public override bool Send(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                LastError = "发送内容为空";
                return false;
            }

            ReportSent(data);
            if (Config.DelayMs > 0)
            {
                Task.Run(async () =>
                {
                    await Task.Delay(Config.DelayMs);
                    OnDataReceived(data);
                });
            }
            else
            {
                OnDataReceived(data);
            }
            return true;
        }
    }

    /// <summary>
    /// Modbus TCP 主站驱动：长连接 + 事务号匹配，支持读/写保持寄存器与线圈。
    /// 自实现 MBAP+PDU 协议（复用项目既有组帧逻辑，无第三方依赖）。
    /// </summary>
    public sealed class ModbusTcpDriver : CommunicationDriverBase
    {
        private TcpClient client;
        private NetworkStream stream;
        private int transactionId;

        private new ModbusTcpDriverConfig Config => (ModbusTcpDriverConfig)base.Config;

        public ModbusTcpDriver(ModbusTcpDriverConfig config) : base(config) { }

        public override bool IsConnected => client?.Connected == true;

        public override bool Connect()
        {
            lock (ioLock)
            {
                if (IsConnected) return true;
                DisconnectCore();
                try
                {
                    var newClient = new TcpClient();
                    if (!newClient.ConnectAsync(Config.Host, Config.Port).Wait(Config.ConnectTimeoutMs))
                    {
                        newClient.Close();
                        LastError = $"连接超时 {Config.Host}:{Config.Port}";
                        ReportError();
                        return false;
                    }
                    client = newClient;
                    stream = client.GetStream();
                    stream.ReadTimeout = Config.ResponseTimeoutMs;
                    stream.WriteTimeout = Config.ResponseTimeoutMs;
                    LastError = "";
                    return true;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    ReportError();
                    return false;
                }
            }
        }

        public override void Disconnect()
        {
            lock (ioLock) DisconnectCore();
        }

        private void DisconnectCore()
        {
            try { stream?.Close(); } catch { }
            try { client?.Close(); } catch { }
            stream = null;
            client = null;
        }

        public override bool Send(byte[] data)
        {
            LastError = "Modbus 驱动不支持裸字节发送，请使用 ModbusRead/ModbusWrite 算子";
            return false;
        }

        // ==================== Modbus 功能码 ====================
        private const byte FuncReadHolding = 0x03;
        private const byte FuncReadCoils = 0x01;
        private const byte FuncWriteSingleRegister = 0x06;
        private const byte FuncWriteSingleCoil = 0x05;
        private const byte FuncWriteMultipleRegisters = 0x10;

        /// <summary>读保持寄存器，返回 ushort 数组。</summary>
        public ushort[] ReadHoldingRegisters(ushort startAddress, ushort count)
            => ReadRegisters(FuncReadHolding, startAddress, count);

        /// <summary>读线圈，返回 bool 数组。</summary>
        public bool[] ReadCoils(ushort startAddress, ushort count)
        {
            var raw = ReadRegisters(FuncReadCoils, startAddress, count);
            if (raw == null) return null;
            var result = new bool[count];
            for (int i = 0; i < count; i++)
                result[i] = (raw[i / 16] & (1 << (i % 16))) != 0;
            return result;
        }

        /// <summary>写单个保持寄存器。</summary>
        public bool WriteSingleRegister(ushort address, ushort value)
        {
            var pdu = new byte[] { FuncWriteSingleRegister, (byte)(address >> 8), (byte)address, (byte)(value >> 8), (byte)value };
            var resp = Transact(pdu);
            return resp != null;
        }

        /// <summary>写单线圈。</summary>
        public bool WriteSingleCoil(ushort address, bool value)
        {
            var pdu = new byte[] { FuncWriteSingleCoil, (byte)(address >> 8), (byte)address, (byte)(value ? 0xFF : 0x00), 0x00 };
            var resp = Transact(pdu);
            return resp != null;
        }

        /// <summary>写多个保持寄存器。</summary>
        public bool WriteMultipleRegisters(ushort startAddress, ushort[] values)
        {
            var pdu = new byte[6 + values.Length * 2];
            pdu[0] = FuncWriteMultipleRegisters;
            pdu[1] = (byte)(startAddress >> 8);
            pdu[2] = (byte)startAddress;
            pdu[3] = (byte)(values.Length >> 8);
            pdu[4] = (byte)values.Length;
            pdu[5] = (byte)(values.Length * 2);
            for (int i = 0; i < values.Length; i++)
            {
                pdu[6 + i * 2] = (byte)(values[i] >> 8);
                pdu[7 + i * 2] = (byte)values[i];
            }
            var resp = Transact(pdu);
            return resp != null;
        }

        private ushort[] ReadRegisters(byte funcCode, ushort startAddress, ushort count)
        {
            if (!EnsureConnected()) return null;
            var pdu = new byte[5];
            pdu[0] = funcCode;
            pdu[1] = (byte)(startAddress >> 8);
            pdu[2] = (byte)startAddress;
            pdu[3] = (byte)(count >> 8);
            pdu[4] = (byte)count;
            var resp = Transact(pdu);
            if (resp == null || resp.Length < 2 || resp[1] != funcCode) return null;

            var byteCount = resp[2];
            var result = new ushort[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = (ushort)((resp[3 + i * 2] << 8) | resp[4 + i * 2]);
            }
            return result;
        }

        /// <summary>执行一次 MBAP+PDU 事务（事务号匹配校验）。</summary>
        private byte[] Transact(byte[] pdu)
        {
            lock (ioLock)
            {
                if (!EnsureConnectedLocked()) return null;
                try
                {
                    var tid = (ushort)Interlocked.Increment(ref transactionId);
                    var frame = new byte[7 + pdu.Length];
                    frame[0] = (byte)(tid >> 8);
                    frame[1] = (byte)tid;
                    frame[2] = 0; // 协议标识
                    frame[3] = 0;
                    frame[4] = (byte)((pdu.Length + 1) >> 8);
                    frame[5] = (byte)(pdu.Length + 1);
                    frame[6] = Config.UnitId;
                    Array.Copy(pdu, 0, frame, 7, pdu.Length);

                    stream.Write(frame, 0, frame.Length);
                    ReportSent(frame);

                    // 读 MBAP 头 8 字节 + 余下数据
                    var header = ReadExact(8);
                    if (header == null) return null;
                    var respTid = (header[0] << 8) | header[1];
                    if (respTid != tid)
                    {
                        LastError = $"Modbus 事务号不匹配 ({respTid} != {tid})";
                        ReportError();
                        return null;
                    }
                    var length = (header[4] << 8) | header[5];
                    var remaining = length - 2; // 减 UnitId + 功能码
                    if (remaining <= 0)
                    {
                        LastError = "Modbus 响应长度非法";
                        ReportError();
                        return null;
                    }
                    var rest = ReadExact(1 + remaining);
                    if (rest == null) return null;

                    var payload = new byte[1 + remaining];
                    payload[0] = header[6]; // UnitId
                    Array.Copy(rest, 0, payload, 1, remaining);

                    OnDataReceived(payload); // 计入接收统计
                    if ((payload[1] & 0x80) != 0)
                    {
                        LastError = $"Modbus 从站异常码 0x{(payload.Length > 2 ? payload[2] : (byte)0):X2}";
                        ReportError();
                        return null;
                    }
                    LastError = "";
                    return payload;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    ReportError();
                    DisconnectCore();
                    return null;
                }
            }
        }

        private bool EnsureConnected() { lock (ioLock) return EnsureConnectedLocked(); }

        private bool EnsureConnectedLocked()
        {
            if (IsConnected) return true;
            return Connect();
        }

        private byte[] ReadExact(int count)
        {
            var buffer = new byte[count];
            var offset = 0;
            while (offset < count)
            {
                var read = stream.Read(buffer, offset, count - offset);
                if (read == 0) throw new IOException("对端关闭连接");
                offset += read;
            }
            return buffer;
        }
    }

    /// <summary>Modbus RTU 主站驱动：独立串口 + CRC16 校验（08 功能码集：读03/05/06/10）。</summary>
    public sealed class ModbusRtuDriver : CommunicationDriverBase
    {
        private SerialPort port;

        private new ModbusRtuDriverConfig Config => (ModbusRtuDriverConfig)base.Config;

        public ModbusRtuDriver(ModbusRtuDriverConfig config) : base(config) { }

        public override bool IsConnected => port?.IsOpen == true;

        public override bool Connect()
        {
            lock (ioLock)
            {
                if (port?.IsOpen == true) return true;
                DisconnectCore();
                try
                {
                    port = new SerialPort(Config.PortName, Config.BaudRate,
                        (Parity)(int)Config.Parity, Config.DataBits, (StopBits)(int)Config.StopBits)
                    {
                        ReadTimeout = Config.ResponseTimeoutMs,
                        WriteTimeout = Config.ResponseTimeoutMs
                    };
                    port.Open();
                    LastError = "";
                    return true;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    ReportError();
                    DisconnectCore();
                    return false;
                }
            }
        }

        public override void Disconnect()
        {
            lock (ioLock) DisconnectCore();
        }

        private void DisconnectCore()
        {
            try { if (port?.IsOpen == true) port.Close(); } catch { }
            try { port?.Dispose(); } catch { }
            port = null;
        }

        public override bool Send(byte[] data)
        {
            LastError = "Modbus RTU 驱动不支持裸字节发送，请使用 ModbusRead/ModbusWrite 算子";
            return false;
        }

        public ushort[] ReadHoldingRegisters(ushort startAddress, ushort count)
        {
            var raw = TransactRtu(BuildRtuPdu(0x03, startAddress, count), expectBytes: 3 + count * 2);
            return raw == null ? null : ToRegisters(raw, count);
        }

        public bool[] ReadCoils(ushort startAddress, ushort count)
        {
            var byteCount = (count + 7) / 8;
            var raw = TransactRtu(BuildRtuPdu(0x01, startAddress, count), expectBytes: 3 + byteCount);
            if (raw == null) return null;
            var result = new bool[count];
            for (int i = 0; i < count; i++)
                result[i] = (raw[3 + i / 8] & (1 << (i % 8))) != 0;
            return result;
        }

        public bool WriteSingleRegister(ushort address, ushort value)
        {
            var pdu = new byte[] { 0x06, (byte)(address >> 8), (byte)address, (byte)(value >> 8), (byte)value };
            return TransactRtu(pdu, expectBytes: 5) != null;
        }

        public bool WriteSingleCoil(ushort address, bool value)
        {
            var pdu = new byte[] { 0x05, (byte)(address >> 8), (byte)address, (byte)(value ? 0xFF : 0x00), 0x00 };
            return TransactRtu(pdu, expectBytes: 5) != null;
        }

        public bool WriteMultipleRegisters(ushort startAddress, ushort[] values)
        {
            var pdu = new byte[6 + values.Length * 2];
            pdu[0] = 0x10;
            pdu[1] = (byte)(startAddress >> 8);
            pdu[2] = (byte)startAddress;
            pdu[3] = (byte)(values.Length >> 8);
            pdu[4] = (byte)values.Length;
            pdu[5] = (byte)(values.Length * 2);
            for (int i = 0; i < values.Length; i++)
            {
                pdu[6 + i * 2] = (byte)(values[i] >> 8);
                pdu[7 + i * 2] = (byte)values[i];
            }
            return TransactRtu(pdu, expectBytes: 5) != null;
        }

        private static byte[] BuildRtuPdu(byte func, ushort start, ushort count) => new byte[]
        {
            func, (byte)(start >> 8), (byte)start, (byte)(count >> 8), (byte)count
        };

        private ushort[] ToRegisters(byte[] resp, int count)
        {
            var result = new ushort[count];
            for (int i = 0; i < count; i++)
                result[i] = (ushort)((resp[3 + i * 2] << 8) | resp[4 + i * 2]);
            return result;
        }

        /// <summary>RTU 事务：发 [从站号+PDU+CRC16]，收 [从站号+PDU+CRC16]。</summary>
        private byte[] TransactRtu(byte[] pdu, int expectBytes)
        {
            lock (ioLock)
            {
                if (port?.IsOpen != true && !Connect()) return null;
                try
                {
                    // 清残留
                    try
                    {
                        while (port.BytesToRead > 0) port.ReadByte();
                    }
                    catch { }

                    var frame = new byte[1 + pdu.Length + 2];
                    frame[0] = Config.SlaveId;
                    Array.Copy(pdu, 0, frame, 1, pdu.Length);
                    var crc = Crc16Modbus(frame, frame.Length - 2);
                    frame[frame.Length - 2] = (byte)(crc & 0xFF);
                    frame[frame.Length - 1] = (byte)(crc >> 8);

                    port.Write(frame, 0, frame.Length);
                    ReportSent(frame);

                    // 读响应：从站号+功能码+数据+CRC
                    var resp = new byte[1 + expectBytes + 2];
                    var offset = 0;
                    var deadline = Environment.TickCount64 + Config.ResponseTimeoutMs;
                    while (offset < resp.Length && Environment.TickCount64 < deadline)
                    {
                        if (port.BytesToRead > 0)
                        {
                            resp[offset++] = (byte)port.ReadByte();
                        }
                        else
                        {
                            Thread.Sleep(2);
                        }
                    }

                    if (offset < resp.Length)
                    {
                        LastError = "Modbus RTU 响应超时";
                        ReportError();
                        return null;
                    }

                    var respCrc = Crc16Modbus(resp, resp.Length - 2);
                    if ((resp[resp.Length - 2] & 0xFF) != (respCrc & 0xFF) ||
                        resp[resp.Length - 1] != (byte)(respCrc >> 8))
                    {
                        LastError = "Modbus RTU CRC 校验失败";
                        ReportError();
                        return null;
                    }

                    OnDataReceived(resp);
                    if ((resp[1] & 0x80) != 0)
                    {
                        LastError = $"Modbus 从站异常码 0x{(resp.Length > 2 ? resp[2] : 0):X2}";
                        ReportError();
                        return null;
                    }
                    LastError = "";
                    return resp;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    ReportError();
                    return null;
                }
            }
        }

        /// <summary>Modbus CRC16（多项式 0xA001）。</summary>
        public static ushort Crc16Modbus(byte[] data, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < length; i++)
            {
                crc ^= data[i];
                for (int b = 0; b < 8; b++)
                {
                    if ((crc & 0x0001) != 0) { crc >>= 1; crc ^= 0xA001; }
                    else crc >>= 1;
                }
            }
            return crc;
        }
    }
}
