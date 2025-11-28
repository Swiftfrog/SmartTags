using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Serialization;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Json; // 需引用 System.Net.Http.Json

namespace SmartTags.Data;

public class TmdbCacheData
{
    public string TmdbId { get; set; }
    public string ImdbId { get; set; }
    public string OriginalLanguage { get; set; }
    public List<string> OriginCountries { get; set; } = new();
    public List<string> ProductionCountries { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

public class TmdbDataManager
{
    private readonly string _cacheFilePath;
    private readonly IHttpClientFactory _httpClientFactory;
    private ConcurrentDictionary<string, TmdbCacheData> _cache;
    private readonly object _fileLock = new object();
    private bool _hasChanges = false;

    public TmdbDataManager(IApplicationPaths appPaths, IHttpClientFactory httpClientFactory)
    {
        _cacheFilePath = Path.Combine(appPaths.PluginConfigurationsPath, "SmartTags_TmdbCache.json");
        _httpClientFactory = httpClientFactory;
        LoadCache();
    }

    public async Task<TmdbCacheData?> GetMetadataAsync(string tmdbId, string type, string apiKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(tmdbId) || string.IsNullOrEmpty(apiKey)) return null;

        // 1. 查缓存
        if (_cache.TryGetValue(tmdbId, out var cachedData))
        {
            return cachedData;
        }

        // 2. 缓存未命中，请求 API
        var newData = await FetchFromTmdb(tmdbId, type, apiKey, cancellationToken);
        if (newData != null)
        {
            _cache[tmdbId] = newData;
            _hasChanges = true;
        }

        return newData;
    }

    private async Task<TmdbCacheData?> FetchFromTmdb(string tmdbId, string type, string apiKey, CancellationToken cancellationToken)
    {
        // 仅请求需要的字段
        var url = $"https://api.themoviedb.org/3/{type}/{tmdbId}?api_key={apiKey}&append_to_response=credits";

        try
        {
            using var client = _httpClientFactory.CreateClient();
            var json = await client.GetFromJsonAsync<JsonElement>(url, cancellationToken);

            var data = new TmdbCacheData
            {
                TmdbId = tmdbId,
                LastUpdated = DateTime.Now
            };

            // 安全提取字段
            if (json.TryGetProperty("original_language", out var lang)) data.OriginalLanguage = lang.GetString();
            if (json.TryGetProperty("imdb_id", out var imdb)) data.ImdbId = imdb.GetString();

            // 提取 Origin Country (优先)
            if (json.TryGetProperty("origin_country", out var originArr))
            {
                foreach (var item in originArr.EnumerateArray())
                    data.OriginCountries.Add(item.GetString());
            }

            // 提取 Production Countries
            if (json.TryGetProperty("production_countries", out var prodArr))
            {
                foreach (var item in prodArr.EnumerateArray())
                {
                    if (item.TryGetProperty("iso_3166_1", out var iso))
                        data.ProductionCountries.Add(iso.GetString());
                }
            }

            return data;
        }
        catch
        {
            // Log error here if needed
            return null;
        }
    }

    private void LoadCache()
    {
        lock (_fileLock)
        {
            if (File.Exists(_cacheFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_cacheFilePath);
                    _cache = JsonSerializer.Deserialize<ConcurrentDictionary<string, TmdbCacheData>>(json) 
                             ?? new ConcurrentDictionary<string, TmdbCacheData>();
                }
                catch
                {
                    _cache = new ConcurrentDictionary<string, TmdbCacheData>();
                }
            }
            else
            {
                _cache = new ConcurrentDictionary<string, TmdbCacheData>();
            }
        }
    }

    public void SaveCacheIfNeeded()
    {
        if (!_hasChanges) return;

        lock (_fileLock)
        {
            var json = JsonSerializer.Serialize(_cache);
            File.WriteAllText(_cacheFilePath, json);
            _hasChanges = false;
        }
    }
}
