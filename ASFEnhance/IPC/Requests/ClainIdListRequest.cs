using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace ASFEnhance.IPC.Requests;

/// <summary>
/// 鉴赏家Id列表请求
/// </summary>
public sealed record ClanIdListRequest
{
    /// <summary>
    /// 鉴赏家ID列表
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    [Required]
    public HashSet<uint>? ClanIds { get; set; }
}
