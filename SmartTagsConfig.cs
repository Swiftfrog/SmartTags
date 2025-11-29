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
    
    [DisplayName("启用IMDB TOP 250 标签")]
    [Description("是否启用添加IMDB TOP 250标签")]
    public bool EnableImdbTopTags { get; set; } = false;

    // 格式化设置
    [DisplayName("启用年代标签")]
    [Description("是否启用添加年代标签")]
    public bool EnableDecadeTags { get; set; } = false;
    
    [DisplayName("年代标签格式")]
    [Description("设置年代标签的格式，{0} 代表年代数字（如80, 90, 00）")]
    public string DecadeTagFormat { get; set; } = "{0}年代";
    
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
