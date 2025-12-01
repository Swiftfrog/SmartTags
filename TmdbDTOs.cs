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
    // 修正属性名，避免冲突
    [DataMember(Name = "production_countries")]
    public List<TmdbCountry>? ProductionCountryList { get; set; } 

    // 2. 制作公司 (V1.2 新增, 对应 JSON: "production_companies")
    [DataMember(Name = "production_companies")]
    public List<TmdbProductionCompany>? ProductionCompanies { get; set; }

    // 3. 电视网/平台 (V1.2 新增, 对应 JSON: "networks")
    [DataMember(Name = "networks")]
    public List<TmdbProductionCompany>? Networks { get; set; }
}

// 新增：专门用于 "production_countries" 的 DTO
[DataContract]
public class TmdbCountry
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
    
    // 公司的国家代码字段通常是 "origin_country"
    [DataMember(Name = "origin_country")]
    public string? OriginCountry { get; set; }
}