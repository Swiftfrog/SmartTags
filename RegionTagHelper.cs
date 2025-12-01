using System.Collections.Generic;
using System.Linq;
using System;

namespace SmartTags;

public static class RegionTagHelper
{
    // 语言代码 -> 主要使用该语言的国家 (用于兜底)
    private static readonly Dictionary<string, HashSet<string>> LanguageToCountryMap = new()
    {
        // === 中文系 ===
        { "zh", new HashSet<string> { "CN", "HK", "TW", "SG", "MO" } },
        { "cn", new HashSet<string> { "CN", "HK", "TW", "SG", "MO" } },
        { "bo", new HashSet<string> { "CN" } },
        // === 英语系 ===
        { "en", new HashSet<string> { "US", "GB", "CA", "AU", "NZ", "IE", "ZA", "PH", "SG" } },
        // === 日韩 ===
        { "ja", new HashSet<string> { "JP" } },
        { "ko", new HashSet<string> { "KR", "KP" } },
        // === 欧洲 ===
        { "fr", new HashSet<string> { "FR", "BE", "CA", "CH", "MA" } },
        { "de", new HashSet<string> { "DE", "AT", "CH", "DD" } },
        { "it", new HashSet<string> { "IT", "CH" } },
        { "es", new HashSet<string> { "ES", "MX", "AR", "CL", "CO", "PE", "CU", "VE" } },
        { "pt", new HashSet<string> { "PT", "BR" } },
        { "ru", new HashSet<string> { "RU", "SU", "UA", "KZ", "BY" } },
        { "nl", new HashSet<string> { "NL", "BE" } },
        // === 北欧 ===
        { "sv", new HashSet<string> { "SE" } },
        { "no", new HashSet<string> { "NO" } },
        { "da", new HashSet<string> { "DK" } },
        { "fi", new HashSet<string> { "FI" } },
        { "is", new HashSet<string> { "IS" } },
        // === 东欧/中欧 ===
        { "pl", new HashSet<string> { "PL" } },
        { "cs", new HashSet<string> { "CZ", "CS", "XC" } },
        { "hu", new HashSet<string> { "HU" } },
        { "ro", new HashSet<string> { "RO" } },
        { "bg", new HashSet<string> { "BG" } },
        { "sr", new HashSet<string> { "RS", "YU", "HR", "BA" } },
        { "hr", new HashSet<string> { "HR", "YU" } },
        { "uk", new HashSet<string> { "UA", "SU" } },
        // === 亚洲其他 ===
        { "hi", new HashSet<string> { "IN" } },
        { "ta", new HashSet<string> { "IN" } },
        { "th", new HashSet<string> { "TH" } },
        { "vi", new HashSet<string> { "VN" } },
        { "id", new HashSet<string> { "ID" } },
        { "ms", new HashSet<string> { "MY", "SG" } },
        { "tl", new HashSet<string> { "PH" } },
        { "fa", new HashSet<string> { "IR" } },
        { "he", new HashSet<string> { "IL" } },
        { "tr", new HashSet<string> { "TR" } },
        // === 阿拉伯 ===
        { "ar", new HashSet<string> { "EG", "SA", "AE", "QA", "MA", "IQ", "LB" } }
    };

    // 国家代码 -> 中文名映射
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

    public static string? GetRegionTag(TmdbCacheData data, SmartTagsConfig config)
    {
        if (data == null) return null;

        string lang = data.OriginalLanguage?.ToLower() ?? "";
        string? code = null;

        // === 逻辑层级 1: 优先分析 OriginCountries ===
        if (data.OriginCountries != null && data.OriginCountries.Count > 0)
        {
            // Case A: 单一产地 (Fast Path - 90%的情况)
            // 只要只有一个，直接信任，无需仲裁
            if (data.OriginCountries.Count == 1)
            {
                code = data.OriginCountries[0];
            }
            // Case B: 多个产地 (需要仲裁)
            else
            {
                bool isChineseLang = lang == "zh" || lang == "cn" || lang == "bo";

                // B1: 华语片 - 语言锚定策略
                if (isChineseLang)
                {
                    // 粤语 (cn) -> 锚定 HK
                    if (lang == "cn")
                    {
                        code = data.OriginCountries.FirstOrDefault(c => c.Equals("HK", StringComparison.OrdinalIgnoreCase));
                    }
                    // 国语 (zh) -> 锚定 TW 或 CN
                    else if (lang == "zh")
                    {
                        code = data.OriginCountries.FirstOrDefault(c => 
                            c.Equals("TW", StringComparison.OrdinalIgnoreCase) || 
                            c.Equals("CN", StringComparison.OrdinalIgnoreCase));
                    }
                }
                
                // B2: 非华语片 (或 B1 锚定失败) - 资金方仲裁策略
                if (string.IsNullOrEmpty(code))
                {
                    var primaryProducer = data.ProductionCountries?.FirstOrDefault();
                    
                    if (!string.IsNullOrEmpty(primaryProducer) && data.OriginCountries.Contains(primaryProducer))
                    {
                        code = primaryProducer;
                    }
                }

                // B3: 所有仲裁都失败，回退到 Origin 的第一个
                if (string.IsNullOrEmpty(code))
                {
                    code = data.OriginCountries[0];
                }
            }
        }
        
        // === 逻辑层级 2: 数据缺失兜底 ===
        // 如果 Origin 为空，尝试用 ProductionCountries + Language 推断
        else if (data.ProductionCountries != null && data.ProductionCountries.Count > 0 && !string.IsNullOrEmpty(lang))
        {
            if (LanguageToCountryMap.TryGetValue(lang, out var validCountries))
            {
                code = data.ProductionCountries.FirstOrDefault(c => validCountries.Contains(c));
            }
        }

        // === 格式化输出 ===
        if (!string.IsNullOrEmpty(code))
        {
            return FormatCountryTag(code, config.CountryStyle);
        }

        // === 逻辑层级 3: 最终兜底 (仅语言) ===
        return GetDefaultCountryByLanguage(lang);
    }

    private static string FormatCountryTag(string code, CountryTagStyle style)
    {
        string name = CountryCodeToName.TryGetValue(code, out var n) ? n : code;
        return style switch
        {
            CountryTagStyle.NameOnly => name,
            CountryTagStyle.CodeOnly => code,
            CountryTagStyle.NameAndCode => $"{name} ({code})",
            _ => name
        };
    }

    private static string? GetDefaultCountryByLanguage(string lang)
    {
        return lang switch
        {
            "ja" => "日本",
            "ko" => "韩国",
            "zh" or "cn" => "华语",
            "en" => "英语",
            "fr" => "法国",
            "de" => "德国",
            "ru" => "俄罗斯",
            "es" => "西班牙",
            "pt" => "葡萄牙",
            "it" => "意大利",
            "th" => "泰国",
            "vi" => "越南",
            "hi" => "印度",
            "pl" => "波兰",
            _ => null
        };
    }
}