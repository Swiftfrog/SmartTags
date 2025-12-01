using System.Collections.Generic;
using System.Linq;

namespace SmartTags;

public static class StudioMapper
{
    // 制作公司映射 (Production Companies)
    private static readonly Dictionary<string, HashSet<int>> CompanyMap = new()
    {
        // === 好莱坞五大 & 巨头 ===
        { "Disney", new HashSet<int> { 2, 6125, 58 } }, // Pictures, Animation, TV
        { "Warner Bros", new HashSet<int> { 174, 19551, 2778 } },
        { "Universal", new HashSet<int> { 33, 2672 } },
        { "Sony", new HashSet<int> { 5, 34, 3268, 559 } },
        { "Paramount", new HashSet<int> { 4, 1081 } },
        { "20th Century", new HashSet<int> { 25 } },
        
        // === 顶级厂牌 & 动画 ===
        { "Marvel", new HashSet<int> { 420 } },
        { "DC", new HashSet<int> { 128064 } },
        { "Pixar", new HashSet<int> { 3 } },
        { "Lucasfilm", new HashSet<int> { 1 } },
        { "DreamWorks", new HashSet<int> { 521 } },
        { "Illumination", new HashSet<int> { 6704 } },
        { "A24", new HashSet<int> { 41077 } },
        { "Lionsgate", new HashSet<int> { 1632 } },
        { "MGM", new HashSet<int> { 21 } },
        { "Legendary", new HashSet<int> { 923 } },
        
        // === 流媒体自制 ===
        { "Netflix", new HashSet<int> { 178464 } }, // Netflix Studios
        { "Amazon", new HashSet<int> { 20580 } },   // Amazon Studios
        { "Apple TV+", new HashSet<int> { 115233 } },

        // === 日本 ===
        { "Ghibli", new HashSet<int> { 10342 } },
        { "Toho", new HashSet<int> { 884 } },
        { "Toei", new HashSet<int> { 3341, 5822 } },
        { "Kyoto Animation", new HashSet<int> { 2969 } },
        { "Sunrise", new HashSet<int> { 306 } },
        { "Bones", new HashSet<int> { 477 } },
        { "MAPPA", new HashSet<int> { 12799 } },
        { "Ufotable", new HashSet<int> { 5904 } },
        { "CoMix Wave", new HashSet<int> { 25263 } },
        { "Madhouse", new HashSet<int> { 569 } },

        // === 韩国 ===
        { "CJ ENM", new HashSet<int> { 7036 } },
        { "Studio Dragon", new HashSet<int> { 95563 } },

        // === 香港 ===
        { "Shaw Brothers", new HashSet<int> { 5361, 906 } },
        { "Golden Harvest", new HashSet<int> { 2521 } },
        { "Milkyway", new HashSet<int> { 2469 } },
        { "Emperor", new HashSet<int> { 7338 } },
        { "Media Asia", new HashSet<int> { 4081 } }
    };

    // 电视网/平台映射 (Networks)
    private static readonly Dictionary<string, HashSet<int>> NetworkMap = new()
    {
        // === 全球流媒体 ===
        { "Netflix", new HashSet<int> { 213 } },
        { "HBO", new HashSet<int> { 49, 3186, 13252 } }, // HBO, HBO Max
        { "Amazon", new HashSet<int> { 1024 } },
        { "Apple TV+", new HashSet<int> { 2552 } },
        { "Disney+", new HashSet<int> { 2739 } },
        { "Hulu", new HashSet<int> { 453 } },
        { "Peacock", new HashSet<int> { 3353 } },
        { "Paramount+", new HashSet<int> { 4330 } },
        { "BBC", new HashSet<int> { 4, 332 } },

        // === 韩国 ===
        { "tvN", new HashSet<int> { 861 } },
        { "JTBC", new HashSet<int> { 885 } },
        { "SBS", new HashSet<int> { 156 } },
        { "KBS", new HashSet<int> { 342 } },
        { "MBC", new HashSet<int> { 97 } },

        // === 日本 ===
        { "NHK", new HashSet<int> { 185 } },
        { "Fuji TV", new HashSet<int> { 1 } },
        { "TBS", new HashSet<int> { 160 } },
        { "TV Asahi", new HashSet<int> { 103 } },

        // === 港台 ===
        { "TVB", new HashSet<int> { 56 } },
        { "PTS", new HashSet<int> { 326 } }, // 公视
        { "ViuTV", new HashSet<int> { 2661 } }
    };

    public static List<string> GetStudioTags(TmdbCacheData data)
    {
        var tags = new HashSet<string>();
        if (data == null) return tags.ToList();

        // 1. 检查制作公司
        if (data.ProductionCompanyIds != null)
        {
            foreach (var kvp in CompanyMap)
            {
                if (kvp.Value.Overlaps(data.ProductionCompanyIds))
                {
                    tags.Add(kvp.Key);
                }
            }
        }

        // 2. 检查电视网
        if (data.NetworkIds != null)
        {
            foreach (var kvp in NetworkMap)
            {
                if (kvp.Value.Overlaps(data.NetworkIds))
                {
                    tags.Add(kvp.Key);
                }
            }
        }

        return tags.ToList();
    }

    // 供 Cleanup 任务使用
    public static IEnumerable<string> GetAllKnownStudioTags()
    {
        return CompanyMap.Keys.Concat(NetworkMap.Keys).Distinct();
    }
}
