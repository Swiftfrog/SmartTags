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

    // V1.2 新增：制作公司
    [DataMember(Name = "production_companies")]
    public List<TmdbProductionCompany>? ProductionCompanies { get; set; }

    // V1.2 新增：电视网/平台 (TV专用)
    [DataMember(Name = "networks")]
    public List<TmdbProductionCompany>? Networks { get; set; }
}

[DataContract]
public class TmdbProductionCompany
{
    [DataMember(Name = "id")]
    public int Id { get; set; }

    [DataMember(Name = "name")]
    public string? Name { get; set; }
    
    [DataMember(Name = "iso_3166_1")]
    public string? IsoCode { get; set; }
}