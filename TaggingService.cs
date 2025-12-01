using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV; // <--- 修复点：添加 TV 引用
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace SmartTags;

/// <summary>
/// 封装通用的打标逻辑，供计划任务和实时监听共同调用
/// </summary>
public class TaggingService
{
    private readonly ILogger _logger;
    private readonly TmdbDataManager _dataManager;
    private readonly HashSet<string> _imdbTopIds;

    public TaggingService(ILogger logger, TmdbDataManager dataManager, HashSet<string> imdbTopIds)
    {
        _logger = logger;
        _dataManager = dataManager;
        _imdbTopIds = imdbTopIds;
    }

    public async Task<bool> ProcessItemAsync(BaseItem item, SmartTagsConfig config, CancellationToken cancellationToken)
    {
        // 过滤掉文件夹、集合等非媒体项
        if (item is not Movie && item is not Series && item is not Episode) 
            return false;

        bool isModified = false;
        List<string> addedLogTags = new List<string>();

        // === A. 年代标签 ===
        if (config.EnableDecadeTags)
        {
            var tag = TryGetDecadeTag(item, config.DecadeStyle);
            if (tag != null && AddTag(item, tag))
            {
                isModified = true;
                addedLogTags.Add(tag);
            }
        }

        // === V1.1 新增: 媒体信息标签 (本地逻辑) ===
        if (config.EnableResolutionTags || config.EnableHdrTags || config.EnableAudioTags)
        {
            var mediaTags = MediaInfoHelper.GetMediaInfoTags(item, config);
            foreach (var tag in mediaTags)
            {
                if (AddTag(item, tag))
                {
                    isModified = true;
                    addedLogTags.Add(tag);
                }
            }
        }

        // === B. IMDb Top 250 (仅限电影) ===
        if (config.EnableImdbTopTags && item is Movie movie)
        {
            var imdbId = movie.GetProviderId(MetadataProviders.Imdb);
            if (!string.IsNullOrEmpty(imdbId) && _imdbTopIds.Contains(imdbId))
            {
                string topTag = "IMDb Top 250";
                if (AddTag(item, topTag))
                {
                    isModified = true;
                    addedLogTags.Add(topTag);
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
                
                var data = await _dataManager.GetMetadataAsync(tmdbId, type, config.TmdbApiKey, cancellationToken);

                if (data != null)
                {
                    var regionTag = RegionTagHelper.GetRegionTag(data, config);
                    if (!string.IsNullOrEmpty(regionTag) && AddTag(item, regionTag))
                    {
                        isModified = true;
                        addedLogTags.Add(regionTag);
                    }
                }
            }
        }

        if (isModified)
        {
            _logger.Info($"[SmartTags] 自动标记: \"{item.Name}\" -> [{string.Join(", ", addedLogTags)}]");
        }

        return isModified;
    }

    private string? TryGetDecadeTag(BaseItem item, DecadeStyle style)
    {
        if (!item.ProductionYear.HasValue || item.ProductionYear.Value < 1850) return null;
        int year = item.ProductionYear.Value;
        int decade4 = (year / 10) * 10;
        return style switch
        {
            DecadeStyle.FourDigits => $"{decade4}年代",
            DecadeStyle.TwoDigits => $"{(decade4 % 100):00}年代",
            DecadeStyle.English => $"{decade4}s",
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
}