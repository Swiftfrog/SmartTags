// Plugin.cs
using MediaBrowser.Common;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Plugins;
using System;
using System.IO;

namespace SmartTags;

public class Plugin : BasePluginSimpleUI<SmartTagsConfig>
{
    public override string Name => "SmartTags";
    public override string Description => "基于 TMDB 数据自动管理原产地、年代及 IMDb Top 250 标签。";
    public override Guid Id => new Guid("AA721234-B111-4222-A333-123456789000");

    public static Plugin Instance { get; private set; }

    public Plugin(IApplicationHost applicationHost) : base(applicationHost)
    {
        Instance = this;
    }
}