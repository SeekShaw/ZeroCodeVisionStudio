using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroCodeVisionStudio.Shared.Helper
{
    /// <summary>
    /// 序列名称生成器。
    /// 为流程画布上同类型的算子节点生成唯一且带序号的默认显示名称，格式为"前缀+两位数字"（如 "阈值分割01"、"阈值分割02" 等）。
    /// <para>
    /// 特性：
    /// <list type="bullet">
    ///   <item><description>不同前缀的序号独立维护，互不干扰。</description></item>
    ///   <item><description>删除节点时序号可被释放，下次创建优先复用最小的空缺序号。</description></item>
    ///   <item><description>序号格式固定为两位数（01~99，大于99则自适应扩展），保持界面显示整齐美观。</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public class SequenceNameGenerator
    {
        /// <summary>每个前缀对应的已占用序号集合（延迟初始化字典）。</summary>
        private readonly Dictionary<string, HashSet<int>> _prefixData = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 为指定前缀生成下一个序列名称，自动寻找最小可用序号（优先填补删除留下的空缺）。
        /// </summary>
        /// <param name="prefix">名称前缀，通常为算子的显示名称（如 "阈值分割"）。</param>
        /// <returns>格式为 "前缀+两位数字" 的名称（如 "阈值分割01"）。</returns>
        public string Generate(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                prefix = "Node";
            }

            if (!_prefixData.TryGetValue(prefix, out var numbers))
            {
                numbers = new HashSet<int>();
                _prefixData[prefix] = numbers;
            }

            // 从 1 开始寻找最小未使用的序号
            int nextNum = 1;
            while (numbers.Contains(nextNum))
            {
                nextNum++;
            }

            numbers.Add(nextNum);

            // 格式化为至少两位数字：01, 02, ... 99
            return $"{prefix}{nextNum:D2}";
        }

        /// <summary>
        /// 释放指定名称占用的序号，供后续 <see cref="Generate"/> 优先复用。
        /// 当节点从画布中被删除或被重命名时调用。
        /// </summary>
        /// <param name="name">要释放的完整名称（如 "阈值分割02"）。</param>
        /// <returns>若成功解析并释放则返回 true；若未被记录或格式不匹配则返回 false。</returns>
        public bool Remove(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            // 从末尾提取连续的数字部分作为序号
            int i = name.Length - 1;
            while (i >= 0 && char.IsDigit(name[i]))
            {
                i--;
            }

            if (i < 0 || i == name.Length - 1)
            {
                // 没有前缀或没有末尾数字
                return false;
            }

            var prefix = name.Substring(0, i + 1);
            var numPart = name.Substring(i + 1);

            if (!int.TryParse(numPart, out int num))
            {
                return false;
            }

            if (!_prefixData.TryGetValue(prefix, out var numbers))
            {
                return false;
            }

            return numbers.Remove(num);
        }

        /// <summary>
        /// 获取指定前缀下当前已被占用的所有序列名称列表（按序号升序排列）。
        /// </summary>
        /// <param name="prefix">名称前缀。</param>
        /// <returns>已占用的名称列表，按序号升序排列。</returns>
        public List<string> GetAll(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix) || !_prefixData.TryGetValue(prefix, out var numbers))
            {
                return new List<string>();
            }

            return numbers.OrderBy(n => n).Select(n => $"{prefix}{n:D2}").ToList();
        }

        /// <summary>
        /// 清空所有前缀的序号占用记录。
        /// </summary>
        public void Clear()
        {
            _prefixData.Clear();
        }

        /// <summary>
        /// 清空指定前缀的序号占用记录。
        /// </summary>
        /// <param name="prefix">名称前缀。</param>
        public void Clear(string prefix)
        {
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                _prefixData.Remove(prefix);
            }
        }
    }
}
