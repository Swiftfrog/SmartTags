using SmartTags.Data;
using System.Collections.Generic;
using System.Linq;
using System;

namespace SmartTags.Logic;

public static class RegionLogic
{
    // ISO 3166-1 alpha-2 到中文的简单映射
    private static readonly Dictionary<string, string> CodeToNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "CN", "中国大陆" }, { "HK", "香港" }, { "TW", "台湾" }, { "SG", "新加坡" }, { "MO", "澳门" },
        { "US", "美国" }, { "GB", "英国" }, { "JP", "日本" }, { "KR", "韩国" }, { "KP", "朝鲜" },
        { "FR", "法国" }, { "DE", "德国" }, { "ES", "西班牙" }, { "IT", "意大利" }, { "RU", "俄罗斯" },
        { "IN", "印度" }, { "TH", "泰国" }, { "VN", "越南" }, { "CA", "加拿大" }, { "AU", "澳大利亚" }
        // 可按需补充更多
    };

    // 语言到主要国家的映射 (用于校验)
    private static readonly Dictionary<string, HashSet<string>> LanguageToCountryMap = new()
    {
        { "zh", new HashSet<string> { "CN", "HK", "TW", "SG", "MO" } },
        { "cn", new HashSet<string> { "CN", "HK", "TW", "SG", "MO" } },
        { "ja", new HashSet<string> { "JP" } },
        { "ko", new HashSet<string> { "KR", "KP" } },
        { "en", new HashSet<string> { "US", "GB", "CA", "AU", "NZ", "IE" } }
    };

    public static string GetSmartRegionTag(TmdbCacheData data)
    {
        if (data == null) return null;

        string lang = data.OriginalLanguage?.ToLower() ?? "";
        string selectedCode = null;

        // --- 逻辑 1: 优先 OriginCountry ---
        // TMDB 的 origin_country 字段非常准，尤其是针对合拍片
        if (data.OriginCountries != null && data.OriginCountries.Count > 0)
        {
            selectedCode = data.OriginCountries[0];
        }

        // --- 逻辑 2: 语言 + 制作国家 校验 ---
        // 如果没有 OriginCountry，尝试从 ProductionCountries 里找一个讲该语言的国家
        else if (data.ProductionCountries != null && data.ProductionCountries.Count > 0 && !string.IsNullOrEmpty(lang))
        {
            if (LanguageToCountryMap.TryGetValue(lang, out var validCountries))
            {
                selectedCode = data.ProductionCountries.FirstOrDefault(c => validCountries.Contains(c));
            }
        }

        // --- 逻辑 3: 语言兜底 ---
        // 如果完全找不到国家代码，根据语言返回一个通用名称
        if (string.IsNullOrEmpty(selectedCode))
        {
            return lang switch
            {
                "ja" => "日本",
                "ko" => "韩国",
                "zh" or "cn" => "华语", // 无法确定具体地区
                "th" => "泰国",
                _ => null
            };
        }

        // 将代码转换为中文
        return CodeToNameMap.TryGetValue(selectedCode, out var name) ? name : selectedCode;
    }
}
