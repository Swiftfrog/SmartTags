using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using MediaBrowser.Model.Querying;
using SmartTags.Data;
using SmartTags.Logic;
using System.Net.Http;

namespace SmartTags.ScheduledTasks;

public class SmartTagsTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger _logger;
    private readonly TmdbDataManager _dataManager;
    private readonly IApplicationPaths _appPaths;

    public SmartTagsTask(ILibraryManager libraryManager, ILogger logger, IApplicationPaths appPaths, IHttpClientFactory httpClientFactory)
    {
        _libraryManager = libraryManager;
        _logger = logger;
        _appPaths = appPaths;
        // 初始化数据管理器
        _dataManager = new TmdbDataManager(appPaths, httpClientFactory);
    }

    public string Name => "SmartTags Update";
    public string Key => "SmartTagsTask";
    public string Description => "基于 TMDB 和 IMDb 生成智能标签（地区/年代/Top250）。";
    public string Category => "SmartTags";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo { Type = TaskTriggerInfo.TriggerDaily, TimeOfDayTicks = TimeSpan.FromHours(4).Ticks };
    }

    public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
    {
        var config = Plugin.Instance.Configuration;
        
        if (string.IsNullOrEmpty(config.TmdbApiKey))
        {
            _logger.Error("[SmartTags] 未配置 TMDB API Key，任务终止。");
            return;
        }

        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { "Movie", "Series" },
            Recursive = true,
            IsVirtualItem = false
        };

        var items = _libraryManager.GetItemList(query);
        var total = items.Length;
        int processed = 0;
        int updated = 0;

        _logger.Info($"[SmartTags] 开始处理 {total} 个项目...");

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool isModified = false;

            // --- 1. 年代标签 (纯本地计算) ---
            if (config.EnableDecadeTags && item.ProductionYear.HasValue)
            {
                string decadeTag = GetDecadeTag(item.ProductionYear.Value, config.DecadeTagFormat);
                if (AddTag(item, decadeTag)) isModified = true;
            }

            // --- 2. 地区标签 (需 TMDB 数据) ---
            if (config.EnableCountryTags)
            {
                var tmdbId = item.GetProviderId(MetadataProvider.Tmdb);
                if (!string.IsNullOrEmpty(tmdbId))
                {
                    string type = item is MediaBrowser.Controller.Entities.Movies.Movie ? "movie" : "tv";
                    
                    // 获取数据 (带缓存)
                    var tmdbData = await _dataManager.GetMetadataAsync(tmdbId, type, config.TmdbApiKey, cancellationToken);
                    
                    if (tmdbData != null)
                    {
                        string regionTag = RegionLogic.GetSmartRegionTag(tmdbData);
                        if (!string.IsNullOrEmpty(regionTag))
                        {
                            if (AddTag(item, regionTag)) isModified = true;
                        }

                        // --- 3. IMDb Top 250 (如果 TMDB 返回了 IMDb ID，也顺便处理) ---
                        // (注：这里简化处理，如果需要极高精度的 Top250 列表，建议还是保留你 PinyinSeek 里的下载 JSON 逻辑)
                        // 这里我们假设你已经有了 IMDb ID，可以后续扩展比对列表逻辑
                    }
                }
            }

            if (isModified)
            {
                item.UpdateToRepository(ItemUpdateType.MetadataEdit);
                updated++;
            }

            processed++;
            progress.Report((double)processed / total * 100);
        }

        // 保存缓存到磁盘
        _dataManager.SaveCacheIfNeeded();
        _logger.Info($"[SmartTags] 处理完成。更新了 {updated} 个项目。");
    }

    private string GetDecadeTag(int year, string format)
    {
        if (year < 1900) return "更早";
        int decade = (year / 10) * 10;
        return string.Format(format, decade);
    }

    private bool AddTag(BaseItem item, string tag)
    {
        if (string.IsNullOrEmpty(tag)) return false;
        
        // 避免重复
        if (item.Tags != null && item.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            return false;

        var tags = (item.Tags ?? Array.Empty<string>()).ToList();
        tags.Add(tag);
        item.Tags = tags.ToArray();
        return true;
    }
}
