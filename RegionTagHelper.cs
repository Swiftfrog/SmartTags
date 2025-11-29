using System.Collections.Generic;
using System.Linq;

namespace SmartTags;

public static class RegionTagHelper
{
    // 语言 -> 主要使用该语言的国家/地区代码映射
    private static readonly Dictionary<string, HashSet<string>> LanguageToCountryMap = new()
    {
        { "zh", new HashSet<string> { "CN", "HK", "TW", "SG", "MO" } },
        { "cn", new HashSet<string> { "CN", "HK", "TW", "SG", "MO" } },
        { "bo", new HashSet<string> { "CN" } }, // 藏语
        { "en", new HashSet<string> { "US", "GB", "CA", "AU", "NZ", "IE" } },
        { "ja", new HashSet<string> { "JP" } },
        { "ko", new HashSet<string> { "KR", "KP" } },
        { "fr", new HashSet<string> { "FR", "BE", "CA", "CH" } },
        { "de", new HashSet<string> { "DE", "AT", "CH" } },
        { "es", new HashSet<string> { "ES", "MX", "AR", "CL", "CO" } }
    };

    // 简单的代码 -> 中文名映射 (你可以根据需要扩展，或者直接把 PinyinSeek 的 CountryMapper 拿过来用)
    private static readonly Dictionary<string, string> CountryCodeToName = new()
    {
        { "CN", "中国" }, { "HK", "香港" }, { "TW", "台湾" }, { "SG", "新加坡" },
        { "US", "美国" }, { "GB", "英国" }, { "JP", "日本" }, { "KR", "韩国" },
        { "FR", "法国" }, { "DE", "德国" }, { "IT", "意大利" }, { "ES", "西班牙" },
        { "RU", "俄罗斯" }, { "IN", "印度" }, { "TH", "泰国" }, { "VN", "越南" },
        { "CA", "加拿大" }, { "AU", "澳大利亚" }
    };

    public static string? GetRegionTag(TmdbCacheData data)
    {
        if (data == null) return null;

        string lang = data.OriginalLanguage?.ToLower() ?? "";
        
        // 1. 优先使用 OriginCountry (最精准)
        if (data.OriginCountries != null && data.OriginCountries.Count > 0)
        {
            return MapCodeToName(data.OriginCountries[0]);
        }

        // 2. 尝试从 ProductionCountries 中筛选符合语言的国家
        if (data.ProductionCountries != null && data.ProductionCountries.Count > 0 && !string.IsNullOrEmpty(lang))
        {
            if (LanguageToCountryMap.TryGetValue(lang, out var validCountries))
            {
                // 找到第一个既在制作列表中，又是该语言主要使用国的国家
                var match = data.ProductionCountries.FirstOrDefault(c => validCountries.Contains(c));
                if (!string.IsNullOrEmpty(match))
                {
                    return MapCodeToName(match);
                }
            }
        }

        // 3. 兜底逻辑
        return GetDefaultCountryByLanguage(lang);
    }

    private static string MapCodeToName(string code)
    {
        // 如果在字典里，返回中文名；否则直接返回代码(或者英文名)
        return CountryCodeToName.TryGetValue(code, out var name) ? name : code;
    }

    private static string? GetDefaultCountryByLanguage(string lang)
    {
        return lang switch
        {
            "ja" => "日本",
            "ko" => "韩国",
            "zh" or "cn" => "华语", // 无法区分具体地区时
            "en" => "英语",
            "fr" => "法国",
            "de" => "德国",
            "ru" => "俄罗斯",
            "th" => "泰国",
            _ => null
        };
    }
}
