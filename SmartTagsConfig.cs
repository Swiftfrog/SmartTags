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
}
