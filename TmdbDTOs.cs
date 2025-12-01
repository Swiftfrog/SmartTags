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
    
    [DataMember(Name = "production_countries")]
    public List<TmdbCountry>? ProductionCountryList { get; set; } 

    [DataMember(Name = "production_companies")]
    public List<TmdbProductionCompany>? ProductionCompanies { get; set; }

    [DataMember(Name = "networks")]
    public List<TmdbProductionCompany>? Networks { get; set; }
}

[DataContract]
public class TmdbCountry
{
    [DataMember(Name = "iso_3166_1")]
    public string? IsoCode { get; set; }

    [DataMember(Name = "name")]
    public string? Name { get; set; }
}

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