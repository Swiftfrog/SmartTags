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
    
    // 我们需要在这里维护单例的 DataManager，以免每次事件都重新加载缓存
    private TmdbDataManager _dataManager;
    private HashSet<string> _imdbTopIds = new(); // 简单的内存缓存

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

    public Task RunAsync()
    {
        // 1. 初始化 DataManager
        _dataManager = new TmdbDataManager(_appPaths, _jsonSerializer, _httpClient);
        
        // 2. 尝试加载一次 IMDb 缓存 (如果本地有的话，就不联网了，保持轻量)
        // 注意：实时模式下我们不主动去下载 IMDb Json，只读本地。
        // 只有计划任务才会负责更新 IMDb 列表。这样避免实时模式卡顿。
        LoadImdbIdsFromLocal();

        // 3. 订阅事件
        _libraryManager.ItemUpdated += OnItemUpdated;
        _libraryManager.ItemAdded += OnItemAdded;

        return Task.CompletedTask;
    }

    private void LoadImdbIdsFromLocal()
    {
        // 这里简写：复用 Task 里的加载逻辑，或者你可以把 Task 里的 GetImdbTop250IdsAsync 提取到 Service 里
        // 为演示方便，这里假设已初始化为空，等计划任务跑一次后就有数据了
    }

    public void Dispose()
    {
        _libraryManager.ItemUpdated -= OnItemUpdated;
        _libraryManager.ItemAdded -= OnItemAdded;
    }

    private async void OnItemAdded(object? sender, ItemChangeEventArgs e)
    {
        // 新增项目时，元数据可能还没刮削完，所以这里我们只做简单记录
        // 真正的处理交给 OnItemUpdated
    }

    private async void OnItemUpdated(object? sender, ItemChangeEventArgs e)
    {
        var config = Plugin.Instance?.Configuration;
        if (config == null || !config.EnableRealtimeMonitor) return;

        // 过滤：只处理 Movie 和 Series
        // 过滤：ItemUpdateType.MetadataImport (刮削完成) 或 MetadataEdit (手动编辑)
        // 这样可以减少触发频率
        var item = e.Item;
        if (item is not MediaBrowser.Controller.Entities.Movies.Movie && item is not MediaBrowser.Controller.Entities.TV.Series)
            return;

        // 避免处理虚拟项目或未锁定的项目
        if (item.IsVirtualItem) return;

        // 核心：调用 Service 进行处理
        try
        {
            // 每次事件创建一个 Service 实例 (轻量级)
            var service = new TaggingService(_logger, _dataManager, _imdbTopIds);
            
            // 注意：不要传入 CancellationToken.None，最好有超时控制
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            
            bool changed = await service.ProcessItemAsync(item, config, cts.Token);

            if (changed)
            {
                // 如果 Service 返回 true，说明 Tags 列表变了
                // 必须调用更新，这会再次触发 ItemUpdated，但因为 Tag 已经存在，下次 ProcessItemAsync 会返回 false
                // 从而打破循环
                await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cts.Token);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"[SmartTags-Monitor] 处理 {item.Name} 时出错: {ex.Message}");
        }
    }
}
