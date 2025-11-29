// Plugin.cs
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
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

    public static Plugin? Instance { get; private set; }
    
    public SmartTagsConfig Configuration => GetOptions();

    public Plugin(IApplicationHost applicationHost) : base(applicationHost)
    {
        Instance = this;
        
        // === 核心逻辑：重启后自动关闭清理开关 ===
        // 注意：此时配置可能已经从 XML 加载了，我们需要检查并覆盖它
        var config = Configuration;
        if (config.EnableCleanup)
        {
            config.EnableCleanup = false;
            SaveConfiguration(); // 强制保存回磁盘，确保 UI 上显示为关闭
        }
    }
}