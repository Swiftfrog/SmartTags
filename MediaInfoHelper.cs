using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using System.Collections.Generic;
using System.Linq;
using System;

namespace SmartTags;

public static class MediaInfoHelper
{
    /// <summary>
    /// 分析媒体项目的流信息，返回需要添加的标签列表
    /// </summary>
    public static List<string> GetMediaInfoTags(BaseItem item, SmartTagsConfig config)
    {
        var tags = new List<string>();
        
        // 获取所有流信息
        var streams = item.GetMediaStreams();
        var videoStream = streams.FirstOrDefault(s => s.Type == MediaStreamType.Video);
        var audioStreams = streams.Where(s => s.Type == MediaStreamType.Audio).ToList();

        // 1. 分辨率标签
        if (config.EnableResolutionTags && videoStream != null && videoStream.Width.HasValue)
        {
            int width = videoStream.Width.Value;
            if (width >= 3800) tags.Add("4K");
            else if (width >= 1900) tags.Add("1080p");
            else if (width >= 1200) tags.Add("720p");
        }

        // 2. HDR / 杜比视界标签
        if (config.EnableHdrTags && videoStream != null)
        {
            bool isDv = false;
            
            // 修正点：VideoRange 是 string 类型，直接获取字符串。
            // 移除不存在的 VideoRangeType。
            string range = videoStream.VideoRange ?? "";
            string title = videoStream.DisplayTitle ?? "";

            // 检查 Dolby Vision
            // 通过字符串匹配 "Dolby" 或 "DOVI"
            if (range.IndexOf("Dolby", StringComparison.OrdinalIgnoreCase) >= 0 || 
                range.IndexOf("DOVI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                title.IndexOf("Dolby Vision", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                tags.Add("Dolby Vision");
                isDv = true;
            }

            // 检查 HDR
            // 修正点：不使用 VideoRange.HDR 枚举，而是检查字符串是否包含 HDR 关键词
            // 常见的 HDR 字符串值可能是 "HDR", "HDR10", "SMPTE ST 2086", "PQ", "HLG" 等
            if (!isDv) 
            {
                if (range.IndexOf("HDR", StringComparison.OrdinalIgnoreCase) >= 0 || 
                    range.IndexOf("PQ", StringComparison.OrdinalIgnoreCase) >= 0 || 
                    range.IndexOf("HLG", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    tags.Add("HDR");
                }
            }
        }

        // 3. 音频标签 (次世代音轨)
        if (config.EnableAudioTags && audioStreams.Any())
        {
            foreach (var audio in audioStreams)
            {
                string profile = audio.Profile?.ToLower() ?? "";
                string codec = audio.Codec?.ToLower() ?? "";
                string title = audio.DisplayTitle?.ToLower() ?? "";

                // Atmos (全景声)
                if (title.Contains("atmos") || profile.Contains("atmos"))
                {
                    if (!tags.Contains("Atmos")) tags.Add("Atmos");
                }

                // DTS:X
                if (title.Contains("dts:x") || title.Contains("dts-x") || profile.Contains("dts-x"))
                {
                    if (!tags.Contains("DTS:X")) tags.Add("DTS:X");
                }
                
                // TrueHD
                if (codec == "truehd")
                {
                     if (!tags.Contains("TrueHD")) tags.Add("TrueHD");
                }

                // DTS-HD Master Audio
                if (profile.Contains("dts-hd ma") || title.Contains("dts-hd ma"))
                {
                     if (!tags.Contains("DTS-HD MA")) tags.Add("DTS-HD MA");
                }
            }
        }

        return tags;
    }
}