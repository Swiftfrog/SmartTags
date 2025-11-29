using MediaBrowser.Model.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Serialization; // 新增引用
using MediaBrowser.Common.Configuration; // 新增引用
using MediaBrowser.Controller.Entities.Movies; // 引用 Movie
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;

namespace SmartTags;

public class SmartTagsCleanupTask : IScheduledTask
{
    private readonly ILogger _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly IJsonSerializer _jsonSerializer; // 新增
    private readonly IApplicationPaths _appPaths;     // 新增

    public SmartTagsCleanupTask(
        ILogger logger, 
        ILibraryManager libraryManager,
        IJsonSerializer jsonSerializer,
        IApplicationPaths appPaths)
    {
        _logger = logger;
        _libraryManager = libraryManager;
        _jsonSerializer = jsonSerializer;
        _appPaths = appPaths;
    }

    public string Name => "SmartTags Cleanup (Rollback)";
    public string Key => "SmartTagsCleanupTask";
    public string Description => "读取本地缓存，精准移除由 SmartTags 生成的标签。";
    public string Category => "SmartTags";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // 仅限手动触发
        return Enumerable.Empty<TaskTriggerInfo>();
    }

    public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
    {
        _logger.Info("[SmartTags] Cleanup 开始执行回滚任务...");
        var config = Plugin.Instance?.Configuration;
        
        // === 安全检查 ===
        // 注意：使用了 MetadataProviders.Tmdb (虽然这里主要用 config 检查)
        if (config == null || !config.EnableCleanup)
        {
            _logger.Warn("[SmartTags] Cleanup 任务被拒绝执行！");
            _logger.Warn("[SmartTags] Cleanup 【危险】清理开关未开启。请先在插件设置中勾选 '启用清理任务' 并保存，然后再次运行此任务。");
            return;
        }

        _logger.Info("[SmartTags] Cleanup 安全检查通过，开始执行回滚逻辑...");
        
        // 1. 读取 TMDB 缓存文件 (用于精准回滚原产地标签)
        var cachePath = Path.Combine(_appPaths.PluginConfigurationsPath, "SmartTags_TmdbCache.json");
        Dictionary<string, TmdbCacheData>? cache = null;

        if (File.Exists(cachePath))
        {
            try
            {
                cache = _jsonSerializer.DeserializeFromFile<Dictionary<string, TmdbCacheData>>(cachePath);
                _logger.Info($"[SmartTags] Cleanup 已加载缓存文件，包含 {cache?.Count ?? 0} 条数据。");
            }
            catch (Exception ex)
            {
                _logger.Error($"[SmartTags] Cleanup 缓存文件读取失败: {ex.Message}。将跳过原产地标签清理。");
            }
        }
        else
        {
            _logger.Warn("[SmartTags] Cleanup 未找到缓存文件 (SmartTags_TmdbCache.json)。将跳过原产地标签清理，仅清理年代和IMDb标签。");
        }

        // 2. 扫描所有项目
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { "Movie", "Series" },
            Recursive = true,
            IsVirtualItem = false
        };
        var items = _libraryManager.GetItemList(query);
        var total = items.Length;
        int processed = 0;
        int cleanedCount = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;
            if (processed % 100 == 0) progress.Report((double)processed / total * 100);

            if (item.Tags == null || item.Tags.Length == 0) continue;

            var currentTags = item.Tags.ToList();
            var tagsToRemove = new List<string>(); // 收集本项目需要删除的标签
            
            // === A. 精准清理原产地标签 (基于缓存) ===
            if (cache != null)
            {
                // 获取该项目的 TMDB ID
                var tmdbId = item.GetProviderId(MetadataProviders.Tmdb);
                if (!string.IsNullOrEmpty(tmdbId) && cache.TryGetValue(tmdbId, out var cacheData))
                {
                    // 核心逻辑：根据缓存数据，重新计算出“如果我们运行插件，会打什么标签”
                    // 然后只删除这一个特定的标签
                    var expectedTag = RegionTagHelper.GetRegionTag(cacheData);
                    
                    if (!string.IsNullOrEmpty(expectedTag) && currentTags.Contains(expectedTag, StringComparer.OrdinalIgnoreCase))
                    {
                        tagsToRemove.Add(expectedTag);
                    }
                }
            }

            // === B. 清理年代标签 (基于算法) ===
            // 只要项目有年份，我们就能算出插件会生成什么年代标签，如果存在就删掉
            if (item.ProductionYear.HasValue && item.ProductionYear.Value >= 1900)
            {
                string format = config?.DecadeTagFormat ?? "{0}年代";
                int decade = (item.ProductionYear.Value / 10) * 10;
                string expectedDecadeTag = string.Format(format, decade);

                if (currentTags.Contains(expectedDecadeTag, StringComparer.OrdinalIgnoreCase))
                {
                    tagsToRemove.Add(expectedDecadeTag);
                }
            }

            // === C. 清理 IMDb Top 250 (固定字符串) ===
            string imdbTag = "IMDb Top 250";
            if (currentTags.Contains(imdbTag, StringComparer.OrdinalIgnoreCase))
            {
                tagsToRemove.Add(imdbTag);
            }

            // === 执行删除 ===
            if (tagsToRemove.Count > 0)
            {
                // 从当前标签列表中移除收集到的标签
                bool listChanged = false;
                foreach (var tag in tagsToRemove)
                {
                    if (currentTags.Remove(tag))
                    {
                        listChanged = true;
                    }
                }

                if (listChanged)
                {
                    item.Tags = currentTags.ToArray();
                    item.UpdateToRepository(ItemUpdateType.MetadataEdit);
                    cleanedCount++;
                    _logger.Info($"[SmartTags] Cleanup 回滚项目: \"{item.Name}\" | 移除标签: [{string.Join(", ", tagsToRemove)}]");
                }
            }
        }

        _logger.Info($"[SmartTags] Cleanup 回滚完成。处理 {total} 个项目，实际回滚 {cleanedCount} 个。");
    }
}
