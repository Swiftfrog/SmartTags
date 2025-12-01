using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SmartTags;

[DataContract]
public class TmdbItemDetails
{
    [DataMember(Name = "id")]
    public int Id { get; set; }

    [DataMember(Name = "imdb_id")]
    public string? ImdbId { get; set; }

    [DataMember(Name = "original_language")]
    public string? OriginalLanguage { get; set; }

    [DataMember(Name = "origin_country")]
    public List<string>? OriginCountry { get; set; }
    
    // 1. 制作国家详情 (对应 JSON: "production_countries")
    // 使用 TmdbProductionCountry 类型
    [DataMember(Name = "production_countries")]
    public List<TmdbProductionCountry>? ProductionCountries { get; set; }

    // 2. 制作公司 (V1.2 新增, 对应 JSON: "production_companies")
    // 修正点：属性名改为 ProductionCompanies，类型是 TmdbProductionCompany
    [DataMember(Name = "production_companies")]
    public List<TmdbProductionCompany>? ProductionCompanies { get; set; }

    // 3. 电视网/平台 (V1.2 新增, 对应 JSON: "networks")
    // 类型也是 TmdbProductionCompany (结构相同)
    [DataMember(Name = "networks")]
    public List<TmdbProductionCompany>? Networks { get; set; }
}

// 专门用于 "production_countries" 数组的对象
[DataContract]
public class TmdbProductionCountry
{
    [DataMember(Name = "iso_3166_1")]
    public string? IsoCode { get; set; }

    [DataMember(Name = "name")]
    public string? Name { get; set; }
}

// 专门用于 "production_companies" 和 "networks" 数组的对象
[DataContract]
public class TmdbProductionCompany
{
    [DataMember(Name = "id")]
    public int Id { get; set; }

    [DataMember(Name = "name")]
    public string? Name { get; set; }
    
    // 修正点：公司的国家代码字段通常是 "origin_country"，不是 "iso_3166_1"
    // 虽然我们在 V1.2 主要用 ID，但写对映射总是好的
    [DataMember(Name = "origin_country")]
    public string? OriginCountry { get; set; }
}