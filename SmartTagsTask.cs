using MediaBrowser.Model.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Logging;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;     
using MediaBrowser.Model.Entities;          
using MediaBrowser.Controller.Entities.Movies; // 引用 Movie 类型
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq;
using System.IO;
using System.Runtime.Serialization; // 用于 DataMember

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
        var config = Plugin.Instance?.Configuration;
        if (config == null || string.IsNullOrEmpty(config.TmdbApiKey))
        {
            _logger.Warn("[SmartTags] 未配置 TMDB API Key，跳过任务。");
            return;
        }

        _logger.Info("[SmartTags] 任务开始。正在初始化...");

        // 1. 准备 IMDb Top 250 数据 (如果启用)
        HashSet<string> imdbTopIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (config.EnableImdbTopTags)
        {
            _logger.Debug("[SmartTags] 正在获取 IMDb Top 250 列表...");
            imdbTopIds = await GetImdbTop250IdsAsync(cancellationToken);
            _logger.Info($"[SmartTags] 加载了 {imdbTopIds.Count} 个 IMDb Top 250 条目。");
        }

        // 2. 初始化数据管理器
        var dataManager = new TmdbDataManager(_appPaths, _jsonSerializer, _httpClient);

        // 3. 查询
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { "Movie", "Series" },
            Recursive = true,
            IsVirtualItem = false
        };
        var items = _libraryManager.GetItemList(query);
        var total = items.Length;
        
        _logger.Info($"[SmartTags] 共扫描到 {total} 个媒体项。");
        
        int processed = 0;
        int updatedCount = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;
            if (processed % 50 == 0) progress.Report((double)processed / total * 100);

            bool isModified = false;
            List<string> addedLogTags = new List<string>(); // 用于记录本次添加了什么标签

            // === A. 年代标签 ===
            if (config.EnableDecadeTags)
            {
                var tag = TryGetDecadeTag(item, config.DecadeTagFormat);
                if (tag != null && AddTag(item, tag))
                {
                    isModified = true;
                    addedLogTags.Add(tag);
                }
            }

            // === V1.1 新增: 媒体信息标签 (本地逻辑，速度快) ===
            // 只要开启了任意一个媒体标签开关
            if (config.EnableResolutionTags || config.EnableHdrTags || config.EnableAudioTags)
            {
                // 调用 Helper 获取建议的标签列表
                var mediaTags = MediaInfoHelper.GetMediaInfoTags(item, config);
                
                foreach (var tag in mediaTags)
                {
                    // 使用之前拆分的 AddTag 逻辑 (带 Debug 日志)
                    if (AddTag(item, tag))
                    {
                        isModified = true;
                        addedLogTags.Add(tag);
                    }
                    else
                    {
                        // 可选：如果觉得日志太吵，可以注释掉这行 Media Info 的跳过日志
                        _logger.Debug($"[SmartTags] 媒体标签已存在: \"{item.Name}\" -> [{tag}]");
                    }
                }
            }

            // === B. IMDb Top 250 (仅限电影) ===
            if (config.EnableImdbTopTags && item is Movie movie)
            {
                var imdbId = movie.GetProviderId(MetadataProviders.Imdb); // 你的修正
                if (!string.IsNullOrEmpty(imdbId) && imdbTopIds.Contains(imdbId))
                {
                    string topTag = "IMDb Top 250";
                    
                    if (AddTag(item, topTag))
                    {
                        isModified = true;
                        addedLogTags.Add(topTag);
                    }
                    else
                    {
                        _logger.Debug($"[SmartTags] IMDb标签已存在，跳过: \"{item.Name}\"");
                    }
                }
            }

            // === C. TMDB 原产国标签 ===
            if (config.EnableCountryTags)
            {
                var tmdbId = item.GetProviderId(MetadataProviders.Tmdb);
                if (!string.IsNullOrEmpty(tmdbId))
                {
                    string type = item is Movie ? "movie" : "tv";
                    
                    // Log Debug: 流程追踪
                    // _logger.Debug($"[SmartTags] 处理: {item.Name} | ID: {tmdbId} | 正在获取元数据...");

                    var data = await dataManager.GetMetadataAsync(tmdbId, type, config.TmdbApiKey, cancellationToken);

                    if (data != null)
                    {
                        // var regionTag = RegionTagHelper.GetRegionTag(data);
                        var regionTag = RegionTagHelper.GetRegionTag(data, config);
                        _logger.Debug($"[SmartTags] 获取成功。语言: {data.OriginalLanguage}, 产地: {string.Join(",", data.OriginCountries)} => 判定标签: {regionTag}");

                        if (!string.IsNullOrEmpty(regionTag))
                        {
                            // 修改点：拆分逻辑以支持 else 日志
                            if (AddTag(item, regionTag))
                            {
                                isModified = true;
                                addedLogTags.Add(regionTag);
                            }
                            else
                            {
                                // 这里满足你的诉求：明确告知已存在
                                _logger.Debug($"[SmartTags] 标签已存在，跳过: \"{item.Name}\" -> [{regionTag}]");
                            }
                        }
                    }
                    else
                    {
                        _logger.Debug($"[SmartTags] 元数据获取失败 (Null): {item.Name}");
                    }
                    
                }
            }

            // === 保存更改 ===
            if (isModified)
            {
                item.UpdateToRepository(ItemUpdateType.MetadataEdit);
                updatedCount++;
                // Log Info: 告知添加了什么
                _logger.Info($"[SmartTags] 更新项目: \"{item.Name}\" | 新增标签: [{string.Join(", ", addedLogTags)}]");
            }
        }

        _logger.Info($"[SmartTags] 任务完成。处理 {total} 个项目，实际更新 {updatedCount} 个。");
    }

    // --- 辅助方法: IMDb Top 250 ---
    private async Task<HashSet<string>> GetImdbTop250IdsAsync(CancellationToken cancellationToken)
    {
        var cachePath = Path.Combine(_appPaths.PluginConfigurationsPath, "SmartTags_ImdbTop250.json");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. 尝试从网络下载
        bool needDownload = true;
        if (File.Exists(cachePath))
        {
            var lastWrite = File.GetLastWriteTimeUtc(cachePath);
            // 缓存有效期 24 小时
            if ((DateTime.UtcNow - lastWrite).TotalHours < 24)
            {
                needDownload = false;
                _logger.Debug("[SmartTags] IMDb Top 250 缓存未过期，使用本地文件。");
            }
        }

        if (needDownload)
        {
            try 
            {
                var url = "https://raw.githubusercontent.com/theapache64/top250/master/top250_min.json";
                var options = new HttpRequestOptions 
                { 
                    Url = url, 
                    CancellationToken = cancellationToken, 
                    BufferContent = true,
                    EnableHttpCompression = true // 开启压缩，省流量
                };
                
                using var response = await _httpClient.GetResponse(options).ConfigureAwait(false);
                using var stream = response.Content;
                using var fileStream = new FileStream(cachePath, FileMode.Create, FileAccess.Write);
                await stream.CopyToAsync(fileStream);
                _logger.Info("[SmartTags] IMDb Top 250 数据已从 GitHub 更新。");
            }
            catch (Exception ex)
            {
                _logger.Error($"[SmartTags] 下载 IMDb Top 250 失败: {ex.Message}。将尝试使用旧缓存。");
            }
        }

        // 2. 读取本地缓存并解析
        if (File.Exists(cachePath))
        {
            try
            {
                // 使用更新后的 DTO
                var list = _jsonSerializer.DeserializeFromFile<List<ImdbSimpleItem>>(cachePath);
                
                if (list != null)
                {
                    foreach (var i in list)
                    {
                        // JSON 格式: "/title/tt0055630/"
                        if (!string.IsNullOrEmpty(i.ImdbUrl))
                        {
                            // Split('/') 会把 "/title/ttxxx/" 切割成 ["", "title", "ttxxx", ""]
                            // 依然可以稳健地找到以 "tt" 开头的那一段
                            var parts = i.ImdbUrl.Split('/');
                            var ttId = parts.FirstOrDefault(p => p.StartsWith("tt", StringComparison.OrdinalIgnoreCase));
                            
                            if (!string.IsNullOrEmpty(ttId))
                            {
                                ids.Add(ttId);
                            }
                        }
                    }
                    _logger.Info($"[SmartTags] 成功解析 {ids.Count} 个 IMDb Top 250 ID。");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[SmartTags] 解析 IMDb 缓存失败: {ex.Message}");
            }
        }

        return ids;
    }

    // --- 辅助方法: 标签操作 ---
    // private string? TryGetDecadeTag(BaseItem item, string format)
    // {
    //     if (!item.ProductionYear.HasValue || item.ProductionYear.Value < 1900) return null;
    //     int decade = (item.ProductionYear.Value / 10) * 10;
    //     return string.Format(format, decade);
    // }

    // private string? TryGetDecadeTag(BaseItem item, string format)
    // {
    //     if (!item.ProductionYear.HasValue || item.ProductionYear.Value < 1900) return null;
    //     
    //     int year = item.ProductionYear.Value;
    //     int decade4 = (year / 10) * 10; // 1990
    //     int decade2 = decade4 % 100;    // 90
// 
    //     // 核心修改：传入两个参数
    //     // {0} = 1990, {1} = 90
    //     return string.Format(format, decade4, decade2);
    // }
    
    private string? TryGetDecadeTag(BaseItem item, DecadeStyle style)
    {
        if (!item.ProductionYear.HasValue || item.ProductionYear.Value < 1850) return null; // 过滤掉太早的年份
        
        int year = item.ProductionYear.Value;
        int decade4 = (year / 10) * 10; // e.g. 2008 -> 2000

        return style switch
        {
            DecadeStyle.FourDigits => $"{decade4}年代",  // "2000年代"
            
            // 修复点：使用 {0:00} 强制补零
            DecadeStyle.TwoDigits => $"{(decade4 % 100):00}年代", // 2000-> "00年代", 1990-> "90年代"
            
            DecadeStyle.English => $"{decade4}s",       // "2000s"
            _ => $"{decade4}年代"
        };
    }

    private bool AddTag(BaseItem item, string tag)
    {
        if (item.Tags == null)
        {
            item.Tags = new[] { tag };
            return true;
        }
        if (!item.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            var list = item.Tags.ToList();
            list.Add(tag);
            item.Tags = list.ToArray();
            return true;
        }
        return false;
    }

    // 用于解析 IMDb JSON 的内部类
    [DataContract]
    private class ImdbSimpleItem
    {
        [DataMember(Name = "imdb_url")]
        public string? ImdbUrl { get; set; }
    }
}