// TmdbDTOs.cs
using System.Collections.Generic;
using System.Runtime.Serialization; // 需要引用 System.Runtime.Serialization

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

    // 关键字段：TMDB 新版原产地字段
    [DataMember(Name = "origin_country")]
    public List<string>? OriginCountry { get; set; }

    // 备用字段：制作国家详情
    [DataMember(Name = "production_countries")]
    public List<TmdbProductionCountry>? ProductionCountries { get; set; }
}

[DataContract]
public class TmdbProductionCountry
{
    [DataMember(Name = "iso_3166_1")]
    public string? IsoCode { get; set; }

    [DataMember(Name = "name")]
    public string? Name { get; set; }
}
