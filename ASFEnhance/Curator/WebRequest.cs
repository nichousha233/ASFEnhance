using ArchiSteamFarm.Steam;
using ASFEnhance.Data;
using SteamKit2;


namespace ASFEnhance.Curator;


internal static class WebRequest
{
    /// <summary>
    /// 关注或者取关鉴赏家
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="clanId"></param>
    /// <param name="isFollow"></param>
    /// <returns></returns>
    internal static async Task<bool> FollowCurator(Bot bot, ulong clanId, bool isFollow)
    {
        var request = new Uri(SteamStoreURL, "/curators/ajaxfollow");
        var referer = new Uri(SteamStoreURL, $"curator/{clanId}");

        var data = new Dictionary<string, string>(3) {
            { "clanid", clanId.ToString() },
            { "follow", isFollow ? "1" : "0" },
        };

        var response = await bot.ArchiWebHandler.UrlPostToJsonObjectWithSession<AJaxFollowResponse>(request, data: data, referer: referer).ConfigureAwait(false);

        return response?.Content?.Success.Result == EResult.OK;
    }

    /// <summary>
    /// 获取关注的鉴赏家列表
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="start"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    internal static async Task<HashSet<CuratorItem>?> GetFollowingCurators(Bot bot, uint start, uint count)
    {
        var request = new Uri(SteamStoreURL, $"/curators/ajaxgetcurators//?query=&start={start}&count={count}&dynamic_data=&filter=mycurators&appid=0");
        var referer = new Uri(SteamStoreURL, "/curators/mycurators/");

        var response = await bot.ArchiWebHandler!.UrlGetToJsonObjectWithSession<AjaxGetCuratorsResponse>(request, referer: referer).ConfigureAwait(false);

        return HtmlParser.ParseCuratorListPage(response?.Content);
    }
}
