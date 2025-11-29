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

        _logger.Info("[SmartTags] Cleanup 开始执行回滚任务...");

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
            
            // // === A. 精准清理原产地标签 (基于缓存) ===
            // var tmdbId = item.GetProviderId(MetadataProviders.Tmdb); // 获取 ID 的代码必须存在
// 
            // if (!string.IsNullOrEmpty(tmdbId) && cache != null && cache.TryGetValue(tmdbId, out var cacheData))
            // {
            //     // 传入 config (或全开的 tempConfig，如果是 Cleanup 这里的 config 就是 Plugin.Config)
            //     // 注意：如果用户改了格式（比如从“香港”改为“香港 (HK)”），
            //     // 这里只能算出“香港 (HK)”，所以旧的“香港”不会被删除。
            //     // 建议用户：先运行 Cleanup 清理旧格式，再改设置，再运行 Update。
            //     var expectedTag = RegionTagHelper.GetRegionTag(cacheData, config); 
            //     
            //     if (!string.IsNullOrEmpty(expectedTag) && currentTags.Contains(expectedTag, StringComparer.OrdinalIgnoreCase))
            //     {
            //         tagsToRemove.Add(expectedTag);
            //     }
            // }

            // === A. 精准清理原产地标签 (遍历所有风格) ===
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

            // === B. 精准清理媒体信息标签 (基于本地重新计算) ===
            // 调用 Helper 算出这部影片“理论上”会拥有的媒体标签
            var expectedMediaTags = MediaInfoHelper.GetMediaInfoTags(item, fullMediaConfig);
            foreach (var tag in expectedMediaTags)
            {
                if (currentTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                {
                    tagsToRemove.Add(tag);
                }
            }

            // === C. 清理年代标签 (基于算法) ===
            // if (item.ProductionYear.HasValue && item.ProductionYear.Value >= 1900)
            // {
            //     string format = config?.DecadeTagFormat ?? "{0}年代";
            //     int decade = (item.ProductionYear.Value / 10) * 10;
            //     string expectedDecadeTag = string.Format(format, decade);
// 
            //     if (currentTags.Contains(expectedDecadeTag, StringComparer.OrdinalIgnoreCase))
            //     {
            //         tagsToRemove.Add(expectedDecadeTag);
            //     }
            // }
            
            // === B. 清理年代标签 (基于算法) ===
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

        _logger.Info($"[SmartTags] Cleanup 完成。处理 {total} 个项目，实际回滚 {cleanedCount} 个。");
    }
}