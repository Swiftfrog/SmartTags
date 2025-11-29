using MediaBrowser.Model.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Logging;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;     // 引用 BaseItem
using MediaBrowser.Model.Entities;          // 引用 MetadataProviders
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq;

namespace SmartTags;

public class SmartTagsTask : IScheduledTask
{
    private readonly ILogger _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly IHttpClient _httpClient;
    private readonly IApplicationPaths _appPaths;

    public SmartTagsTask(
        ILogger logger, 
        ILibraryManager libraryManager, 
        IJsonSerializer jsonSerializer, 
        IHttpClient httpClient,
        IApplicationPaths appPaths)
    {
        _logger = logger;
        _libraryManager = libraryManager;
        _jsonSerializer = jsonSerializer;
        _httpClient = httpClient;
        _appPaths = appPaths;
    }

    public string Name => "SmartTags Update";
    public string Key => "SmartTagsTask";
    public string Description => "基于 TMDB 数据更新原产地、年代及 IMDb Top 标签。";
    public string Category => "SmartTags";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo { Type = TaskTriggerInfo.TriggerDaily, TimeOfDayTicks = TimeSpan.FromHours(4).Ticks };
    }

    public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
    {
        var config = Plugin.Instance.Configuration;
        if (string.IsNullOrEmpty(config?.TmdbApiKey))
        {
            _logger.Warn("[SmartTags] 未配置 TMDB API Key，跳过任务。");
            return;
        }

        _logger.Info("[SmartTags] 开始执行标签更新任务...");

        // 1. 初始化数据管理器
        var dataManager = new TmdbDataManager(_appPaths, _jsonSerializer, _httpClient);

        // 2. 查询所有电影和剧集
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { "Movie", "Series" },
            Recursive = true,
            IsVirtualItem = false
        };
        var items = _libraryManager.GetItemList(query);
        var total = items.Length;
        
        int processed = 0;
        int updatedCount = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;
            progress.Report((double)processed / total * 100);

            bool isModified = false;

            // --- A. 年代标签 (纯本地逻辑) ---
            if (config.EnableDecadeTags)
            {
                if (TryAddDecadeTag(item, config.DecadeTagFormat)) isModified = true;
            }

            // --- B. 需要 TMDB 数据的标签 (原产地 & IMDb Top) ---
            if (config.EnableCountryTags || config.EnableImdbTopTags)
            {
                // 获取 TMDB ID
                var tmdbId = item.GetProviderId(MetadataProviders.Tmdb);
                
                if (!string.IsNullOrEmpty(tmdbId))
                {
                    // 获取元数据 (读缓存或请求API)
                    // "movie" or "tv"
                    string type = item is MediaBrowser.Controller.Entities.Movies.Movie ? "movie" : "tv";
                    
                    var data = await dataManager.GetMetadataAsync(tmdbId, type, config.TmdbApiKey, cancellationToken);

                    if (data != null)
                    {
                        // 处理原产地
                        if (config.EnableCountryTags)
                        {
                            var regionTag = RegionTagHelper.GetRegionTag(data);
                            if (!string.IsNullOrEmpty(regionTag) && !item.Tags.Contains(regionTag))
                            {
                                AddTag(item, regionTag);
                                isModified = true;
                            }
                        }

                        // 处理 IMDb Top (此处简化：如果 TMDB 数据里有 IMDb ID，且你有一个 Top250 的列表)
                        // 注意：为了实现 Top 250，我们需要一个 Top 250 的 ID 列表。
                        // 目前先留空，或者你可以复用 PinyinSeek 那个下载 JSON 的逻辑。
                        // if (config.EnableImdbTopTags && IsImdbTop250(data.ImdbId)) { ... }
                    }
                }
            }

            // --- 保存更改 ---
            if (isModified)
            {
                item.UpdateToRepository(ItemUpdateType.MetadataEdit);
                updatedCount++;
            }
        }

        _logger.Info($"[SmartTags] 任务完成。处理 {total} 个项目，更新 {updatedCount} 个。");
    }

    // 辅助方法：添加年代标签
    private bool TryAddDecadeTag(BaseItem item, string format)
    {
        if (!item.ProductionYear.HasValue) return false;
        
        int year = item.ProductionYear.Value;
        if (year < 1900) return false;

        int decade = (year / 10) * 10;
        // 格式化，例如 "{0}年代" -> "1980年代"
        // 注意：有些用户喜欢 "80年代"，那么逻辑是 decade % 100
        // 这里默认用 4 位年份。如果你想支持 "80年代"，可以判断 decade > 1900 ? decade % 100 : decade
        
        string tag = string.Format(format, decade); // "1980年代"

        if (!item.Tags.Contains(tag))
        {
            AddTag(item, tag);
            return true;
        }
        return false;
    }

    private void AddTag(BaseItem item, string tag)
    {
        if (item.Tags == null)
        {
            item.Tags = new[] { tag };
        }
        else if (!item.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            var list = item.Tags.ToList();
            list.Add(tag);
            item.Tags = list.ToArray();
        }
    }
}