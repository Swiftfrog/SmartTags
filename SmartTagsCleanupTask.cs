using MediaBrowser.Model.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Common.Configuration;
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
    private readonly IJsonSerializer _jsonSerializer;
    private readonly IApplicationPaths _appPaths;

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

    public string Name => "SmartTags Cleanup";
    public string Key => "SmartTagsCleanupTask";
    public string Description => "精准移除由 SmartTags 生成的标签（含原产地、年代、IMDb、媒体信息）。";
    public string Category => "SmartTags";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // 仅限手动触发
        return Enumerable.Empty<TaskTriggerInfo>();
    }

    public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
    {
        _logger.Info("[SmartTags] Cleanup 收到任务启动请求...");
        var config = Plugin.Instance?.Configuration;

        // 安全检查
        if (config == null || !config.EnableCleanup)
        {
            _logger.Warn("[SmartTags] Cleanup 任务被拒绝！原因：清理开关未开启。");
            return;
        }

        try
        {
            // === 上锁 ===
            Plugin.IsCleanupRunning = true;
            _logger.Info("[SmartTags】Cleanup 已激活全局清理锁，实时监听将暂停。");

            _logger.Info("[SmartTags] Cleanup 开始执行删除任务...");
    
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
                _logger.Warn("[SmartTags] Cleanup 未找到缓存文件。将跳过原产地标签清理。");
            }
    
            // 2. 准备一个“全开启”的临时配置，用于计算媒体信息标签
            // 即使当前用户关闭了媒体标签功能，我们也要能清理掉以前生成的标签
            var fullMediaConfig = new SmartTagsConfig
            {
                EnableResolutionTags = true,
                EnableHdrTags = true,
                EnableAudioTags = true
                // 其他字段默认即可，不影响 MediaInfoHelper 计算
            };
    
            // 3. 扫描
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
                var tagsToRemove = new List<string>(); 
                
                // === 精准清理原产地标签 (遍历所有风格) ===
                var tmdbId = item.GetProviderId(MetadataProviders.Tmdb);
    
                if (!string.IsNullOrEmpty(tmdbId) && cache != null && cache.TryGetValue(tmdbId, out var cacheData))
                {
                    // 核心修改：不再只读取 config.CountryStyle，而是遍历枚举中所有可能的风格
                    // 这样无论用户以前是用 "香港"、"HK" 还是 "香港 (HK)" 生成的，都能被清理掉
                    foreach (CountryTagStyle style in Enum.GetValues(typeof(CountryTagStyle)))
                    {
                        // 创建一个临时的 Config 对象，仅用于欺骗 Helper 计算不同风格的标签
                        var tempStyleConfig = new SmartTagsConfig 
                        { 
                            CountryStyle = style 
                        };
    
                        var expectedTag = RegionTagHelper.GetRegionTag(cacheData, tempStyleConfig);
                    
                        if (!string.IsNullOrEmpty(expectedTag) && currentTags.Contains(expectedTag, StringComparer.OrdinalIgnoreCase))
                        {
                            // 使用 HashSet 或检查 Contains 防止重复添加（虽然 List.Remove 也不怕重复）
                            if (!tagsToRemove.Contains(expectedTag))
                            {
                                tagsToRemove.Add(expectedTag);
                            }
                        }
                    }
                }
    
                // === 精准清理媒体信息标签 (基于本地重新计算) ===
                // 调用 Helper 算出这部影片“理论上”会拥有的媒体标签
                // var expectedMediaTags = MediaInfoHelper.GetMediaInfoTags(item, fullMediaConfig);
                // foreach (var tag in expectedMediaTags)
                // {
                //     if (currentTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                //     {
                //         tagsToRemove.Add(tag);
                //     }
                // }
                // 修正逻辑：不再重新计算“该有什么”，而是直接检查“有没有SmartTags的词”
                // 这样即使文件没有媒体信息，也能把残留的 "4K", "HDR" 等标签删干净
                var knownMediaTags = MediaInfoHelper.GetAllKnownMediaInfoTags();
                foreach (var tag in knownMediaTags)
                {
                    if (currentTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                    {
                        tagsToRemove.Add(tag);
                    }
                }

                // === 清理年代标签 (基于算法) ===
                if (item.ProductionYear.HasValue && item.ProductionYear.Value >= 1850)
                {
                    int year = item.ProductionYear.Value;
                    int decade4 = (year / 10) * 10;
                    int decade2 = decade4 % 100;
    
                    // 我们把所有可能生成的格式都算一遍，只要有就删
                    // 这样无论用户切换过什么设置，清理任务都能把它们干掉
                    var possibleTags = new List<string>
                    {
                        $"{decade4}年代",      // 1990年代
                        $"{decade2:00}年代",   // 90年代 / 00年代 (修复了0年代)
                        $"{decade2}年代",      // 兼容旧版 BUG 产生的 "0年代"
                        $"{decade4}s"          // 1990s
                    };
    
                    foreach (var tag in possibleTags)
                    {
                        if (currentTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                        {
                            tagsToRemove.Add(tag);
                        }
                    }
                }
    
                // === D. 清理 IMDb Top 250 ===
                string imdbTag = "IMDb Top 250";
                if (currentTags.Contains(imdbTag, StringComparer.OrdinalIgnoreCase))
                {
                    tagsToRemove.Add(imdbTag);
                }
                
                // === E. 清理制片商/流媒体标签 (基于已知列表) ===
                // 注意：这会检查所有 SmartTags "认识" 的厂牌名。
                // 如果用户手动打了一个 "Netflix"，也会被删掉。这是回滚的代价。
                var knownStudios = StudioMapper.GetAllKnownStudioTags();
                foreach (var studio in knownStudios)
                {
                    if (currentTags.Contains(studio, StringComparer.OrdinalIgnoreCase))
                    {
                        tagsToRemove.Add(studio);
                    }
                }
                   
                // === 执行删除 ===
                if (tagsToRemove.Count > 0)
                {
                    bool listChanged = false;
                    foreach (var tag in tagsToRemove)
                    {
                        // 使用 Remove 确保只删除匹配项
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
                        _logger.Info($"[SmartTags] Cleanup 项目: \"{item.Name}\" | 移除标签: [{string.Join(", ", tagsToRemove)}]");
                    }
                }
            }
    
            _logger.Info($"[SmartTags] Cleanup 完成。处理 {total} 个项目，实际处理 {cleanedCount} 个。");
        }
        catch (Exception ex)
        {
        	_logger.Error($"[SmartTags] Cleanup 任务执行过程中发生错误: {ex.Message}");
            throw; // 抛出异常以便 Emby 界面显示任务失败
        }
        finally
        {
            // === 采纳你的建议：添加冷却延迟 ===
            // 防止 Emby 的事件队列有滞后，导致任务刚结束锁就开了，最后几个事件又把标签加回来了。
            _logger.Info("[SmartTags] Cleanup 正在等待事件队列冷却 (5秒)...");
            await Task.Delay(5000); // 延迟 5 秒释放锁

            // === 解锁 ===
            Plugin.IsCleanupRunning = false;
            _logger.Info("[SmartTags] Cleanup] 全局清理锁已释放。");
            
            // === 任务结束（无论成功失败），自动关闭开关 ===
            _logger.Info("[SmartTags] Cleanup 任务结束，正在自动关闭危险操作开关...");
            Plugin.Instance?.OnCleanupFinished();
        }   
    }
}