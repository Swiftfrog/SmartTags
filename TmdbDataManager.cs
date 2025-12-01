using MediaBrowser.Model.Serialization;
using MediaBrowser.Common.Net;
using MediaBrowser.Common.Configuration;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace SmartTags;

public class TmdbCacheData
{
    public string TmdbId { get; set; } = string.Empty;
    public string? ImdbId { get; set; }
    public string? OriginalLanguage { get; set; }
    public List<string> OriginCountries { get; set; } = new();
    
    // === [修复] 添加缺失的属性 ===
    // 用于存储 production_countries (如 "US", "GB")，RegionTagHelper 的兜底逻辑依赖它
    public List<string> ProductionCountries { get; set; } = new();
    
    // V1.2 变更：分开存储 ID，避免冲突
    public List<int> ProductionCompanyIds { get; set; } = new(); 
    public List<int> NetworkIds { get; set; } = new();

    public DateTime LastUpdated { get; set; }
}

public class TmdbDataManager
{
    private readonly string _cacheFilePath;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly IHttpClient _httpClient;
    private ConcurrentDictionary<string, TmdbCacheData> _cache;

    // === V1.1 Fix: 静态锁，确保所有实例共用一个限流器 ===
    private static readonly object _fileLock = new object();
    private static DateTime _lastRequestTime = DateTime.MinValue;
    private static readonly TimeSpan _minRequestInterval = TimeSpan.FromMilliseconds(300);

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
        if (_cache.TryGetValue(tmdbId, out var cachedItem)) return cachedItem;

        var fetchedItem = await FetchFromTmdb(tmdbId, type, apiKey, cancellationToken);
        
        if (fetchedItem != null)
        {
            _cache[tmdbId] = fetchedItem;
            SaveCache();
        }
        return fetchedItem;
    }

    private async Task<TmdbCacheData?> FetchFromTmdb(string tmdbId, string type, string apiKey, CancellationToken cancellationToken)
    {
        // === 静态节流逻辑 ===
        TimeSpan delay = TimeSpan.Zero;
        lock (_fileLock) // 简单的锁保护时间计算
        {
            var timeSinceLast = DateTime.UtcNow - _lastRequestTime;
            if (timeSinceLast < _minRequestInterval)
            {
                delay = _minRequestInterval - timeSinceLast;
            }
            // 预支时间，占位
            _lastRequestTime = DateTime.UtcNow + delay;
        }
        
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }
        // === 结束节流 ===

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
            var details = _jsonSerializer.DeserializeFromStream<TmdbItemDetails>(response.Content);

            if (details == null) return null;

            return new TmdbCacheData
            {
                TmdbId = tmdbId,
                LastUpdated = DateTime.Now,
                ImdbId = details.ImdbId,
                OriginalLanguage = details.OriginalLanguage,
                OriginCountries = details.OriginCountry ?? new List<string>(),
                
                // 1. 提取制作国家代码 (注意属性名变化)
                // details.ProductionCountryList -> TmdbCountry -> IsoCode
                ProductionCountries = details.ProductionCountryList?.Select(c => c.IsoCode ?? "").ToList() ?? new List<string>(),
                
                // 2. 提取制作公司 ID
                ProductionCompanyIds = details.ProductionCompanies?.Select(c => c.Id).ToList() ?? new List<int>(),
                
                // 3. 提取电视网 ID
                NetworkIds = details.Networks?.Select(n => n.Id).ToList() ?? new List<int>()
                
                
            };
        }
        catch (Exception)
        {
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