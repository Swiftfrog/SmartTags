using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities; // 包含 MediaStream, VideoRange 等定义
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
        
        // 获取所有流信息 (Emby 已经刮削好的)
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
            // SD 画质一般不打标签，避免污染
        }

        // 2. HDR / 杜比视界标签
        if (config.EnableHdrTags && videoStream != null)
        {
            // 检查 Dolby Vision
            // Emby 的 VideoRange 字段可能包含 "DolbyVision" 或者 VideoRangeType 字段
            // 这里采用宽松匹配，覆盖多数情况
            bool isDv = false;
            
            // 检查 VideoRangeType (新版 Emby) 或 VideoRange (旧版/通用)
            // 注意：不同版本的 Emby 字段可能略有不同，字符串匹配最稳妥
            string range = videoStream.VideoRange?.ToString() ?? "";
            string rangeType = videoStream.VideoRangeType?.ToString() ?? "";
            string title = videoStream.DisplayTitle ?? ""; // 有时文件名/标题里有

            if (range.Contains("Dolby", StringComparison.OrdinalIgnoreCase) || 
                rangeType.Contains("Dolby", StringComparison.OrdinalIgnoreCase) ||
                rangeType.Contains("DOVI", StringComparison.OrdinalIgnoreCase))
            {
                tags.Add("Dolby Vision");
                isDv = true;
            }

            // 检查 HDR (如果不是 DV，或者想同时标记)
            // 通常 DV 也算 HDR，但用户可能只想要一个最强的。
            // 策略：如果是 HDR 但不是 DV，标记 HDR。
            if (videoStream.VideoRange == VideoRange.HDR || 
                range.Contains("HDR", StringComparison.OrdinalIgnoreCase))
            {
                // 只有当没有标记 DV 时，才标记普通的 HDR (可选策略，或者共存)
                // 这里我们选择共存策略：如果是 DV，它通常也是 HDR。
                // 但为了标签简洁，如果有了 Dolby Vision，通常不需要再打 HDR 标签，除非是 HDR10+
                if (!isDv) 
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
                string layout = audio.ChannelLayout?.ToLower() ?? ""; // 5.1, 7.1

                // Atmos (全景声)
                // 通常在 Title 或 Profile 中出现
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
