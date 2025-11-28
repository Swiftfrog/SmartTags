using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using System;
using System.IO;

namespace SmartTags;

public class Plugin : BasePluginSimpleUI<SmartTagsConfig>
{
    public override string Name => "SmartTags";
    public override string Description => "智能元数据标签生成器 (地区/年代/IMDb)";
    public override Guid Id => new Guid("A111B222-C333-D444-E555-1234567890AB");

    public static Plugin Instance { get; private set; }

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }
}
