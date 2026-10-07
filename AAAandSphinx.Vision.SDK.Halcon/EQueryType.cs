using System;
using System.Collections.Generic;
using System.Text;

namespace AAAandSphinx.Vision.SDK.Halcon
{
    /// <summary>
    /// HALCON 帧抓取器（FrameGrabber）查询类型枚举。
    /// 对应 HALCON info_framegrabber 算子的 Information 参数，
    /// 用于查询已连接相机设备的各类属性和配置信息。
    /// </summary>
    public enum EQueryType
    {
        /// <summary>查询设备的所有默认参数值。</summary>
        defaults,

        /// <summary>查询设备的通用信息（制造商、型号等）。</summary>
        general,

        /// <summary>查询已发现的相机/采集板信息列表。</summary>
        info_boards,

        /// <summary>查询所有可设置的参数名称列表。</summary>
        parameters,

        /// <summary>查询只读参数列表。</summary>
        parameters_readonly,

        /// <summary>查询只写参数列表。</summary>
        parameters_writeonly,

        /// <summary>查询接口的版本号。</summary>
        revision,

        /// <summary>查询每通道位深度（如 8、10、12、16）。</summary>
        bits_per_channel,

        /// <summary>查询相机类型（彩色/黑白等）。</summary>
        camera_type,

        /// <summary>查询色彩空间（如 gray、rgb）。</summary>
        color_space,

        /// <summary>查询可用设备列表。</summary>
        device,

        /// <summary>查询是否支持外部触发。</summary>
        external_trigger,

        /// <summary>查询当前采集字段模式（奇/偶/全帧）。</summary>
        field,

        /// <summary>查询通用属性。</summary>
        generic,

        /// <summary>查询水平分辨率。</summary>
        horizontal_resolution,

        /// <summary>查询图像高度（像素）。</summary>
        image_height,

        /// <summary>查询图像宽度（像素）。</summary>
        image_width,

        /// <summary>查询使用的端口号。</summary>
        port,

        /// <summary>查询采集起始列坐标。</summary>
        start_column,

        /// <summary>查询采集起始行坐标。</summary>
        start_row,

        /// <summary>查询垂直分辨率。</summary>
        vertical_resolution
    }
}
