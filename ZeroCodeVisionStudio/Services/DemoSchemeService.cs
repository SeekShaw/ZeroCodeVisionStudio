using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ZeroCodeVisionStudio.Shared.Data;
using HalconDotNet;

namespace ZeroCodeVisionStudio.Services
{
    /// <summary>
    /// 示例方案生成器：首次启动时在默认方案目录的「示例方案」子目录下
    /// 合成样本图像（缺陷/零件/量块/模板）与形状模板文件，并生成 13 个不同方向的完整工业级示例方案。
    /// 涵盖：表面缺陷检测、目标计数、尺寸测量、灰度统计、预处理增强、模板匹配、动态阈值、
    /// 边缘轮廓、逻辑变量、多分支并行、OK/NG条件分流、执行状态联动、双流程生产统计。
    /// </summary>
    public static class DemoSchemeService
    {
        public static string DemoDirectory =>
            Path.Combine(SchemePersistenceService.DefaultSchemeDirectory, "示例方案");

        private static string ImagesDirectory => Path.Combine(DemoDirectory, "images");
        private static string MarkerFile => Path.Combine(DemoDirectory, ".generated");

        private static readonly object gate = new();

        public static readonly (string fileName, string description)[] AllDemoInfos = new[]
        {
            ("01-入门-表面缺陷检测.json", "读取图像 → 二值化 → 连通域 → 形状筛选 → 缺陷测量 → 结果显示：亮工件上的划伤/污点检测，显示缺陷总数与 OK/NG。"),
            ("02-入门-目标计数.json", "读取图像 → 二值化(亮极性) → 连通域 → 区域统计 → 结果显示：暗背景下的亮工件数量自动识别与统计。"),
            ("03-入门-尺寸测量.json", "读取图像 → 二值化 → 最小外接矩形 → 结果显示：标准量块宽度与高度的亚像素级测量。"),
            ("04-入门-灰度统计判定.json", "读取图像 → 平均灰度判定（期望值±容差）→ 结果显示：按整图/区域灰度均值与标准差快速判别良品。"),
            ("05-入门-图像预处理增强.json", "读取图像 → 均值滤波 → 直方图均衡化 → 自动阈值 → 结果显示：低对比度、光照不均图像的预处理增强链。"),
            ("06-进阶-模板匹配定位.json", "读取预生成形状模板 → 模板匹配（位置/角度/得分）→ 显示节点绑定匹配得分与角度（测量值绑定显示）。"),
            ("07-进阶-动态阈值检测.json", "读取图像 → 动态阈值(局部背景差分) → 连通域 → 缺陷测量 → 结果显示：光照不均场景下的局部缺陷检测。"),
            ("08-进阶-边缘轮廓检测.json", "读取图像 → Sobel边缘梯度 → 阈值 → 连通域 → 区域统计：边缘幅值提取与边缘连通域分析。"),
            ("09-进阶-逻辑控制与变量.json", "读取图像 → 变量写入 → 计数器 → 结果显示：变量与逻辑算子协作（计数取模），为多周期流程控制打样。"),
            ("10-综合-多分支并行检测.json", "读取图像 → 二值化 → 连通域 → 三条独立分支（面积/圆度/缺陷）并发执行；汇总显示绑定缺陷计数与 OK 判定（参数绑定 + 方案级并行开关）。"),
            ("11-综合-OKNG条件分流.json", "缺陷测量(允许上限3) → OK/NG 两个显示节点绑定同一判定值，用执行条件 $display.item.1$ 自动二选一分流。"),
            ("12-综合-执行状态联动.json", "缺陷测量 → 两个显示节点：一个绑定缺陷计数（检测结果），一个绑定执行状态 exec.success/exec.status（每个节点都有的常驻输出）——演示执行状态可作为下游数据源。"),
            ("13-综合-生产统计双流程.json", "双流程方案：检测主流程（图像→缺陷→显示，绑定缺陷计数）；统计与逻辑流程（变量写入 → 计数器取模 → 显示，绑定计数值）。")
        };

        /// <summary>确保示例方案已生成（幂等：标记文件版本匹配则跳过）。</summary>
        public static void EnsureCreated()
        {
            lock (gate)
            {
                const string version = "v7";
                if (File.Exists(MarkerFile) && File.ReadAllText(MarkerFile).Trim() == version) return;

                Directory.CreateDirectory(DemoDirectory);
                foreach (var stale in Directory.GetFiles(DemoDirectory, "*.json"))
                {
                    try { File.Delete(stale); } catch { }
                }
                Directory.CreateDirectory(ImagesDirectory);
                GenerateImages();
                GenerateShapeModel();
                GenerateAllSchemes();
                File.WriteAllText(MarkerFile, version);
            }
        }

        /// <summary>列出示例方案（仅返回实际存在的文件）。</summary>
        public static List<SchemeMenuItem> ListDemos()
        {
            var result = new List<SchemeMenuItem>();
            foreach (var (fileName, description) in AllDemoInfos)
            {
                var path = Path.Combine(DemoDirectory, fileName);
                if (!File.Exists(path)) continue;
                result.Add(new SchemeMenuItem
                {
                    DisplayName = Path.GetFileNameWithoutExtension(fileName),
                    FullPath = path,
                    Description = description
                });
            }
            return result;
        }

        // ==================== 样本图像合成 ====================

        private static void GenerateImages()
        {
            // surface.png：均匀工件底 + 暗划伤/暗污点/亮污点
            using (var img = Proto(640, 480, 190))
            {
                Overpaint(img, Line(120, 120, 520, 160), 55);
                Overpaint(img, Line(140, 300, 500, 340), 50);
                Overpaint(img, Line(300, 100, 340, 400), 60);
                Overpaint(img, Circle(420, 250, 12), 45);
                Overpaint(img, Circle(220, 210, 10), 245);
                Save(img, "surface.png");
            }

            // parts.png：暗背景 + 亮目标（计数/尺寸）
            using (var img = Proto(640, 480, 60))
            {
                Overpaint(img, Circle(120, 120, 35), 220);
                Overpaint(img, Circle(320, 100, 28), 225);
                Overpaint(img, Circle(520, 140, 32), 215);
                Overpaint(img, Circle(160, 330, 30), 220);
                Overpaint(img, Circle(350, 300, 36), 218);
                Overpaint(img, Circle(540, 340, 26), 222);
                Overpaint(img, Rect(240, 200, 310, 250), 230);
                Overpaint(img, Rect(430, 210, 500, 260), 228);
                Save(img, "parts.png");
            }

            // gauge.png：量块（最小外接矩形 → 宽高测量）
            using (var img = Proto(640, 480, 90))
            {
                Overpaint(img, Rect(200, 140, 440, 340), 210);
                Save(img, "gauge.png");
            }

            // template.png：特征标记（模板匹配源：亮块 + 暗圆孔）
            using (var img = Proto(640, 480, 100))
            {
                Overpaint(img, Rect(240, 180, 400, 300), 230);
                Overpaint(img, Circle(320, 240, 28), 60);
                Save(img, "template.png");
            }
        }

        private static void GenerateShapeModel()
        {
            var imagePath = Path.Combine(ImagesDirectory, "template.png");
            var modelPath = Path.Combine(ImagesDirectory, "template.shm");
            HObject image = null;
            HTuple modelId = new HTuple();
            try
            {
                HOperatorSet.ReadImage(out image, imagePath);
                HOperatorSet.CreateShapeModel(image, "auto", -0.39, 0.78, "auto", "auto",
                    "use_polarity", "auto", "auto", out modelId);
                HOperatorSet.WriteShapeModel(modelId, modelPath);
            }
            finally
            {
                if (image != null && image.IsInitialized()) image.Dispose();
                if (modelId.Length > 0)
                {
                    try { HOperatorSet.ClearShapeModel(modelId); } catch { }
                }
            }
        }

        private static HObject Proto(int width, int height, double gray)
        {
            HOperatorSet.GenImageConst(out var img, "byte", width, height);
            using (var full = Rect(0, 0, height - 1, width - 1))
            {
                HOperatorSet.OverpaintRegion(img, full, gray, "fill");
            }
            return img;
        }

        private static HObject Rect(double r1, double c1, double r2, double c2)
        {
            HOperatorSet.GenRectangle1(out var region, r1, c1, r2, c2);
            return region;
        }

        private static HObject Circle(double row, double col, double radius)
        {
            HOperatorSet.GenCircle(out var region, row, col, radius);
            return region;
        }

        private static HObject Line(double r1, double c1, double r2, double c2)
        {
            HOperatorSet.GenRegionLine(out var region, r1, c1, r2, c2);
            return region;
        }

        private static void Overpaint(HObject image, HObject region, double gray)
        {
            try
            {
                HOperatorSet.OverpaintRegion(image, region, gray, "fill");
            }
            finally
            {
                region.Dispose();
            }
        }

        private static void Save(HObject image, string fileName)
        {
            HOperatorSet.WriteImage(image, "png", 0, Path.Combine(ImagesDirectory, fileName));
        }

        // ==================== 方案全量生成 ====================

        private static void GenerateAllSchemes()
        {
            var surface = Path.Combine(ImagesDirectory, "surface.png");
            var parts = Path.Combine(ImagesDirectory, "parts.png");
            var gauge = Path.Combine(ImagesDirectory, "gauge.png");
            var template = Path.Combine(ImagesDirectory, "template.png");
            var shapeModel = Path.Combine(ImagesDirectory, "template.shm");

            // 01 表面缺陷检测：暗缺陷（划伤/污点）在亮工件上的标准检测链
            {
                var wf = NewWorkflow();
                var ri = Nd(wf, "ReadImage", "读取图像", P("ImageFiles", L(surface)), 60, 60);
                var bt = Nd(wf, "BinaryThreshold", "二值化", null, 320, 60);
                var conn = Nd(wf, "Connection", "连通域", null, 580, 60);
                var ss = Nd(wf, "SelectShape", "形状筛选", null, 840, 60);
                var dm = Nd(wf, "DefectMeasure", "缺陷测量", P("MinAreaFilter", 30, "AllowedMaxCount", 8), 1100, 60);
                var dsp = Nd(wf, "DisplayInfo", "结果显示", P("HeaderText", "表面缺陷检测"), 1360, 60);
                Edge(wf, ri, bt); Edge(wf, bt, conn); Edge(wf, conn, ss); Edge(wf, ss, dm); Edge(wf, dm, dsp);
                Bind(wf, dsp, "display.item.1", dm, "defect.count");
                Bind(wf, dsp, "display.item.2", dm, "measure.is_ok");
                Save(new SchemeData { Name = "01-入门-表面缺陷检测", Workflows = { wf }, ActiveWorkflowIndex = 0 },
                    "01-入门-表面缺陷检测.json",
                    "读取图像 → 二值化 → 连通域 → 形状筛选 → 缺陷测量 → 结果显示：亮工件上的划伤/污点检测，显示缺陷总数与 OK/NG。");
            }

            // 02 目标计数：亮目标 Blob 计数
            {
                var wf = NewWorkflow();
                var ri = Nd(wf, "ReadImage", "读取图像", P("ImageFiles", L(parts)), 60, 60);
                var bt = Nd(wf, "BinaryThreshold", "二值化", P("LightDark", "light"), 320, 60);
                var conn = Nd(wf, "Connection", "连通域", null, 580, 60);
                var ac = Nd(wf, "AreaCenter", "区域统计", null, 840, 60);
                var dsp = Nd(wf, "DisplayInfo", "结果显示", P("HeaderText", "目标计数"), 1100, 60);
                Edge(wf, ri, bt); Edge(wf, bt, conn); Edge(wf, conn, ac); Edge(wf, ac, dsp);
                Bind(wf, dsp, "display.item.1", ac, "region.count");
                Save(new SchemeData { Name = "02-入门-目标计数", Workflows = { wf }, ActiveWorkflowIndex = 0 },
                    "02-入门-目标计数.json",
                    "读取图像 → 二值化(亮极性) → 连通域 → 区域统计 → 结果显示：暗背景下的亮工件数量自动识别与统计。");
            }

            // 03 入门-尺寸测量：最小外接矩形 → 宽高
            {
                var wf = NewWorkflow();
                var ri = Nd(wf, "ReadImage", "读取图像", P("ImageFiles", L(gauge)), 60, 60);
                var bt = Nd(wf, "BinaryThreshold", "二值化", P("LightDark", "light"), 320, 60);
                var sr = Nd(wf, "SmallestRectangle1", "最小外接矩形", null, 580, 60);
                var dsp = Nd(wf, "DisplayInfo", "结果显示", P("HeaderText", "尺寸测量"), 840, 60);
                Edge(wf, ri, bt); Edge(wf, bt, sr); Edge(wf, sr, dsp);
                Bind(wf, dsp, "display.item.1", sr, "geometry.smallest_rectangle1.width");
                Bind(wf, dsp, "display.item.2", sr, "geometry.smallest_rectangle1.height");
                Save(new SchemeData { Name = "03-入门-尺寸测量", Workflows = { wf }, ActiveWorkflowIndex = 0 },
                    "03-入门-尺寸测量.json",
                    "读取图像 → 二值化 → 最小外接矩形 → 结果显示：量块宽度/高度测量。");
            }

            // 04 灰度统计判定
            {
                var wf = NewWorkflow();
                var ri = Nd(wf, "ReadImage", "读取图像", P("ImageFiles", L(surface)), 60, 60);
                var it = Nd(wf, "Intensity", "灰度判定", P("ExpectedMean", 190.0, "Tolerance", 15.0), 320, 60);
                var dsp = Nd(wf, "DisplayInfo", "结果显示", P("HeaderText", "灰度统计判定"), 580, 60);
                Edge(wf, ri, it); Edge(wf, it, dsp);
                Bind(wf, dsp, "display.item.1", it, "region.intensity_mean");
                Bind(wf, dsp, "display.item.2", it, "measure.is_ok");
                Save(new SchemeData { Name = "04-入门-灰度统计判定", Workflows = { wf }, ActiveWorkflowIndex = 0 },
                    "04-入门-灰度统计判定.json",
                    "读取图像 → 平均灰度判定（期望值±容差）→ 结果显示：按灰度均值快速判 OK/NG。");
            }

            // 05 图像预处理增强
            {
                var wf = NewWorkflow();
                var ri = Nd(wf, "ReadImage", "读取图像", P("ImageFiles", L(surface)), 60, 60);
                var mi = Nd(wf, "MeanImage", "均值滤波", P("MaskWidth", 9, "MaskHeight", 9), 320, 60);
                var eq = Nd(wf, "EquHistoImage", "直方图均衡化", null, 580, 60);
                var at = Nd(wf, "AutoThreshold", "自动阈值", null, 840, 60);
                var dsp = Nd(wf, "DisplayInfo", "结果显示", P("HeaderText", "预处理增强"), 1100, 60);
                Edge(wf, ri, mi); Edge(wf, mi, eq); Edge(wf, eq, at); Edge(wf, at, dsp);
                Save(new SchemeData { Name = "05-入门-图像预处理增强", Workflows = { wf }, ActiveWorkflowIndex = 0 },
                    "05-入门-图像预处理增强.json",
                    "读取图像 → 均值滤波 → 直方图均衡化 → 自动阈值 → 结果显示：低对比度图像的预处理增强链。");
            }

            // 06 进阶-模板匹配定位
            {
                var wf = NewWorkflow();
                var ri = Nd(wf, "ReadImage", "读取图像", P("ImageFiles", L(template)), 60, 60);
                var rm = Nd(wf, "ReadShapeModel", "读取模板", P("ModelPath", shapeModel), 320, 60);
                var fm = Nd(wf, "FindShapeModel", "模板匹配", P("MinScore", 0.7), 580, 60);
                var dsp = Nd(wf, "DisplayInfo", "定位结果", P("HeaderText", "模板匹配"), 840, 60);
                Edge(wf, ri, rm); Edge(wf, rm, fm); Edge(wf, fm, dsp);
                Bind(wf, dsp, "display.item.1", fm, "shape_match.scores");
                Bind(wf, dsp, "display.item.2", fm, "shape_match.angles");
                Save(new SchemeData { Name = "06-进阶-模板匹配定位", Workflows = { wf }, ActiveWorkflowIndex = 0 },
                    "06-进阶-模板匹配定位.json",
                    "读取预生成形状模板 → 模板匹配（位置/角度/得分）→ 显示节点绑定匹配得分与角度（测量值绑定显示）。");
            }

            // 07 进阶-动态阈值检测
            {
                var wf = NewWorkflow();
                var ri = Nd(wf, "ReadImage", "读取图像", P("ImageFiles", L(surface)), 60, 60);
                var dt = Nd(wf, "DynThreshold", "动态阈值", null, 320, 60);
                var conn = Nd(wf, "Connection", "连通域", null, 580, 60);
                var dm = Nd(wf, "DefectMeasure", "缺陷测量", P("MinAreaFilter", 30, "AllowedMaxCount", 8), 840, 60);
                var dsp = Nd(wf, "DisplayInfo", "结果显示", P("HeaderText", "动态阈值检测"), 1100, 60);
                Edge(wf, ri, dt); Edge(wf, dt, conn); Edge(wf, conn, dm); Edge(wf, dm, dsp);
                Bind(wf, dsp, "display.item.1", dm, "defect.count");
                Bind(wf, dsp, "display.item.2", dm, "measure.is_ok");
                Save(new SchemeData { Name = "07-进阶-动态阈值检测", Workflows = { wf }, ActiveWorkflowIndex = 0 },
                    "07-进阶-动态阈值检测.json",
                    "读取图像 → 动态阈值 → 连通域 → 缺陷测量 → 结果显示：光照不均场景的局部对比缺陷检测。");
            }

            // 08 进阶-边缘轮廓检测
            {
                var wf = NewWorkflow();
                var ri = Nd(wf, "ReadImage", "读取图像", P("ImageFiles", L(parts)), 60, 60);
                var sa = Nd(wf, "SobelAmp", "Sobel边缘", null, 320, 60);
                var th = Nd(wf, "Threshold", "边缘阈值", P("MinGray", 30.0, "MaxGray", 255.0), 580, 60);
                var conn = Nd(wf, "Connection", "连通域", null, 840, 60);
                var ac = Nd(wf, "AreaCenter", "边缘统计", null, 1100, 60);
                var dsp = Nd(wf, "DisplayInfo", "结果显示", P("HeaderText", "边缘轮廓检测"), 1360, 60);
                Edge(wf, ri, sa); Edge(wf, sa, th); Edge(wf, th, conn); Edge(wf, conn, ac); Edge(wf, ac, dsp);
                Bind(wf, dsp, "display.item.1", ac, "region.count");
                Bind(wf, dsp, "display.item.2", ac, "region.area");
                Save(new SchemeData { Name = "08-进阶-边缘轮廓检测", Workflows = { wf }, ActiveWorkflowIndex = 0 },
                    "08-进阶-边缘轮廓检测.json",
                    "读取图像 → Sobel边缘 → 阈值 → 连通域 → 区域统计：边缘梯度提取与边缘区域分析。");
            }

            // 09 进阶-逻辑控制与变量
            {
                var wf = NewWorkflow();
                var ri = Nd(wf, "ReadImage", "读取图像", P("ImageFiles", L(parts)), 60, 60);
                var vs = Nd(wf, "VariableSet", "变量写入", P("VariableName", "station", "Value", "A1"), 320, 60);
                var cnt = Nd(wf, "Counter", "计数器", P("Modulo", 5), 580, 60);
                var dsp = Nd(wf, "DisplayInfo", "结果显示", P("HeaderText", "逻辑控制"), 840, 60);
                Edge(wf, ri, vs); Edge(wf, vs, cnt); Edge(wf, cnt, dsp);
                Bind(wf, dsp, "display.item.1", cnt, "logic.count");
                Save(new SchemeData { Name = "09-进阶-逻辑控制与变量", Workflows = { wf }, ActiveWorkflowIndex = 0 },
                    "09-进阶-逻辑控制与变量.json",
                    "读取图像 → 变量写入 → 计数器 → 结果显示：变量与逻辑算子协作（计数取模），为多周期流程控制打样。");
            }

            // 10 多分支并行检测与汇总
            {
                var wf = NewWorkflow();
                var ri = Nd(wf, "ReadImage", "读取图像", P("ImageFiles", L(parts)), 60, 60);
                var bt = Nd(wf, "BinaryThreshold", "二值化", P("LightDark", "light"), 320, 60);
                var conn = Nd(wf, "Connection", "连通域", null, 580, 60);
                var ac = Nd(wf, "AreaCenter", "分支A-面积统计", null, 840, 60);
                var dspA = Nd(wf, "DisplayInfo", "显示-面积结果", P("HeaderText", "面积统计"), 1100, 60);
                var circ = Nd(wf, "Circularity", "分支B-圆度检测", null, 840, 210);
                var dspB = Nd(wf, "DisplayInfo", "显示-圆度结果", P("HeaderText", "圆度检测"), 1100, 210);
                var dm = Nd(wf, "DefectMeasure", "分支C-缺陷测量", P("MinAreaFilter", 30, "AllowedMaxCount", 8), 840, 360);
                var dspC = Nd(wf, "DisplayInfo", "汇总显示", P("HeaderText", "综合判定"), 1100, 360);
                Edge(wf, ri, bt); Edge(wf, bt, conn);
                Edge(wf, conn, ac); Edge(wf, ac, dspA);
                Edge(wf, conn, circ); Edge(wf, circ, dspB);
                Edge(wf, conn, dm); Edge(wf, dm, dspC);
                Bind(wf, dspC, "display.item.1", dm, "defect.count");
                Bind(wf, dspC, "display.item.2", dm, "measure.is_ok");
                wf.ParallelExecution = true;
                Save(new SchemeData { Name = "10-综合-多分支并行检测", Workflows = { wf }, ActiveWorkflowIndex = 0 },
                    "10-综合-多分支并行检测.json",
                    "读取图像 → 二值化 → 连通域 → 三条独立分支（面积/圆度/缺陷）并发执行；汇总显示绑定缺陷计数与 OK 判定（参数绑定 + 方案级并行开关）。");
            }

            // 11 OK/NG 条件分流
            {
                var wf = NewWorkflow();
                var ri = Nd(wf, "ReadImage", "读取图像", P("ImageFiles", L(surface)), 60, 210);
                var bt = Nd(wf, "BinaryThreshold", "二值化", null, 320, 210);
                var conn = Nd(wf, "Connection", "连通域", null, 580, 210);
                var dm = Nd(wf, "DefectMeasure", "缺陷测量", P("MinAreaFilter", 30, "AllowedMaxCount", 3), 840, 210);
                var ok = Nd(wf, "DisplayInfo", "OK结果显示", P("HeaderText", "OK"), 1100, 60);
                var ng = Nd(wf, "DisplayInfo", "NG报警显示", P("HeaderText", "NG"), 1100, 360);
                Edge(wf, ri, bt); Edge(wf, bt, conn); Edge(wf, conn, dm);
                Edge(wf, dm, ok); Edge(wf, dm, ng);
                Bind(wf, ok, "display.item.1", dm, "measure.is_ok");
                Bind(wf, ng, "display.item.1", dm, "measure.is_ok");
                Cond(wf, ok, "$display.item.1$ == true");
                Cond(wf, ng, "$display.item.1$ == false");
                Save(new SchemeData { Name = "11-综合-OKNG条件分流", Workflows = { wf }, ActiveWorkflowIndex = 0 },
                    "11-综合-OKNG条件分流.json",
                    "缺陷测量(允许上限3) → OK/NG 两个显示节点绑定同一判定值，用执行条件 $display.item.1$ 自动二选一分流。");
            }

            // 12 综合-执行状态联动
            {
                var wf = NewWorkflow();
                var ri = Nd(wf, "ReadImage", "读取图像", P("ImageFiles", L(parts)), 60, 210);
                var bt = Nd(wf, "BinaryThreshold", "二值化", P("LightDark", "light"), 320, 210);
                var dm = Nd(wf, "DefectMeasure", "缺陷测量", P("MinAreaFilter", 30, "AllowedMaxCount", 8), 580, 210);
                var state = Nd(wf, "DisplayInfo", "执行状态显示", P("HeaderText", "执行状态联动"), 840, 60);
                var result = Nd(wf, "DisplayInfo", "检测结果显示", P("HeaderText", "检测结果"), 840, 360);
                Edge(wf, ri, bt); Edge(wf, bt, dm);
                Edge(wf, dm, state); Edge(wf, dm, result);
                Bind(wf, state, "display.item.1", dm, "exec.success");
                Bind(wf, state, "display.item.2", dm, "exec.status");
                Bind(wf, result, "display.item.1", dm, "defect.count");
                Save(new SchemeData { Name = "12-综合-执行状态联动", Workflows = { wf }, ActiveWorkflowIndex = 0 },
                    "12-综合-执行状态联动.json",
                    "缺陷测量 → 两个显示节点：一个绑定缺陷计数（检测结果），一个绑定执行状态 exec.success/exec.status（每个节点都有的常驻输出）——演示执行状态可作为下游数据源。");
            }

            // 13 生产统计双流程
            {
                var detect = NewWorkflow("检测主流程");
                var ri = Nd(detect, "ReadImage", "读取图像", P("ImageFiles", L(parts)), 60, 60);
                var bt = Nd(detect, "BinaryThreshold", "二值化", P("LightDark", "light"), 320, 60);
                var conn = Nd(detect, "Connection", "连通域", null, 580, 60);
                var dm = Nd(detect, "DefectMeasure", "缺陷测量", P("MinAreaFilter", 30, "AllowedMaxCount", 8), 840, 60);
                var dsp = Nd(detect, "DisplayInfo", "检测结果显示", P("HeaderText", "检测结果"), 1100, 60);
                Edge(detect, ri, bt); Edge(detect, bt, conn); Edge(detect, conn, dm); Edge(detect, dm, dsp);
                Bind(detect, dsp, "display.item.1", dm, "defect.count");

                var stat = NewWorkflow("统计与逻辑");
                var vs = Nd(stat, "VariableSet", "变量写入", P("VariableName", "station", "Value", "A1"), 60, 60);
                var counter = Nd(stat, "Counter", "节拍计数器", P("Modulo", 5), 320, 60);
                var sdsp = Nd(stat, "DisplayInfo", "统计显示", P("HeaderText", "生产统计"), 580, 60);
                Edge(stat, vs, counter); Edge(stat, counter, sdsp);
                Bind(stat, sdsp, "display.item.1", counter, "logic.count");

                Save(new SchemeData { Name = "13-综合-生产统计双流程", Workflows = { detect, stat }, ActiveWorkflowIndex = 0 },
                    "13-综合-生产统计双流程.json",
                    "双流程方案：检测主流程（图像→缺陷→显示，绑定缺陷计数）；统计与逻辑流程（变量写入 → 计数器取模 → 显示，绑定计数值）。");
            }
        }

        private static WorkflowData NewWorkflow(string name = "主流程") => new() { Name = name };

        private static List<string> L(params string[] items) => new(items);

        private static Dictionary<string, object> P(params object[] pairs)
        {
            var dict = new Dictionary<string, object>();
            for (var i = 0; i + 1 < pairs.Length; i += 2) dict[(string)pairs[i]] = pairs[i + 1];
            return dict;
        }

        /// <summary>追加节点（指定画布坐标，横向链 260 间距 / 行高 150），返回节点索引。</summary>
        private static int Nd(WorkflowData wf, string key, string display, Dictionary<string, object> parameters,
            double x = 60, double y = 60)
        {
            wf.Nodes.Add(new NodeData
            {
                RegisterKey = key,
                DisplayName = display,
                X = x,
                Y = y,
                ParameterValues = parameters ?? new Dictionary<string, object>()
            });
            return wf.Nodes.Count - 1;
        }

        private static void Edge(WorkflowData wf, int from, int to) =>
            wf.Connections.Add(new ConnectionData { FromIndex = from, ToIndex = to });

        /// <summary>为节点登记输入绑定（按节点索引寻址源）。</summary>
        private static void Bind(WorkflowData wf, int target, string targetKey, int source, string sourceKey)
        {
            wf.Nodes[target].InputBindings.Add(new InputBindingData
            {
                TargetInputKey = targetKey,
                SourceNodeIndex = source,
                SourceOutputKey = sourceKey
            });
        }

        private static void Cond(WorkflowData wf, int node, string condition) =>
            wf.Nodes[node].ExecutionCondition = condition;

        private static void Save(SchemeData data, string fileName, string description)
        {
            SchemePersistenceService.Save(Path.Combine(DemoDirectory, fileName), data);
            _ = description;
        }
    }
}