using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ZeroCodeVisionStudio.Shared.Data
{
    /// <summary>
    /// 流程预设构建器——提供内置示例方案的快速构建能力。
    /// <para>
    /// 该静态类封装了一组出厂预设流程的构建逻辑。调用 <see cref="BuildDefaultDefectInspectionScheme"/> 
    /// 可以在不配置任何参数的情况下，直接得到一个包含"缺陷检测 + 形状匹配定位"的完整方案对象，
    /// 方便新用户快速上手或演示框架能力。
    /// </para>
    /// </summary>
    public static class WorkflowPresetBuilder
    {
        /// <summary>
        /// 构建"缺陷检测与定位匹配"预设方案对象。
        /// <para>
        /// 该方案包含一条流程，依次包含以下节点：
        /// 读取图像 → RGB 转灰度 → 裁剪图像域 → 阈值分割 → 形态学处理 → 形状筛选 → 显示结果 → 缺陷测量，
        /// 以及并行的创建/查找形状模板分支用于定位匹配。
        /// </para>
        /// </summary>
        /// <returns>包含完整预配置节点和连线的 <see cref="SchemeData"/> 实例。</returns>
        public static SchemeData BuildDefaultDefectInspectionScheme()
        {
            return new SchemeData
            {
                Name = "缺陷检测与定位匹配",
                ActiveWorkflowIndex = 0,
                Workflows = new List<WorkflowData>
                {
                    new WorkflowData
                    {
                        Name = "缺陷检测主流程",
                        Nodes = new List<NodeData>
                        {
                            Node("ReadImage", "读取图像", 80, 80, new Dictionary<string, object>
                            {
                                ["ImageFiles"] = new List<string>()
                            }),
                            Node("RgbToGray", "RGB转灰度", 320, 80),
                            Node("ReduceDomain", "裁剪图像域", 560, 80),
                            Node("Threshold", "阈值分割", 800, 80, new Dictionary<string, object>
                            {
                                ["MinGray"] = 0d,
                                ["MaxGray"] = 128d
                            }),
                            Node("Morphology", "形态学处理", 1040, 80, new Dictionary<string, object>
                            {
                                ["Operation"] = "ClosingCircle",
                                ["Radius"] = 3.5d
                            }),
                            Node("SelectShape", "形状筛选", 1280, 80, new Dictionary<string, object>
                            {
                                ["Feature"] = "Area",
                                ["Operation"] = "And",
                                ["Min"] = 50d,
                                ["Max"] = 99999999d
                            }),
                            Node("DisplayInfo", "显示结果", 1520, 80, new Dictionary<string, object>
                            {
                                ["HeaderText"] = "缺陷检测结果"
                            }),
                            Node("DefectMeasure", "缺陷测量", 1760, 80, new Dictionary<string, object>
                            {
                                ["AllowedMaxCount"] = 0,
                                ["AllowedMaxTotalArea"] = 0d
                            }),
                            Node("CreateShapeModel", "创建形状模板", 800, 320, new Dictionary<string, object>
                            {
                                ["NumLevels"] = 4,
                                ["AngleStartDeg"] = -30d,
                                ["AngleExtentDeg"] = 60d,
                                ["AngleStepDeg"] = 0d,
                                ["Optimization"] = "auto",
                                ["Metric"] = "UsePolarity",
                                ["Contrast"] = "auto",
                                ["MinContrast"] = "auto",
                                ["SavePath"] = string.Empty
                            }),
                            Node("FindShapeModel", "查找形状模板", 1040, 320, new Dictionary<string, object>
                            {
                                ["AngleStartDeg"] = -30d,
                                ["AngleExtentDeg"] = 60d,
                                ["MinScore"] = 0.5d,
                                ["NumMatches"] = 1,
                                ["MaxOverlap"] = 0.5d,
                                ["SubPixel"] = "LeastSquares",
                                ["NumLevels"] = 0,
                                ["Greediness"] = 0.9d
                            }),
                            Node("DisplayInfo", "定位结果显示", 1280, 320, new Dictionary<string, object>
                            {
                                ["HeaderText"] = "定位匹配结果"
                            }),
                            Node("UnionRegion", "区域合并", 1520, 320)
                        },
                        Connections = new List<ConnectionData>
                        {
                            Connection(0, 1),
                            Connection(1, 2),
                            Connection(2, 3),
                            Connection(3, 4),
                            Connection(4, 5),
                            Connection(5, 7),
                            Connection(8, 9),
                            Connection(9, 10),
                        },
                        DisplaySources = new List<DisplaySourceData>
                        {
                            new DisplaySourceData { NodeIndex = 7, OutputKey = RunOutputKeys.Output, Order = 0, IsVisible = true },
                            new DisplaySourceData { NodeIndex = 10, OutputKey = RunOutputKeys.Output, Order = 1, IsVisible = true }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// 将 <see cref="BuildDefaultDefectInspectionScheme"/> 构建的预设方案序列化为缩进格式的 JSON 字符串。
        /// </summary>
        /// <returns>符合 <see cref="SchemePersistenceService"/> 存储规范的 JSON 字符串。</returns>
        public static string BuildDefaultDefectInspectionJson()
        {
            return JsonSerializer.Serialize(BuildDefaultDefectInspectionScheme(), new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            });
        }

        /// <summary>
        /// 获取"缺陷检测与定位匹配"预设方案的默认文件路径。
        /// 路径位于 <see cref="SchemePersistenceService.DefaultSchemeDirectory"/> 目录下。
        /// </summary>
        /// <returns>预设文件的完整路径字符串。</returns>
        public static string GetDefaultPresetFilePath()
        {
            return Path.Combine(
                SchemePersistenceService.DefaultSchemeDirectory,
                "缺陷检测与定位匹配.json");
        }

        /// <summary>
        /// 构建一个节点数据对象的快捷工厂方法。
        /// </summary>
        /// <param name="registerKey">算子注册键名，用于在 ControlCenter 中查找算子类型。</param>
        /// <param name="displayName">节点在画布上的显示名称。</param>
        /// <param name="x">画布 X 坐标。</param>
        /// <param name="y">画布 Y 坐标。</param>
        /// <param name="parameterValues">预设的参数值字典；不传则使用空字典。</param>
        /// <returns>构建好的 <see cref="NodeData"/> 实例。</returns>
        private static NodeData Node(string registerKey, string displayName, double x, double y, Dictionary<string, object> parameterValues = null)
        {
            return new NodeData
            {
                RegisterKey = registerKey,
                DisplayName = displayName,
                X = x,
                Y = y,
                ParameterValues = parameterValues ?? new Dictionary<string, object>()
            };
        }

        /// <summary>
        /// 构建一个连线数据对象的快捷工厂方法。
        /// </summary>
        /// <param name="fromIndex">上游节点在 Nodes 列表中的索引。</param>
        /// <param name="toIndex">下游节点在 Nodes 列表中的索引。</param>
        /// <returns>构建好的 <see cref="ConnectionData"/> 实例。</returns>
        private static ConnectionData Connection(int fromIndex, int toIndex)
        {
            return new ConnectionData
            {
                FromIndex = fromIndex,
                ToIndex = toIndex
            };
        }
    }
}
