// TmdbDataManager.cs
using MediaBrowser.Model.Serialization;
using MediaBrowser.Common.Net;
using MediaBrowser.Common.Configuration;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq; // 用于 Select

namespace SmartTags;

// 这是存入本地磁盘的精简缓存结构（保持不变）
public class TmdbCacheData
{
    public string TmdbId { get; set; } = string.Empty;
    public string? ImdbId { get; set; }
    public string? OriginalLanguage { get; set; }
    public List<string> OriginCountries { get; set; } = new();
    public List<string> ProductionCountries { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

public class TmdbDataManager
{
    private readonly string _cacheFilePath;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly IHttpClient _httpClient;
    private ConcurrentDictionary<string, TmdbCacheData> _cache;
    private readonly object _fileLock = new object();

    public TmdbDataManager(IApplicationPaths appPaths, IJsonSerializer jsonSerializer, IHttpClient httpClient)
    {
        _cacheFilePath = Path.Combine(appPaths.PluginConfigurationsPath, "SmartTags_TmdbCache.json");
        _jsonSerializer = jsonSerializer;
        _httpClient = httpClient;
        _cache = new ConcurrentDictionary<string, TmdbCacheData>();
        LoadCache();
    }

    public async Task<TmdbCacheData?> GetMetadataAsync(string tmdbId, string type, string apiKey, CancellationToken cancellationToken)
    {
        // 1. 查内存缓存
        if (_cache.TryGetValue(tmdbId, out var cachedItem)) return cachedItem;

        // 2. 无缓存，去下载
        var fetchedItem = await FetchFromTmdb(tmdbId, type, apiKey, cancellationToken);
        
        if (fetchedItem != null)
        {
            _cache[tmdbId] = fetchedItem;
            SaveCache();
        }
        return fetchedItem;
    }

    // === 这里使用了你推荐的高效写法 ===
    private async Task<TmdbCacheData?> FetchFromTmdb(string tmdbId, string type, string apiKey, CancellationToken cancellationToken)
    {
        var url = $"https://api.themoviedb.org/3/{type}/{tmdbId}?api_key={apiKey}";
        
        var options = new HttpRequestOptions
        {
            Url = url,
            CancellationToken = cancellationToken,
            TimeoutMs = 10000,
            BufferContent = true,
            EnableHttpCompression = true
        };

        try
        {
            using var response = await _httpClient.GetResponse(options).ConfigureAwait(false);
            
            // 直接反序列化为实体类，干净利落！
            var details = _jsonSerializer.DeserializeFromStream<TmdbItemDetails>(response.Content);

            if (details == null) return null;

            // 将 API 返回的复杂对象转换为我们简单的缓存对象
            return new TmdbCacheData
            {
                TmdbId = tmdbId,
                LastUpdated = DateTime.Now,
                ImdbId = details.ImdbId,
                OriginalLanguage = details.OriginalLanguage,
                // 处理可能为 null 的列表
                OriginCountries = details.OriginCountry ?? new List<string>(),
                ProductionCountries = details.ProductionCountries?.Select(c => c.IsoCode ?? "").ToList() ?? new List<string>()
            };
        }
        catch (Exception)
        {
            // 网络错误或解析失败
            return null;
        }
    }

    private void LoadCache()
    {
        lock (_fileLock)
        {
            if (File.Exists(_cacheFilePath))
            {
                try {
                    var dict = _jsonSerializer.DeserializeFromFile<Dictionary<string, TmdbCacheData>>(_cacheFilePath);
                    if (dict != null) _cache = new ConcurrentDictionary<string, TmdbCacheData>(dict);
                } catch { _cache = new ConcurrentDictionary<string, TmdbCacheData>(); }
            }
        }
    }

    private void SaveCache()
    {
        lock (_fileLock)
        {
            _jsonSerializer.SerializeToFile(_cache, _cacheFilePath);
        }
    }
}
