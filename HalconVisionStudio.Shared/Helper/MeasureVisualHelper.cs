using System;
using HalconDotNet;

namespace HalconVisionStudio.Shared
{
    /// <summary>
    /// 测量图形标注辅助工具类。
    /// 负责生成带十字叉、测量线、箭头、交点等直观几何标注的 Halcon 图元（XLD），
    /// 以及提供安全着色与图像叠加渲染。
    /// </summary>
    public static class MeasureVisualHelper
    {
        /// <summary>
        /// 检查 HObject 是否为有效、已初始化且包含至少一个实际图元的 HALCON 对象。
        /// </summary>
        public static bool IsValidObject(HObject obj)
        {
            if (obj == null || !obj.IsInitialized() || obj.Key == IntPtr.Zero)
                return false;
            try
            {
                HOperatorSet.CountObj(obj, out var count);
                return count.Length > 0 && count.I > 0;
            }
            catch
            {
                return false;
            }
        }

        private static void AppendToResult(ref HObject result, HObject newObj)
        {
            if (newObj == null || !newObj.IsInitialized() || newObj.Key == IntPtr.Zero)
                return;
            try
            {
                HOperatorSet.ConcatObj(result, newObj, out var concatenated);
                result.Dispose();
                result = concatenated;
            }
            catch
            {
            }
            finally
            {
                newObj.Dispose();
            }
        }

        /// <summary>
        /// 生成两点距离测量的标注图元（两个端点十字叉 + 连接线段）。
        /// </summary>
        public static HObject CreateDistancePPOverlay(double r1, double c1, double r2, double c2)
        {
            HOperatorSet.GenEmptyObj(out var result);

            try
            {
                // 端点 1 十字叉
                HOperatorSet.GenCrossContourXld(out var cross1, r1, c1, 14, 0);
                AppendToResult(ref result, cross1);

                // 端点 2 十字叉
                HOperatorSet.GenCrossContourXld(out var cross2, r2, c2, 14, 0);
                AppendToResult(ref result, cross2);

                // 连接线段
                HOperatorSet.GenContourPolygonXld(out var line, new HTuple(r1, r2), new HTuple(c1, c2));
                AppendToResult(ref result, line);

                // 测量方向箭头
                var dist = Math.Sqrt((r1 - r2) * (r1 - r2) + (c1 - c2) * (c1 - c2));
                if (dist > 30)
                {
                    var arrow1 = CreateArrowHead(r1, c1, r2, c2, 12);
                    AppendToResult(ref result, arrow1);

                    var arrow2 = CreateArrowHead(r2, c2, r1, c1, 12);
                    AppendToResult(ref result, arrow2);
                }
            }
            catch
            {
                // 保证异常时返回有效空对象或已有图元
            }

            return result;
        }

        /// <summary>
        /// 生成点到线距离测量的标注图元（基准线段 + 目标点十字 + 垂足十字 + 垂线）。
        /// </summary>
        public static HObject CreateDistancePLOverlay(
            double pRow, double pCol,
            double lRow1, double lCol1, double lRow2, double lCol2,
            double projRow, double projCol)
        {
            HOperatorSet.GenEmptyObj(out var result);

            try
            {
                // 基准线
                HOperatorSet.GenContourPolygonXld(out var line, new HTuple(lRow1, lRow2), new HTuple(lCol1, lCol2));
                AppendToResult(ref result, line);

                // 目标点十字叉 (稍大)
                HOperatorSet.GenCrossContourXld(out var crossP, pRow, pCol, 18, Math.PI / 4.0);
                AppendToResult(ref result, crossP);

                // 垂足十字叉
                HOperatorSet.GenCrossContourXld(out var crossProj, projRow, projCol, 12, 0);
                AppendToResult(ref result, crossProj);

                // 垂直测量虚线/实线
                HOperatorSet.GenContourPolygonXld(out var perpLine, new HTuple(pRow, projRow), new HTuple(pCol, projCol));
                AppendToResult(ref result, perpLine);
            }
            catch
            {
            }

            return result;
        }

        /// <summary>
        /// 生成两条直线夹角测量的标注图元（两条直线 + 交点）。
        /// </summary>
        public static HObject CreateAngleLLOverlay(
            double l1r1, double l1c1, double l1r2, double l1c2,
            double l2r1, double l2c1, double l2r2, double l2c2)
        {
            HOperatorSet.GenEmptyObj(out var result);

            try
            {
                // 线 1
                HOperatorSet.GenContourPolygonXld(out var line1, new HTuple(l1r1, l1r2), new HTuple(l1c1, l1c2));
                AppendToResult(ref result, line1);

                // 线 2
                HOperatorSet.GenContourPolygonXld(out var line2, new HTuple(l2r1, l2r2), new HTuple(l2c1, l2c2));
                AppendToResult(ref result, line2);

                // 计算两直线交点
                HOperatorSet.IntersectionLines(l1r1, l1c1, l1r2, l1c2, l2r1, l2c1, l2r2, l2c2, out var iRow, out var iCol, out var isParallel);
                if (isParallel.Length > 0 && isParallel.I == 1 && iRow.Length > 0)
                {
                    HOperatorSet.GenCrossContourXld(out var crossI, iRow.D, iCol.D, 16, Math.PI / 4.0);
                    AppendToResult(ref result, crossI);
                }
            }
            catch
            {
            }

            return result;
        }

        /// <summary>
        /// 将 XLD 测量图元绘制到原始图像副本中。
        /// </summary>
        public static HObject PaintOverlayToImage(HObject sourceImage, HObject overlay, bool isOk = true)
        {
            var validSource = IsValidObject(sourceImage);
            var validOverlay = IsValidObject(overlay);

            if (!validSource && !validOverlay)
                return null;

            if (!validSource)
            {
                try { return overlay.Clone(); }
                catch { return null; }
            }

            if (!validOverlay)
            {
                try { return sourceImage.Clone(); }
                catch { return null; }
            }

            try
            {
                HOperatorSet.CountChannels(sourceImage, out var channels);
                HTuple colorTuple = channels.I == 3
                    ? (isOk ? new HTuple(0, 255, 64) : new HTuple(255, 32, 32))
                    : new HTuple(255);

                HOperatorSet.PaintXld(overlay, sourceImage, out var paintedImage, colorTuple);
                return paintedImage;
            }
            catch
            {
                try { return sourceImage.Clone(); }
                catch { return null; }
            }
        }

        private static HObject CreateArrowHead(double fromR, double fromC, double toR, double toC, double size)
        {
            var dr = toR - fromR;
            var dc = toC - fromC;
            var angle = Math.Atan2(dc, dr);
            var alpha = Math.PI / 6.0; // 30 deg

            var leftR = toR - size * Math.Cos(angle - alpha);
            var leftC = toC - size * Math.Sin(angle - alpha);

            var rightR = toR - size * Math.Cos(angle + alpha);
            var rightC = toC - size * Math.Sin(angle + alpha);

            HOperatorSet.GenContourPolygonXld(
                out var arrow,
                new HTuple(leftR, toR, rightR),
                new HTuple(leftC, toC, rightC));

            return arrow;
        }
    }
}
