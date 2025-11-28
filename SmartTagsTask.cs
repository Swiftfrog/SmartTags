using MediaBrowser.Model.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Serialization; // IJsonSerializer 命名空间
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Logging;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Net.Http; // IHttpClientFactory

namespace SmartTags;

public class SmartTagsTask : IScheduledTask
{
    private readonly ILogger _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IApplicationPaths _appPaths;

    // 构造函数注入所有我们未来需要的服务
    public SmartTagsTask(
        ILogger logger, 
        ILibraryManager libraryManager, 
        IJsonSerializer jsonSerializer, 
        IHttpClientFactory httpClientFactory,
        IApplicationPaths appPaths)
    {
        _logger = logger;
        _libraryManager = libraryManager;
        _jsonSerializer = jsonSerializer;
        _httpClientFactory = httpClientFactory;
        _appPaths = appPaths;
    }

    public string Name => "SmartTags Update";
    public string Key => "SmartTagsTask";
    public string Description => "扫描媒体库并更新 SmartTags 标签（原产地/年代/IMDb Top）。";
    public string Category => "SmartTags";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // 默认手动触发，或者你可以设置为每天一次
        yield return new TaskTriggerInfo { Type = TaskTriggerInfo.TriggerDaily, TimeOfDayTicks = TimeSpan.FromHours(4).Ticks };
    }

    public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
    {
        _logger.Info("[SmartTags] 计划任务已启动。正在检查配置...");

        var config = Plugin.Instance.Configuration;

        if (string.IsNullOrEmpty(config.TmdbApiKey))
        {
            _logger.Warn("[SmartTags] 未配置 TMDB API Key，任务跳过。请在插件设置中填写 Key。");
            return;
        }

        _logger.Info($"[SmartTags] 配置检查通过。功能开关 - 原产地: {config.EnableCountryTags}, 年代: {config.EnableDecadeTags}, IMDbTop: {config.EnableImdbTopTags}");

        // TODO: 这里未来会调用处理逻辑
        await Task.CompletedTask;
    }
}
