using System.Collections.Generic;
using System.Linq;

namespace SmartTags;

public static class RegionTagHelper
{
    // 语言代码 (ISO 639-1) -> 主要使用该语言的国家/地区代码 (ISO 3166-1)
    private static readonly Dictionary<string, HashSet<string>> LanguageToCountryMap = new()
    {
        // === 中文系 ===
        { "zh", new HashSet<string> { "CN", "HK", "TW", "SG", "MO" } },
        { "cn", new HashSet<string> { "CN", "HK", "TW", "SG", "MO" } }, // TMDB 偶尔返回
        { "bo", new HashSet<string> { "CN" } }, // 藏语

        // === 英语系 (覆盖主要英语产地) ===
        { "en", new HashSet<string> { "US", "GB", "CA", "AU", "NZ", "IE", "ZA", "PH", "SG" } },

        // === 日韩 ===
        { "ja", new HashSet<string> { "JP" } },
        { "ko", new HashSet<string> { "KR", "KP" } },

        // === 欧洲主要语种 ===
        { "fr", new HashSet<string> { "FR", "BE", "CA", "CH", "MA" } }, // 法语：法国, 比利时, 魁北克, 瑞士, 摩洛哥
        { "de", new HashSet<string> { "DE", "AT", "CH", "DD" } },       // 德语：含东德(DD)
        { "it", new HashSet<string> { "IT", "CH" } },                   // 意大利语
        { "es", new HashSet<string> { "ES", "MX", "AR", "CL", "CO", "PE", "CU", "VE" } }, // 西班牙语：含拉美
        { "pt", new HashSet<string> { "PT", "BR" } },                   // 葡萄牙语：葡萄牙, 巴西
        { "ru", new HashSet<string> { "RU", "SU", "UA", "KZ", "BY" } }, // 俄语：含苏联(SU)
        { "nl", new HashSet<string> { "NL", "BE" } },                   // 荷兰语

        // === 北欧 ===
        { "sv", new HashSet<string> { "SE" } }, // 瑞典语
        { "no", new HashSet<string> { "NO" } }, // 挪威语
        { "da", new HashSet<string> { "DK" } }, // 丹麦语
        { "fi", new HashSet<string> { "FI" } }, // 芬兰语
        { "is", new HashSet<string> { "IS" } }, // 冰岛语

        // === 东欧/中欧 ===
        { "pl", new HashSet<string> { "PL" } },             // 波兰语
        { "cs", new HashSet<string> { "CZ", "CS", "XC" } }, // 捷克语：含捷克斯洛伐克
        { "hu", new HashSet<string> { "HU" } },             // 匈牙利语
        { "ro", new HashSet<string> { "RO" } },             // 罗马尼亚语
        { "bg", new HashSet<string> { "BG" } },             // 保加利亚语
        { "sr", new HashSet<string> { "RS", "YU", "HR", "BA" } }, // 塞尔维亚/克罗地亚语：含南斯拉夫(YU)
        { "hr", new HashSet<string> { "HR", "YU" } },
        { "uk", new HashSet<string> { "UA", "SU" } },       // 乌克兰语

        // === 亚洲其他 ===
        { "hi", new HashSet<string> { "IN" } }, // 印地语
        { "ta", new HashSet<string> { "IN" } }, // 泰米尔语
        { "th", new HashSet<string> { "TH" } }, // 泰语
        { "vi", new HashSet<string> { "VN" } }, // 越南语
        { "id", new HashSet<string> { "ID" } }, // 印尼语
        { "ms", new HashSet<string> { "MY", "SG" } }, // 马来语
        { "tl", new HashSet<string> { "PH" } }, // 他加禄语 (菲律宾)
        { "fa", new HashSet<string> { "IR" } }, // 波斯语
        { "he", new HashSet<string> { "IL" } }, // 希伯来语
        { "tr", new HashSet<string> { "TR" } }, // 土耳其语

        // === 阿拉伯语系 ===
        { "ar", new HashSet<string> { "EG", "SA", "AE", "QA", "MA", "IQ", "LB" } }
    };

    // 简单的代码 -> 中文名映射 (你可以根据需要扩展，或者直接把 PinyinSeek 的 CountryMapper 拿过来用)
    private static readonly Dictionary<string, string> CountryCodeToName = new()
    {
        // === 英语国家 ===
        { "US", "美国" }, { "GB", "英国" }, { "CA", "加拿大" }, { "AU", "澳大利亚" }, 
        { "NZ", "新西兰" }, { "IE", "爱尔兰" },

        // === 中文/东亚 ===
        { "CN", "中国" }, { "HK", "香港" }, { "TW", "台湾" }, { "SG", "新加坡" },
        { "JP", "日本" }, { "KR", "韩国" }, { "KP", "朝鲜" }, { "MO", "澳门" },

        // === 历史政权 (老电影常见) ===
        { "SU", "苏联" },            // Soviet Union
        { "CS", "捷克斯洛伐克" },     // Czechoslovakia
        { "YU", "南斯拉夫" },         // Yugoslavia
        { "DD", "东德" },            // East Germany
        { "XC", "捷克斯洛伐克" },     // TMDB 偶尔使用的非标准代码

        // === 欧洲主要 ===
        { "FR", "法国" }, { "DE", "德国" }, { "ES", "西班牙" }, { "IT", "意大利" },
        { "RU", "俄罗斯" }, { "PT", "葡萄牙" }, { "NL", "荷兰" }, { "BE", "比利时" },
        { "SE", "瑞典" }, { "NO", "挪威" }, { "DK", "丹麦" }, { "FI", "芬兰" },
        { "IS", "冰岛" }, { "CH", "瑞士" }, { "AT", "奥地利" }, { "GR", "希腊" },
        
        // === 欧洲其他 ===
        { "PL", "波兰" }, { "TR", "土耳其" }, { "UA", "乌克兰" }, { "CZ", "捷克" },
        { "HU", "匈牙利" }, { "RO", "罗马尼亚" }, { "BG", "保加利亚" }, { "RS", "塞尔维亚" },
        { "HR", "克罗地亚" }, { "SK", "斯洛伐克" },

        // === 美洲 ===
        { "BR", "巴西" }, { "MX", "墨西哥" }, { "AR", "阿根廷" }, { "CL", "智利" },
        { "CO", "哥伦比亚" }, { "PE", "秘鲁" }, { "CU", "古巴" }, { "VE", "委内瑞拉" },

        // === 亚洲其他 ===
        { "IN", "印度" }, { "TH", "泰国" }, { "VN", "越南" }, { "ID", "印尼" },
        { "PH", "菲律宾" }, { "MY", "马来西亚" }, { "PK", "巴基斯坦" }, { "IR", "伊朗" },
        { "IL", "以色列" }, { "SA", "沙特" }, { "AE", "阿联酋" }, { "QA", "卡塔尔" },
        { "KZ", "哈萨克斯坦" },

        // === 非洲 ===
        { "EG", "埃及" }, { "ZA", "南非" }, { "MA", "摩洛哥" }, { "NG", "尼日利亚" },
        { "KE", "肯尼亚" }
    };

    // public static string? GetRegionTag(TmdbCacheData data)
    // {
    //     if (data == null) return null;
// 
    //     string lang = data.OriginalLanguage?.ToLower() ?? "";
    //     
    //     // 1. 优先使用 OriginCountry (最精准)
    //     if (data.OriginCountries != null && data.OriginCountries.Count > 0)
    //     {
    //         return MapCodeToName(data.OriginCountries[0]);
    //     }
// 
    //     // 2. 尝试从 ProductionCountries 中筛选符合语言的国家
    //     if (data.ProductionCountries != null && data.ProductionCountries.Count > 0 && !string.IsNullOrEmpty(lang))
    //     {
    //         if (LanguageToCountryMap.TryGetValue(lang, out var validCountries))
    //         {
    //             // 找到第一个既在制作列表中，又是该语言主要使用国的国家
    //             var match = data.ProductionCountries.FirstOrDefault(c => validCountries.Contains(c));
    //             if (!string.IsNullOrEmpty(match))
    //             {
    //                 return MapCodeToName(match);
    //             }
    //         }
    //     }
// 
    //     // 3. 兜底逻辑
    //     return GetDefaultCountryByLanguage(lang);
    // }

    public static string? GetRegionTag(TmdbCacheData data, SmartTagsConfig config)
    {
        if (data == null) return null;

        string lang = data.OriginalLanguage?.ToLower() ?? "";
        string code = null;

        // === 逻辑升级 Start ===
        
        // 1. 优先分析 OriginCountries
        if (data.OriginCountries != null && data.OriginCountries.Count > 0)
        {
            // 情况 A: 只有一个原产地，直接用
            if (data.OriginCountries.Count == 1)
            {
                code = data.OriginCountries[0];
            }
            // 情况 B: 有多个原产地 (合拍片)，需要仲裁
            else
            {
                // 尝试用 ProductionCountries 的第一个作为主导国进行匹配
                var primaryProducer = data.ProductionCountries?.FirstOrDefault();
                
                if (!string.IsNullOrEmpty(primaryProducer) && data.OriginCountries.Contains(primaryProducer))
                {
                    // 命中！Production 里的老大在 Origin 名单里，听它的
                    // 案例：Origin=[ES, AR], Prod=[AR, ES] -> 选中 AR
                    code = primaryProducer;
                }
                else
                {
                    // 没命中，或者没有 Production 信息，只能回退到 Origin 的第一个
                    code = data.OriginCountries[0];
                }
            }
        }
        // 2. 如果 Origin 为空，尝试 ProductionCountries + Language 兜底
        else if (data.ProductionCountries != null && data.ProductionCountries.Count > 0 && !string.IsNullOrEmpty(lang))
        {
            if (LanguageToCountryMap.TryGetValue(lang, out var validCountries))
            {
                code = data.ProductionCountries.FirstOrDefault(c => validCountries.Contains(c));
            }
        }
        // === 逻辑升级 End ===

        // 如果找到了具体的国家代码
        if (!string.IsNullOrEmpty(code))
        {
            return FormatCountryTag(code, config.CountryStyle);
        }

        // 3. 最终兜底 (仅语言)
        return GetDefaultCountryByLanguage(lang);
    }

    // === 新增：格式化辅助方法 ===
    private static string FormatCountryTag(string code, CountryTagStyle style)
    {
        // 先获取中文名，如果映射表里没有，就用代码本身
        string name = CountryCodeToName.TryGetValue(code, out var n) ? n : code;

        return style switch
        {
            CountryTagStyle.NameOnly => name, // "香港"
            CountryTagStyle.CodeOnly => code, // "HK"
            CountryTagStyle.NameAndCode => $"{name} ({code})", // "香港 (HK)"
            _ => name
        };
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
