using MediaBrowser.Model.Plugins;
using Emby.Web.GenericEdit;
using System.ComponentModel;

namespace SmartTags;

public class SmartTagsConfig : EditableOptionsBase
{
    public override string EditorTitle => "SmartTags Settings";
    
    // TMDB API Key (必须)
    [DisplayName("TMDB API Key")]
    [Description("申请地址：https://www.themoviedb.org/settings/api")]
    public string TmdbApiKey { get; set; } = "";

    // 功能开关
    [DisplayName("启用原产国标签")]
    [Description("是否启用添加原产国标签")]
    public bool EnableCountryTags { get; set; } = false;
    
    // === 新增：国家标签风格设置 ===
    [DisplayName("原产国标签格式")]
    [Description("选择生成的标签样式")]
    public CountryTagStyle CountryStyle { get; set; } = CountryTagStyle.NameOnly;
    
    [DisplayName("启用IMDB TOP 250 标签")]
    [Description("是否启用添加IMDB TOP 250标签")]
    public bool EnableImdbTopTags { get; set; } = false;

    // 格式化设置
    [DisplayName("启用年代标签")]
    [Description("是否启用添加年代标签")]
    public bool EnableDecadeTags { get; set; } = false;
    
    // === 修改：改为枚举选择 ===
    [DisplayName("年代标签风格")]
    [Description("选择年代标签的显示格式。注意：2位数字风格无法区分 1950 和 2050。")]
    public DecadeStyle DecadeStyle { get; set; } = DecadeStyle.FourDigits;
    
    // === V1.1 新增：媒体信息标签 ===
    [DisplayName("启用分辨率标签")]
    [Description("自动添加 4K, 1080p, 720p 等标签")]
    public bool EnableResolutionTags { get; set; } = false;

    [DisplayName("启用 HDR/画质标签")]
    [Description("自动添加 HDR, Dolby Vision 标签")]
    public bool EnableHdrTags { get; set; } = false;

    [DisplayName("启用音频格式标签")]
    [Description("自动添加 Atmos, DTS:X, TrueHD, DTS-HD 等次世代音轨标签")]
    public bool EnableAudioTags { get; set; } = false;
    
    [DisplayName("启用清理任务")]
    [Description("慎重！执行 '清除SmartTags生成的标签' 任务。此开关在 Emby 重启后会自动关闭。")]
    public bool EnableCleanup { get; set; } = false;
}

// 定义显示风格枚举
public enum CountryTagStyle
{
    [Description("仅名称 (例如: 香港)")]
    NameOnly,
    [Description("仅代码 (例如: HK)")]
    CodeOnly,
    [Description("名称和代码 (例如: 香港 (HK))")]
    NameAndCode
}

// 定义年代显示风格
public enum DecadeStyle
{
    [Description("4位数字 (推荐, 如: 1990年代)")]
    FourDigits, // 1990年代 - 无歧义
    [Description("2位数字 (如: 90年代)")]
    TwoDigits,  // 90年代 - 2000年会显示为 "00年代"
    [Description("英文风格 (如: 1990s)")]
    English     // 1990s
}