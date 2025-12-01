using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Common.Net;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;

namespace SmartTags;

public class SmartTagsMonitor : IServerEntryPoint
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger _logger;
    private readonly IApplicationPaths _appPaths;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly IHttpClient _httpClient;
    
    // 修复点1：声明为可空，消除 CS8618 警告
    private TmdbDataManager? _dataManager;
    private HashSet<string> _imdbTopIds = new();

    public SmartTagsMonitor(
        ILibraryManager libraryManager, 
        ILogger logger, 
        IApplicationPaths appPaths,
        IJsonSerializer jsonSerializer,
        IHttpClient httpClient)
    {
        _libraryManager = libraryManager;
        _logger = logger;
        _appPaths = appPaths;
        _jsonSerializer = jsonSerializer;
        _httpClient = httpClient;
    }

    public void Run()
    {
        // 1. 初始化 DataManager
        _dataManager = new TmdbDataManager(_appPaths, _jsonSerializer, _httpClient);
        
        // 2. 加载本地 IMDb 数据
        LoadImdbIdsFromLocal();

        // 3. 订阅事件
        _libraryManager.ItemUpdated += OnItemUpdated;
        _libraryManager.ItemAdded += OnItemAdded;
    }

    private void LoadImdbIdsFromLocal()
    {
        // 实时模式暂时留空，依赖计划任务填充数据
    }

    public void Dispose()
    {
        _libraryManager.ItemUpdated -= OnItemUpdated;
        _libraryManager.ItemAdded -= OnItemAdded;
    }

    private void OnItemAdded(object? sender, ItemChangeEventArgs e)
    {
        // 新增项目时，元数据可能还没刮削完，交给 OnItemUpdated 处理
    }

    private async void OnItemUpdated(object? sender, ItemChangeEventArgs e)
    {
        // === 核心检查：如果正在清理，直接忽略所有事件 ===
        if (Plugin.IsCleanupRunning) 
        {
            // 可选：打个 Debug 日志看是否生效 (太频繁可不打)
            // _logger.Debug("[SmartTags-Monitor] 清理任务正在运行，跳过实时处理。");
            return; 
        }     
                
        var config = Plugin.Instance?.Configuration;
        if (config == null || !config.EnableRealtimeMonitor) return;

        // 判空保护
        if (_dataManager == null) return;

        var item = e.Item;
        
        // 过滤类型：只处理 Movie 和 Series
        if (item is not MediaBrowser.Controller.Entities.Movies.Movie && 
            item is not MediaBrowser.Controller.Entities.TV.Series)
            return;

        // 避免处理虚拟项目
        if (item.IsVirtualItem) return;

        try
        {
            // 每次事件创建一个 Service 实例
            var service = new TaggingService(_logger, _dataManager, _imdbTopIds);
            
            // 设置 30秒 超时
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            
            bool changed = await service.ProcessItemAsync(item, config, cts.Token);

            if (changed)
            {
                // 修复点2：改回同步方法 UpdateToRepository，消除 CS1061 错误
                // Emby 内部会处理数据库写入，不需要 await
                item.UpdateToRepository(ItemUpdateType.MetadataEdit);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"[SmartTags-Monitor] 处理 {item.Name} 时出错: {ex.Message}");
        }
    }
}