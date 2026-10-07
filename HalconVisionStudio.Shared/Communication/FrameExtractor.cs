using System;

namespace HalconVisionStudio.Shared.Communication
{
    /// <summary>帧提取模式：解决流式协议的粘包/半包。</summary>
    public enum FrameExtractMode
    {
        /// <summary>不提取：返回缓冲区全部数据（数据报协议适用）。</summary>
        [System.ComponentModel.Description("不提取(全部缓冲)")] 无 = 0,

        /// <summary>定长帧：凑满 N 字节取出一帧。</summary>
        [System.ComponentModel.Description("定长帧")] 定长 = 1,

        /// <summary>结束符帧：如文本协议的 \r\n。</summary>
        [System.ComponentModel.Description("结束符帧")] 结束符 = 2,

        /// <summary>帧头帧尾：0xAA ... 0x55 模式（十六进制配置）。</summary>
        [System.ComponentModel.Description("帧头帧尾")] 帧头帧尾 = 3,

        /// <summary>长度字段：帧头(2B) + 长度(1-2B) + 载荷。</summary>
        [System.ComponentModel.Description("长度字段帧")] 长度字段 = 4
    }

    /// <summary>
    /// 帧提取器：从接收缓冲字节流中按协议模式切出一帧，剩余字节留存。
    /// TryExtract 未凑满完整帧时返回 false 且不消费缓冲。
    /// </summary>
    public static class FrameExtractor
    {
        /// <summary>
        /// 尝试从缓冲中提取一帧。
        /// </summary>
        /// <param name="buffer">当前缓冲（调用方持有）。</param>
        /// <param name="mode">提取模式。</param>
        /// <param name="fixedLength">定长帧长度。</param>
        /// <param name="terminator">结束符（如 "\r\n"）。</param>
        /// <param name="headHex">帧头十六进制（如 "AA"）。</param>
        /// <param name="tailHex">帧尾十六进制（如 "55"）。</param>
        /// <param name="lengthFieldOffset">长度字段偏移（帧头后第几字节）。</param>
        /// <param name="lengthFieldSize">长度字段字节数（1 或 2）。</param>
        /// <param name="lengthIncludesHeader">长度值是否含帧头/长度字段本身。</param>
        /// <param name="frame">成功时输出完整一帧。</param>
        /// <param name="remainder">剩余缓冲。</param>
        public static bool TryExtract(
            byte[] buffer,
            FrameExtractMode mode,
            int fixedLength,
            string terminator,
            string headHex,
            string tailHex,
            int lengthFieldOffset,
            int lengthFieldSize,
            bool lengthIncludesHeader,
            out byte[] frame,
            out byte[] remainder)
        {
            frame = null;
            remainder = buffer ?? Array.Empty<byte>();
            if (buffer == null || buffer.Length == 0) return false;

            switch (mode)
            {
                case FrameExtractMode.定长:
                {
                    if (fixedLength <= 0 || buffer.Length < fixedLength) return false;
                    frame = new byte[fixedLength];
                    Array.Copy(buffer, frame, fixedLength);
                    remainder = CopyRest(buffer, fixedLength);
                    return true;
                }

                case FrameExtractMode.结束符:
                {
                    var term = CommDataHelper.ParseHex(ToHexLiteral(terminator));
                    if (term.Length == 0)
                    {
                        term = System.Text.Encoding.UTF8.GetBytes(terminator ?? "");
                    }
                    if (term.Length == 0) return false;

                    var index = IndexOf(buffer, term, 0);
                    if (index < 0) return false;
                    var frameLength = index + term.Length;
                    frame = new byte[frameLength];
                    Array.Copy(buffer, frame, frameLength);
                    remainder = CopyRest(buffer, frameLength);
                    return true;
                }

                case FrameExtractMode.帧头帧尾:
                {
                    var head = CommDataHelper.ParseHex(headHex ?? "");
                    var tail = CommDataHelper.ParseHex(tailHex ?? "");
                    if (head.Length == 0 || tail.Length == 0) return false;

                    var headIndex = IndexOf(buffer, head, 0);
                    if (headIndex < 0)
                    {
                        // 保留可能的半截帧头
                        remainder = KeepTailPartial(buffer, head);
                        return false;
                    }
                    var tailIndex = IndexOf(buffer, tail, headIndex + head.Length);
                    if (tailIndex < 0) return false;

                    var frameLength = tailIndex + tail.Length - headIndex;
                    frame = new byte[frameLength];
                    Array.Copy(buffer, headIndex, frame, 0, frameLength);
                    remainder = CopyRest(buffer, tailIndex + tail.Length);
                    return true;
                }

                case FrameExtractMode.长度字段:
                {
                    var head = CommDataHelper.ParseHex(headHex ?? "");
                    if (lengthFieldSize != 1 && lengthFieldSize != 2) return false;

                    var headIndex = head.Length > 0 ? IndexOf(buffer, head, 0) : 0;
                    if (head.Length > 0 && headIndex < 0)
                    {
                        remainder = KeepTailPartial(buffer, head);
                        return false;
                    }

                    var lengthPos = headIndex + head.Length + Math.Max(0, lengthFieldOffset);
                    if (buffer.Length < lengthPos + lengthFieldSize) return false;

                    int declared = lengthFieldSize == 1
                        ? buffer[lengthPos]
                        : (buffer[lengthPos] << 8) | buffer[lengthPos + 1];

                    var total = lengthIncludesHeader
                        ? declared
                        : declared + lengthPos + lengthFieldSize;

                    if (total <= 0 || total > 4096)
                    {
                        // 声明长度非法：丢弃到长度字段之后，避免死锁
                        remainder = CopyRest(buffer, lengthPos + lengthFieldSize);
                        return false;
                    }
                    if (buffer.Length < total) return false;

                    frame = new byte[total];
                    Array.Copy(buffer, headIndex >= 0 ? headIndex : 0, frame, 0, total);
                    remainder = CopyRest(buffer, (headIndex >= 0 ? headIndex : 0) + total);
                    return true;
                }

                case FrameExtractMode.无:
                default:
                    frame = buffer;
                    remainder = Array.Empty<byte>();
                    return true;
            }
        }

        private static byte[] CopyRest(byte[] buffer, int from)
        {
            if (from >= buffer.Length) return Array.Empty<byte>();
            var rest = new byte[buffer.Length - from];
            Array.Copy(buffer, from, rest, 0, rest.Length);
            return rest;
        }

        /// <summary>找不到帧头时，保留可能是帧头前缀的尾部字节（部分帧头）。</summary>
        private static byte[] KeepTailPartial(byte[] buffer, byte[] pattern)
        {
            for (var keep = Math.Min(pattern.Length - 1, buffer.Length); keep > 0; keep--)
            {
                var match = true;
                for (var i = 0; i < keep; i++)
                {
                    if (buffer[buffer.Length - keep + i] != pattern[i]) { match = false; break; }
                }
                if (match)
                {
                    var rest = new byte[keep];
                    Array.Copy(buffer, buffer.Length - keep, rest, 0, keep);
                    return rest;
                }
            }
            return Array.Empty<byte>();
        }

        private static int IndexOf(byte[] source, byte[] pattern, int start)
        {
            if (pattern.Length == 0 || source.Length < pattern.Length) return -1;
            for (var i = start; i <= source.Length - pattern.Length; i++)
            {
                var match = true;
                for (var j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }

        /// <summary>结束符若是 "0D 0A" 形式按十六进制处理，否则按 UTF-8 文本。</summary>
        private static string ToHexLiteral(string terminator) =>
            terminator != null && terminator.Contains(" ") ? terminator : "";
    }
}
