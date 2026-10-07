using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using S7.Net;

namespace HalconVisionStudio.Shared.Communication
{
    /// <summary>
    /// PLC 值读写统一接口：PlcRead / PlcWrite 算子面向此接口编程，与品牌协议解耦。
    /// type 取值：bit / int16 / uint16 / int32 / uint32 / float。
    /// </summary>
    public interface IPlcValueDriver
    {
        /// <summary>读取地址当前值（连接断开时自动重连）。失败抛异常或返回 null。</summary>
        object ReadValue(string address, string type);

        /// <summary>写值到地址。返回是否成功（失败原因见 LastError）。</summary>
        bool WriteValue(string address, string type, object value);
    }

    /// <summary>PLC 值类型编解码辅助（字节长度、标准化）。</summary>
    internal static class PlcValueCodec
    {
        public static string Normalize(string type) => (type ?? "").Trim().ToLowerInvariant();

        public static bool IsBit(string type) => Normalize(type) == "bit";

        /// <summary>类型字节长度（bit 按字节对齐参与位运算，返回 1）。</summary>
        public static int ByteSize(string type) => Normalize(type) switch
        {
            "int16" => 2,
            "uint16" => 2,
            "int32" => 4,
            "uint32" => 4,
            "float" => 4,
            _ => 1
        };

        public static void Validate(string type)
        {
            var t = Normalize(type);
            if (t is not ("bit" or "int16" or "uint16" or "int32" or "uint32" or "float"))
                throw new NotSupportedException($"不支持的 PLC 数据类型: {type}");
        }
    }

    // ====================================================================================
    // 西门子 S7（S7netplus 库）
    // ====================================================================================

    /// <summary>
    /// 西门子 S7 驱动：基于 S7netplus 长连接读写 S7-200 Smart / 300 / 400 / 1200 / 1500。
    /// 地址：DB10.DBW0 / DB10.DBD4 / DB10.DBX0.1 / MW100 / MD100 / M100.3 / I50 / Q80。
    /// </summary>
    public sealed class S7Driver : CommunicationDriverBase, IPlcValueDriver
    {
        private Plc plc;

        private new S7DriverConfig Config => (S7DriverConfig)base.Config;

        private static readonly CpuType[] CpuMap =
        {
            CpuType.S7200, CpuType.S7200Smart, CpuType.S7300,
            CpuType.S7400, CpuType.S71200, CpuType.S71500
        };

        public S7Driver(S7DriverConfig config) : base(config) { }

        public override bool IsConnected => plc?.IsConnected == true;

        public override bool Connect()
        {
            lock (ioLock)
            {
                if (IsConnected) return true;
                DisconnectCore();
                try
                {
                    var cpu = CpuMap[Config.CpuIndexClamped];
                    plc = new Plc(cpu, Config.Host, Config.Port, (short)Config.Rack, (short)Config.Slot)
                    {
                        ReadTimeout = Config.ConnectTimeoutMs,
                        WriteTimeout = Config.ConnectTimeoutMs
                    };
                    plc.Open();
                    if (!plc.IsConnected)
                    {
                        LastError = $"S7 连接失败 {Config.Host}:{Config.Port}";
                        ReportError();
                        DisconnectCore();
                        return false;
                    }
                    ReportPlcText($"S7 已连接 {Config.Host} ({cpu} 机架{Config.Rack} 槽{Config.Slot})", true);
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
            try { plc?.Close(); } catch { }
            plc = null;
        }

        public override bool Send(byte[] data)
        {
            LastError = "S7 驱动不支持裸字节发送，请使用 PLC读取 / PLC写入 算子";
            return false;
        }

        public object ReadValue(string address, string type)
        {
            PlcValueCodec.Validate(type);
            lock (ioLock)
            {
                EnsureConnectedOrThrow();
                var a = ParseAddress(address);
                try
                {
                    object result;
                    if (PlcValueCodec.IsBit(type))
                    {
                        var raw = plc.ReadBytes(a.Area, a.Db, a.Byte, 1);
                        result = raw != null && raw.Length > 0 && (raw[0] & (1 << a.Bit)) != 0;
                    }
                    else
                    {
                        var size = PlcValueCodec.ByteSize(type);
                        var raw = plc.ReadBytes(a.Area, a.Db, a.Byte, size);
                        result = DecodeBe(raw, PlcValueCodec.Normalize(type));
                    }

                    if (result == null)
                    {
                        LastError = $"S7 读取 {address} 无有效响应";
                        ReportError();
                        return null;
                    }
                    LastError = "";
                    ReportPlcText($"S7 读 {address} = {result}", true);
                    return result;
                }
                catch (Exception ex)
                {
                    LastError = $"S7 读取 {address} 失败: {ex.Message}";
                    ReportError();
                    DisconnectCore();
                    return null;
                }
            }
        }

        public bool WriteValue(string address, string type, object value)
        {
            PlcValueCodec.Validate(type);
            lock (ioLock)
            {
                EnsureConnectedOrThrow();
                var a = ParseAddress(address);
                try
                {
                    if (PlcValueCodec.IsBit(type))
                    {
                        // 位写入：读-改-写该字节（兼容任意位号，不依赖库的批量位接口）。
                        var on = Convert.ToBoolean(Convert.ToDouble(value, CultureInfo.InvariantCulture));
                        var raw = plc.ReadBytes(a.Area, a.Db, a.Byte, 1);
                        if (raw == null || raw.Length == 0) { LastError = "S7 位写入前读取失败"; ReportError(); return false; }
                        var b = on ? (byte)(raw[0] | (1 << a.Bit)) : (byte)(raw[0] & ~(1 << a.Bit));
                        plc.WriteBytes(a.Area, a.Db, a.Byte, new[] { b });
                    }
                    else
                    {
                        plc.WriteBytes(a.Area, a.Db, a.Byte, EncodeBe(value, PlcValueCodec.Normalize(type)));
                    }
                    LastError = "";
                    ReportPlcText($"S7 写 {address} = {value}", false);
                    return true;
                }
                catch (Exception ex)
                {
                    LastError = $"S7 写入 {address} 失败: {ex.Message}";
                    ReportError();
                    DisconnectCore();
                    return false;
                }
            }
        }

        private void EnsureConnectedOrThrow()
        {
            if (!IsConnected && !Connect())
                throw new InvalidOperationException($"S7 驱动 [{Config.Name}] 未连接: {LastError}");
        }

        /// <summary>上报文本化收发记录到报文监视器（库驱动拿不到原始字节，用操作摘要替代）。</summary>
        private void ReportPlcText(string text, bool isRx)
        {
            CommunicationCenter.ReportMessage(Config.Name, isRx, Encoding.UTF8.GetBytes(text));
        }

        // ==================== 地址解析 ====================

        private readonly record struct S7Address(DataType Area, int Db, int Byte, int Bit);

        /// <summary>
        /// 解析 S7 地址：DB10.DBX0.1 / DB10.DBW0 / DB10.10 / MW100 / MD100 / M100 / M100.3 / I50 / Q80。
        /// 尺寸字母（X/B/W/D）仅用于定位字节偏移，实际读写长度由算子的数据类型决定。
        /// </summary>
        private static S7Address ParseAddress(string address)
        {
            var s = (address ?? "").Trim().ToUpperInvariant();
            if (s.Length < 2)
                throw new FormatException($"S7 地址 [{address}] 无效（示例：DB10.DBW0 / MW100 / M100.3 / I50 / Q80）");

            if (s.StartsWith("DB", StringComparison.Ordinal))
            {
                var dot = s.IndexOf('.');
                if (dot < 3)
                    throw new FormatException($"S7 DB 地址 [{address}] 无效（示例：DB10.DBW0 / DB10.DBX0.1）");
                var db = int.Parse(s.Substring(2, dot - 2), CultureInfo.InvariantCulture);
                var rest = s.Substring(dot + 1);
                if (rest.StartsWith("DB", StringComparison.Ordinal)) rest = rest.Substring(2);
                return FinishParse(DataType.DataBlock, db, rest, address);
            }

            var area = s[0] switch
            {
                'M' => DataType.Memory,
                'I' => DataType.Input,
                'Q' => DataType.Output,
                _ => throw new FormatException($"S7 地址 [{address}] 无效：不支持的区域 [{s[0]}]（支持 DB / M / I / Q）")
            };
            return FinishParse(area, 0, s.Substring(1), address);
        }

        private static S7Address FinishParse(DataType area, int db, string rest, string origin)
        {
            // 去掉可选尺寸字母 X/B/W/D（其后必须跟数字，避免把十六进制误删）。
            if (rest.Length > 1 && rest[0] is 'X' or 'B' or 'W' or 'D' && char.IsDigit(rest[1]))
                rest = rest.Substring(1);

            var bit = 0;
            var dot = rest.IndexOf('.');
            if (dot >= 0)
            {
                bit = int.Parse(rest.Substring(dot + 1), CultureInfo.InvariantCulture);
                if (bit is < 0 or > 7)
                    throw new FormatException($"S7 位号 [{origin}] 超出 0~7");
                rest = rest.Substring(0, dot);
            }
            if (rest.Length == 0)
                throw new FormatException($"S7 地址 [{origin}] 无效");
            var byteAddress = int.Parse(rest, CultureInfo.InvariantCulture);
            return new S7Address(area, db, byteAddress, bit);
        }

        // ==================== 大端编解码（S7 线序） ====================

        private static object DecodeBe(byte[] raw, string type)
        {
            var size = PlcValueCodec.ByteSize(type);
            if (raw == null || raw.Length < size) return null;
            return type switch
            {
                "int16" => (short)((raw[0] << 8) | raw[1]),
                "uint16" => (ushort)((raw[0] << 8) | raw[1]),
                "int32" => (raw[0] << 24) | (raw[1] << 16) | (raw[2] << 8) | raw[3],
                "uint32" => (uint)((raw[0] << 24) | (raw[1] << 16) | (raw[2] << 8) | raw[3]),
                "float" => BitConverter.Int32BitsToSingle(
                    (raw[0] << 24) | (raw[1] << 16) | (raw[2] << 8) | raw[3]),
                _ => null
            };
        }

        private static byte[] EncodeBe(object value, string type)
        {
            var d = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return type switch
            {
                "int16" => Be2((short)d),
                "uint16" => Be2((ushort)d),
                "int32" => Be4((int)d),
                "uint32" => Be4((uint)d),
                "float" => Be4(BitConverter.SingleToInt32Bits((float)d)),
                _ => throw new NotSupportedException(type)
            };
        }

        private static byte[] Be2(int v) => new[] { (byte)(v >> 8), (byte)v };
        private static byte[] Be4(long v) => new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };
    }

    // ====================================================================================
    // 三菱 MELSEC MC（4E 二进制帧，自实现）
    // ====================================================================================

    /// <summary>
    /// 三菱 MC 协议驱动（4E 二进制帧）：Q/L 系列、FX5U 等以太网模块。
    /// 地址：D/W/R（字，十进制）、M（位，十进制）、X/Y/B（位，十六进制编号）。
    /// 帧序注意：帧头多字节字段大端，但器件号为 3 字节小端、字数据小端。
    /// </summary>
    public sealed class McDriver : CommunicationDriverBase, IPlcValueDriver
    {
        private TcpClient client;
        private NetworkStream stream;

        private new McDriverConfig Config => (McDriverConfig)base.Config;

        public McDriver(McDriverConfig config) : base(config) { }

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
                    if (!newClient.ConnectAsync(Config.Host, Config.Port).Wait(Config.TimeoutMs))
                    {
                        newClient.Close();
                        LastError = $"MC 连接超时 {Config.Host}:{Config.Port}";
                        ReportError();
                        return false;
                    }
                    client = newClient;
                    stream = client.GetStream();
                    stream.ReadTimeout = Config.TimeoutMs;
                    stream.WriteTimeout = Config.TimeoutMs;
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
            try { stream?.Close(); } catch { }
            try { client?.Close(); } catch { }
            stream = null;
            client = null;
        }

        public override bool Send(byte[] data)
        {
            LastError = "MC 驱动不支持裸字节发送，请使用 PLC读取 / PLC写入 算子";
            return false;
        }

        // ==================== 器件表与地址解析 ====================

        private readonly record struct McDevice(byte CodeHi, byte CodeLo, int Number, bool IsBit);

        /// <summary>器件码（ASCII 2B）+ 是否位器件 + 编号是否十六进制。</summary>
        private static readonly Dictionary<char, (string Code, bool IsBit, bool IsHex)> Devices = new()
        {
            ['D'] = ("D*", false, false),
            ['W'] = ("W*", false, false),
            ['R'] = ("R*", false, false),
            ['M'] = ("M*", true, false),
            ['X'] = ("X*", true, true),
            ['Y'] = ("Y*", true, true),
            ['B'] = ("B*", true, true),
        };

        private static McDevice ParseDevice(string address)
        {
            var s = (address ?? "").Trim().ToUpperInvariant();
            if (s.Length < 2)
                throw new FormatException($"MC 地址 [{address}] 无效（示例：D100 / W100 / M100 / X1A / Y10 / B0）");
            if (!Devices.TryGetValue(s[0], out var meta))
                throw new FormatException($"MC 不支持器件 [{s[0]}*]（支持 D / W / R / M / X / Y / B）");
            var numText = s.Substring(1);
            var number = meta.IsHex
                ? int.Parse(numText, NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : int.Parse(numText, CultureInfo.InvariantCulture);
            if (number > 0xFFFFFF)
                throw new FormatException($"MC 器件号 [{address}] 超出 24 位范围");
            return new McDevice((byte)meta.Code[0], (byte)meta.Code[1], number, meta.IsBit);
        }

        // ==================== 4E 帧事务 ====================

        /// <summary>
        /// 执行一次 4E 帧事务：返回响应数据（结束码之后的载荷），失败返回 null。
        /// request = 命令(2B) + 子命令(2B) + 器件码(2B) + 器件号(3B 小端) + 计数/数据。
        /// </summary>
        private byte[] Transact(byte[] request)
        {
            lock (ioLock)
            {
                if (!IsConnected && !Connect())
                    throw new InvalidOperationException($"MC 驱动 [{Config.Name}] 未连接: {LastError}");
                try
                {
                    var frame = new byte[11 + request.Length];
                    frame[0] = 0x50; frame[1] = 0x00;                 // 4E 子帧头
                    frame[2] = (byte)Config.NetworkNo;                 // 网络号
                    frame[3] = (byte)Config.PlcNo;                     // PLC 号
                    frame[4] = (byte)(Config.ModuleIo >> 8);           // 目标模块 IO 号（大端）
                    frame[5] = (byte)Config.ModuleIo;
                    frame[6] = (byte)Config.StationNo;                 // 目标站号
                    frame[7] = (byte)(request.Length >> 8);            // 请求数据长度（大端）
                    frame[8] = (byte)request.Length;
                    frame[9] = (byte)(Config.TimeoutMs >> 8);          // 监视定时器（大端）
                    frame[10] = (byte)Config.TimeoutMs;
                    Array.Copy(request, 0, frame, 11, request.Length);

                    stream.Write(frame, 0, frame.Length);
                    ReportSent(frame);

                    // 响应头 9B：D0 00 + 网络 + PLC + IO(2B) + 站 + 响应数据长度(2B，含结束码)。
                    var head = ReadExact(9);
                    if (head[0] != 0xD0 || head[1] != 0x00)
                    {
                        LastError = $"MC 响应子帧头异常 {head[0]:X2} {head[1]:X2}";
                        ReportError();
                        DisconnectCore();
                        return null;
                    }
                    var respLen = (head[7] << 8) | head[8];
                    var payload = ReadExact(respLen);
                    OnDataReceived(head); // 响应计入接收统计（含头便于对帧）

                    var endCode = (payload[0] << 8) | payload[1];
                    if (endCode != 0)
                    {
                        LastError = $"MC 结束码 0x{endCode:X4}（参考三菱手册错误代码）";
                        ReportError();
                        return null;
                    }
                    LastError = "";
                    return payload.Length > 2 ? payload[2..] : Array.Empty<byte>();
                }
                catch (Exception ex)
                {
                    LastError = $"MC 通讯失败: {ex.Message}";
                    ReportError();
                    DisconnectCore();
                    return null;
                }
            }
        }

        /// <summary>按字单位读（返回小端字数组）。</summary>
        private ushort[] ReadWordsCore(McDevice dev, int wordCount)
        {
            if (dev.IsBit) throw new FormatException("MC 位器件（M/X/Y/B）不支持字读取，请用 Bit 类型");
            var req = new byte[11];
            req[0] = 0x04; req[1] = 0x01;                            // 批量读
            req[2] = 0x00; req[3] = 0x00;                             // 子命令：字单位
            req[4] = dev.CodeHi; req[5] = dev.CodeLo;                 // 器件码（ASCII）
            req[6] = (byte)(dev.Number & 0xFF);                       // 器件号 3B 小端
            req[7] = (byte)((dev.Number >> 8) & 0xFF);
            req[8] = (byte)((dev.Number >> 16) & 0xFF);
            req[9] = (byte)(wordCount >> 8);                          // 器件数（大端）
            req[10] = (byte)wordCount;

            var data = Transact(req);
            if (data == null || data.Length < 2 + wordCount * 2) return null;
            var words = new ushort[wordCount];
            for (var i = 0; i < wordCount; i++)
                words[i] = (ushort)(data[2 + i * 2] | (data[3 + i * 2] << 8));
            return words;
        }

        /// <summary>按位单位读（每位 1 字节 0x00/0x01）。</summary>
        private bool[] ReadBitsCore(McDevice dev, int count)
        {
            if (!dev.IsBit) throw new FormatException($"MC 字器件不支持位读取，请用 Int16/UInt16 等类型");
            var req = new byte[11];
            req[0] = 0x04; req[1] = 0x01;                            // 批量读
            req[2] = 0x00; req[3] = 0x01;                             // 子命令：位单位
            req[4] = dev.CodeHi; req[5] = dev.CodeLo;
            req[6] = (byte)(dev.Number & 0xFF);
            req[7] = (byte)((dev.Number >> 8) & 0xFF);
            req[8] = (byte)((dev.Number >> 16) & 0xFF);
            req[9] = (byte)(count >> 8);
            req[10] = (byte)count;

            var data = Transact(req);
            if (data == null || data.Length < 2 + count) return null;
            var bits = new bool[count];
            for (var i = 0; i < count; i++)
                bits[i] = data[2 + i] != 0;
            return bits;
        }

        private bool WriteWordsCore(McDevice dev, ushort[] words)
        {
            if (dev.IsBit) throw new FormatException("MC 位器件不支持字写入，请用 Bit 类型");
            var req = new byte[9 + words.Length * 2];
            req[0] = 0x14; req[1] = 0x01;                             // 批量写
            req[2] = 0x00; req[3] = 0x00;                             // 子命令：字单位
            req[4] = dev.CodeHi; req[5] = dev.CodeLo;
            req[6] = (byte)(dev.Number & 0xFF);
            req[7] = (byte)((dev.Number >> 8) & 0xFF);
            req[8] = (byte)((dev.Number >> 16) & 0xFF);
            for (var i = 0; i < words.Length; i++)                    // 字数据小端
            {
                req[9 + i * 2] = (byte)(words[i] & 0xFF);
                req[10 + i * 2] = (byte)(words[i] >> 8);
            }
            return Transact(req) != null;
        }

        private bool WriteBitsCore(McDevice dev, bool[] bits)
        {
            if (!dev.IsBit) throw new FormatException("MC 字器件不支持位写入");
            var req = new byte[9 + bits.Length];
            req[0] = 0x14; req[1] = 0x01;                             // 批量写
            req[2] = 0x00; req[3] = 0x01;                             // 子命令：位单位
            req[4] = dev.CodeHi; req[5] = dev.CodeLo;
            req[6] = (byte)(dev.Number & 0xFF);
            req[7] = (byte)((dev.Number >> 8) & 0xFF);
            req[8] = (byte)((dev.Number >> 16) & 0xFF);
            for (var i = 0; i < bits.Length; i++)
                req[9 + i] = (byte)(bits[i] ? 1 : 0);
            return Transact(req) != null;
        }

        // ==================== IPlcValueDriver ====================

        public object ReadValue(string address, string type)
        {
            PlcValueCodec.Validate(type);
            var dev = ParseDevice(address);
            var t = PlcValueCodec.Normalize(type);

            if (t == "bit")
            {
                var bits = ReadBitsCore(dev, 1);
                if (bits == null) return null;
                return bits[0];
            }

            var wordCount = PlcValueCodec.ByteSize(t) / 2;
            var words = ReadWordsCore(dev, wordCount);
            if (words == null) return null;
            return DecodeLe(words, t);
        }

        public bool WriteValue(string address, string type, object value)
        {
            PlcValueCodec.Validate(type);
            var dev = ParseDevice(address);
            var t = PlcValueCodec.Normalize(type);
            try
            {
                if (t == "bit")
                    return WriteBitsCore(dev, new[] { Convert.ToBoolean(Convert.ToDouble(value, CultureInfo.InvariantCulture)) });

                var wordCount = PlcValueCodec.ByteSize(t) / 2;
                var d = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                var words = t switch
                {
                    "int16" => new[] { (ushort)(short)d },
                    "uint16" => new[] { (ushort)d },
                    "int32" => new[] { (ushort)((int)d & 0xFFFF), (ushort)(((int)d >> 16) & 0xFFFF) },
                    "uint32" => new[] { (ushort)((uint)d & 0xFFFF), (ushort)(((uint)d >> 16) & 0xFFFF) },
                    "float" => SplitFloatLe((float)d),
                    _ => throw new NotSupportedException(t)
                };
                return WriteWordsCore(dev, words);
            }
            catch (FormatException ex)
            {
                LastError = $"MC 写入值 [{value}] 无法转换为 {type}: {ex.Message}";
                return false;
            }
            catch (InvalidOperationException)
            {
                throw; // 连接失败异常上抛给算子
            }
        }

        private static ushort[] SplitFloatLe(float f)
        {
            var bits = BitConverter.SingleToInt32Bits(f);
            return new[] { (ushort)(bits & 0xFFFF), (ushort)((bits >> 16) & 0xFFFF) };
        }

        private static object DecodeLe(ushort[] words, string type) => type switch
        {
            "int16" => (short)words[0],
            "uint16" => words[0],
            "int32" => (words[0] | (words[1] << 16)),
            "uint32" => (uint)(words[0] | (words[1] << 16)),
            "float" => BitConverter.Int32BitsToSingle(words[0] | (words[1] << 16)),
            _ => null
        };

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

    // ====================================================================================
    // 欧姆龙 FINS/TCP（自实现）
    // ====================================================================================

    /// <summary>
    /// 欧姆龙 FINS/TCP 驱动：连接时节点地址握手，数据命令 0x00000002 透传 FINS 帧。
    /// 地址：D100（DM）、W100（WR）、H100（HR）、CIO100 / C100（CIO），位用 .位号（如 CIO100.05 / D100.3）。
    /// 全帧大端。
    /// </summary>
    public sealed class FinsDriver : CommunicationDriverBase, IPlcValueDriver
    {
        private TcpClient client;
        private NetworkStream stream;
        private byte localNode;
        private byte destNode;
        private byte serviceId;

        private new FinsDriverConfig Config => (FinsDriverConfig)base.Config;

        public FinsDriver(FinsDriverConfig config) : base(config) { }

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
                    if (!newClient.ConnectAsync(Config.Host, Config.Port).Wait(Config.TimeoutMs))
                    {
                        newClient.Close();
                        LastError = $"FINS 连接超时 {Config.Host}:{Config.Port}";
                        ReportError();
                        return false;
                    }
                    client = newClient;
                    stream = client.GetStream();
                    stream.ReadTimeout = Config.TimeoutMs;
                    stream.WriteTimeout = Config.TimeoutMs;

                    if (!HandshakeNodeAssign())
                    {
                        DisconnectCore();
                        return false;
                    }
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
            try { stream?.Close(); } catch { }
            try { client?.Close(); } catch { }
            stream = null;
            client = null;
        }

        public override bool Send(byte[] data)
        {
            LastError = "FINS 驱动不支持裸字节发送，请使用 PLC读取 / PLC写入 算子";
            return false;
        }

        /// <summary>FINS/TCP 节点地址分配握手：命令 0x00000000，响应 0x00000001 返回双方节点号。</summary>
        private bool HandshakeNodeAssign()
        {
            var requested = Config.LocalNode < 0 ? 0 : Config.LocalNode;
            var body = new byte[10];
            body[4] = (byte)(requested >> 8);   // 期望的本站节点号（0=由 PLC 分配）
            body[5] = (byte)requested;

            var resp = TcpTransact(0x00000000, body);
            if (resp == null || resp.Length < 10)
            {
                LastError = string.IsNullOrEmpty(LastError) ? "FINS 节点握手无响应" : LastError;
                ReportError();
                return false;
            }

            var err = (resp[4] << 24) | (resp[5] << 16) | (resp[6] << 8) | resp[7];
            if (err != 0)
            {
                LastError = $"FINS 节点分配被拒绝 (错误码 0x{err:X8})";
                ReportError();
                return false;
            }

            // 响应体：命令(4B) + 错误(4B) + 客户端节点(2B) + 服务器节点(2B) —— 共 12B。
            localNode = (byte)((resp[8] << 8) | resp[9]);                 // PLC 分配/确认的本站节点
            var serverNode = resp.Length >= 12 ? (byte)((resp[10] << 8) | resp[11]) : (byte)0;
            destNode = Config.DestNode >= 0 ? (byte)Config.DestNode : serverNode;
            ReportPlcText($"FINS 已连接 {Config.Host}（本站节点 {localNode}，PLC 节点 {destNode}）");
            return true;
        }

        /// <summary>FINS/TCP 帧事务：magic"FINS" + 长度(4B BE) + 命令(4B) + 错误(4B) + 载荷。返回去掉命令/错误字段后的载荷。</summary>
        private byte[] TcpTransact(uint command, byte[] payload)
        {
            if (!IsConnected)
                throw new InvalidOperationException($"FINS 驱动 [{Config.Name}] 未连接: {LastError}");

            var packet = new byte[16 + payload.Length];
            packet[0] = (byte)'F'; packet[1] = (byte)'I'; packet[2] = (byte)'N'; packet[3] = (byte)'S';
            var len = 8 + payload.Length;                                // 长度 = 命令+错误+载荷
            packet[4] = (byte)(len >> 24); packet[5] = (byte)(len >> 16);
            packet[6] = (byte)(len >> 8); packet[7] = (byte)len;
            packet[8] = (byte)(command >> 24); packet[9] = (byte)(command >> 16);
            packet[10] = (byte)(command >> 8); packet[11] = (byte)command;
            // 12~15 错误码 0
            Array.Copy(payload, 0, packet, 16, payload.Length);

            stream.Write(packet, 0, packet.Length);
            ReportSent(packet);

            var head = ReadExact(8);
            if (head[0] != 'F' || head[1] != 'I' || head[2] != 'N' || head[3] != 'S')
            {
                LastError = "FINS 响应魔数错误";
                ReportError();
                DisconnectCore();
                return null;
            }
            var respLen = (head[4] << 24) | (head[5] << 16) | (head[6] << 8) | head[7];
            var body = ReadExact(respLen);
            OnDataReceived(head); // 响应计入接收统计

            var respCmd = ((uint)body[0] << 24) | ((uint)body[1] << 16) | ((uint)body[2] << 8) | body[3];
            var respErr = ((uint)body[4] << 24) | ((uint)body[5] << 16) | ((uint)body[6] << 8) | body[7];
            if (respErr != 0)
            {
                LastError = $"FINS/TCP 错误码 0x{respErr:X8}";
                ReportError();
                return null;
            }
            return body.Length > 8 ? body[8..] : Array.Empty<byte>();
        }

        /// <summary>组 FINS 帧头：ICF(0x80)/RSV/GCT(2)/DNA/DA1/DA2/SNA/SA1/SA2/SID。</summary>
        private byte[] FinsHeader()
        {
            serviceId++;
            return new byte[]
            {
                0x80, 0x00, 0x02,
                (byte)Config.DestNetwork, destNode, 0x00,
                0x00, localNode, 0x00,
                serviceId
            };
        }

        // ==================== 区域码与地址解析 ====================

        private readonly record struct FinsArea(byte WordCode, byte BitCode, int WordAddress, byte Bit);

        /// <summary>解析 FINS 地址：CIO100 / C100.05 / W100 / H100 / D100 / D100.3。</summary>
        private static FinsArea ParseFinsAddress(string address)
        {
            var s = (address ?? "").Trim().ToUpperInvariant();
            if (s.Length < 2)
                throw new FormatException($"FINS 地址 [{address}] 无效（示例：D100 / W100 / H100 / CIO100 / CIO100.05）");

            byte wordCode, bitCode;
            string numText;
            if (s.StartsWith("CIO", StringComparison.Ordinal)) { wordCode = 0xB0; bitCode = 0x30; numText = s.Substring(3); }
            else if (s[0] == 'C') { wordCode = 0xB0; bitCode = 0x30; numText = s.Substring(1); }
            else if (s[0] == 'W') { wordCode = 0xB1; bitCode = 0x91; numText = s.Substring(1); }
            else if (s[0] == 'H') { wordCode = 0xB2; bitCode = 0xB3; numText = s.Substring(1); }
            else if (s[0] == 'D') { wordCode = 0x82; bitCode = 0x02; numText = s.Substring(1); }
            else throw new FormatException($"FINS 不支持区域 [{s[0]}]（支持 CIO / W / H / D）");

            byte bit = 0;
            var dot = numText.IndexOf('.');
            if (dot >= 0)
            {
                bit = byte.Parse(numText.Substring(dot + 1), CultureInfo.InvariantCulture);
                if (bit > 15) throw new FormatException($"FINS 位号 [{address}] 超出 0~15");
                numText = numText.Substring(0, dot);
            }
            if (numText.Length == 0)
                throw new FormatException($"FINS 地址 [{address}] 无效");
            var wordAddress = int.Parse(numText, CultureInfo.InvariantCulture);
            if (wordAddress > 0xFFFFFF)
                throw new FormatException($"FINS 地址 [{address}] 超出范围");
            return new FinsArea(wordCode, bitCode, wordAddress, bit);
        }

        // ==================== 读写命令（01 01 读 / 01 02 写） ====================

        /// <summary>字单位读：FINS 0101 + 区码 + 字地址(3B BE) + 位(0) + 字数(2B BE)。返回大端字节数组。</summary>
        private byte[] ReadArea(byte areaCode, int wordAddress, int byteCount)
        {
            var wordCount = (byteCount + 1) / 2;
            var fins = new byte[19];
            var header = FinsHeader();
            Array.Copy(header, 0, fins, 0, 10);
            fins[10] = 0x01; fins[11] = 0x01;                          // 存储区读
            fins[12] = areaCode;
            fins[13] = (byte)(wordAddress >> 16);                      // 字地址 3B 大端
            fins[14] = (byte)(wordAddress >> 8);
            fins[15] = (byte)wordAddress;
            fins[16] = 0x00;                                           // 位号（字单位读固定 0）
            fins[17] = (byte)(wordCount >> 8);                         // 字数大端
            fins[18] = (byte)wordCount;

            var resp = FinsTransact(fins);
            if (resp == null || resp.Length < 4 + wordCount * 2) return null;
            return resp[4..];                                          // 去掉 命令2+结束码2
        }

        /// <summary>位单位读：区码用位区码，计数=位数，每位 1 字节。</summary>
        private bool[] ReadBits(byte bitAreaCode, int wordAddress, byte bitNumber, int count)
        {
            var fins = new byte[19];
            var header = FinsHeader();
            Array.Copy(header, 0, fins, 0, 10);
            fins[10] = 0x01; fins[11] = 0x01;
            fins[12] = bitAreaCode;
            fins[13] = (byte)(wordAddress >> 16);
            fins[14] = (byte)(wordAddress >> 8);
            fins[15] = (byte)wordAddress;
            fins[16] = bitNumber;                                      // 起始位号
            fins[17] = (byte)(count >> 8);
            fins[18] = (byte)count;

            var resp = FinsTransact(fins);
            if (resp == null || resp.Length < 4 + count) return null;
            var bits = new bool[count];
            for (var i = 0; i < count; i++)
                bits[i] = resp[4 + i] != 0;
            return bits;
        }

        private bool WriteArea(byte areaCode, int wordAddress, byte bitNumber, int unitCount, byte[] data, bool bitUnits)
        {
            var fins = new byte[19 + data.Length];
            var header = FinsHeader();
            Array.Copy(header, 0, fins, 0, 10);
            fins[10] = 0x01; fins[11] = 0x02;                          // 存储区写
            fins[12] = areaCode;
            fins[13] = (byte)(wordAddress >> 16);
            fins[14] = (byte)(wordAddress >> 8);
            fins[15] = (byte)wordAddress;
            fins[16] = bitNumber;
            fins[17] = (byte)(unitCount >> 8);
            fins[18] = (byte)unitCount;
            Array.Copy(data, 0, fins, 19, data.Length);

            var resp = FinsTransact(fins);
            return resp != null;
        }

        /// <summary>FINS 帧透传并校验结束码（响应 = 头 + 命令2 + 结束码2 + 数据）。</summary>
        private byte[] FinsTransact(byte[] finsFrame)
        {
            lock (ioLock)
            {
                if (!IsConnected && !Connect())
                    throw new InvalidOperationException($"FINS 驱动 [{Config.Name}] 未连接: {LastError}");
                try
                {
                    var resp = TcpTransact(0x00000002, finsFrame);
                    if (resp == null || resp.Length < 4) return null;

                    var endCode = (resp[2] << 8) | resp[3];
                    if (endCode != 0)
                    {
                        LastError = $"FINS 结束码 0x{endCode:X4}（参考欧姆龙 W342 手册）";
                        ReportError();
                        return null;
                    }
                    LastError = "";
                    return resp;
                }
                catch (Exception ex)
                {
                    LastError = $"FINS 通讯失败: {ex.Message}";
                    ReportError();
                    DisconnectCore();
                    return null;
                }
            }
        }

        // ==================== IPlcValueDriver ====================

        public object ReadValue(string address, string type)
        {
            PlcValueCodec.Validate(type);
            var a = ParseFinsAddress(address);
            var t = PlcValueCodec.Normalize(type);

            if (t == "bit")
            {
                var bits = ReadBits(a.BitCode, a.WordAddress, a.Bit, 1);
                if (bits == null) return null;
                return bits[0];
            }

            var size = PlcValueCodec.ByteSize(t);
            var raw = ReadArea(a.WordCode, a.WordAddress, size);
            if (raw == null || raw.Length < size) return null;
            return DecodeBe2(raw, t);
        }

        public bool WriteValue(string address, string type, object value)
        {
            PlcValueCodec.Validate(type);
            var a = ParseFinsAddress(address);
            var t = PlcValueCodec.Normalize(type);
            try
            {
                if (t == "bit")
                {
                    var on = Convert.ToBoolean(Convert.ToDouble(value, CultureInfo.InvariantCulture));
                    return WriteArea(a.BitCode, a.WordAddress, a.Bit, 1, new[] { (byte)(on ? 1 : 0) }, true);
                }

                var data = EncodeBe2(value, t);
                return WriteArea(a.WordCode, a.WordAddress, 0, data.Length / 2, data, false);
            }
            catch (FormatException ex)
            {
                LastError = $"FINS 写入值 [{value}] 无法转换: {ex.Message}";
                return false;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
        }

        /// <summary>上报文本化收发记录到报文监视器。</summary>
        private void ReportPlcText(string text)
        {
            CommunicationCenter.ReportMessage(Config.Name, true, Encoding.UTF8.GetBytes(text));
        }

        private static object DecodeBe2(byte[] raw, string type)
        {
            return type switch
            {
                "int16" => (short)((raw[0] << 8) | raw[1]),
                "uint16" => (ushort)((raw[0] << 8) | raw[1]),
                "int32" => (raw[0] << 24) | (raw[1] << 16) | (raw[2] << 8) | raw[3],
                "uint32" => (uint)((raw[0] << 24) | (raw[1] << 16) | (raw[2] << 8) | raw[3]),
                "float" => BitConverter.Int32BitsToSingle((raw[0] << 24) | (raw[1] << 16) | (raw[2] << 8) | raw[3]),
                _ => null
            };
        }

        private static byte[] EncodeBe2(object value, string type)
        {
            var d = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return type switch
            {
                "int16" => Be2s((short)d),
                "uint16" => Be2s((ushort)d),
                "int32" => Be4s((int)d),
                "uint32" => Be4s((uint)d),
                "float" => Be4s(BitConverter.SingleToInt32Bits((float)d)),
                _ => throw new NotSupportedException(type)
            };
        }

        private static byte[] Be2s(int v) => new[] { (byte)(v >> 8), (byte)v };
        private static byte[] Be4s(long v) => new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };

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
}
