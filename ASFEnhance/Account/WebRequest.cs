using ArchiSteamFarm.Core;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Steam.Data;
using ArchiSteamFarm.Web.Responses;
using ASFEnhance.Data;
using Newtonsoft.Json;
using System.Net;
using System.Text;
using static ASFEnhance.Account.CurrencyHelper;

namespace ASFEnhance.Account;

internal static class WebRequest
{
    /// <summary>
    /// 加载账户历史记录
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="cursorData"></param>
    /// <returns></returns>
    private static async Task<AccountHistoryResponse?> AjaxLoadMoreHistory(Bot bot, AccountHistoryResponse.CursorData cursorData)
    {
        var request = new Uri(SteamStoreURL, "/account/AjaxLoadMoreHistory/?l=schinese");

        var data = new Dictionary<string, string>(5, StringComparer.Ordinal) {
            { "cursor[wallet_txnid]", cursorData.WalletTxnid },
            { "cursor[timestamp_newest]", cursorData.TimestampNewest.ToString() },
            { "cursor[balance]", cursorData.Balance },
            { "cursor[currency]", cursorData.Currency.ToString() },
        };

        var response = await bot.ArchiWebHandler!.UrlPostToJsonObjectWithSession<AccountHistoryResponse>(request, referer: SteamStoreURL, data: data).ConfigureAwait(false);

        return response?.Content;
    }

    /// <summary>
    /// 获取在线汇率
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="currency"></param>
    /// <returns></returns>
    private static async Task<ExchangeAPIResponse?> GetExchangeRatio(string currency)
    {
        var request = new Uri($"https://api.exchangerate-api.com/v4/latest/{currency}");
        var response = await ASF.WebBrowser!.UrlGetToJsonObject<ExchangeAPIResponse>(request).ConfigureAwait(false);
        return response?.Content;
    }

    /// <summary>
    /// 获取更多历史记录
    /// </summary>
    /// <param name="bot"></param>
    /// <returns></returns>
    private static async Task<HtmlDocumentResponse?> GetAccountHistoryAjax(Bot bot)
    {
        var request = new Uri(SteamStoreURL, "/account/history?l=schinese");
        var response = await bot.ArchiWebHandler.UrlGetToHtmlDocumentWithSession(request, referer: SteamStoreURL).ConfigureAwait(false);
        return response;
    }

    /// <summary>
    /// 获取账号消费历史记录
    /// </summary>
    /// <param name="bot"></param>
    /// <returns></returns>
    internal static async Task<string> GetAccountHistoryDetail(Bot bot)
    {
        // 读取在线汇率
        string myCurrency = bot.WalletCurrency.ToString();
        ExchangeAPIResponse? exchangeRate = await GetExchangeRatio(myCurrency).ConfigureAwait(false);
        if (exchangeRate == null)
        {
            return bot.FormatBotResponse(Langs.GetExchangeRateFailed);
        }

        // 获取货币符号
        if (!Currency2Symbol.TryGetValue(myCurrency, out var symbol))
        {
            symbol = myCurrency;
        }

        var result = new StringBuilder();
        result.AppendLine(bot.FormatBotResponse(Langs.MultipleLineResult));

        int giftedSpend = 0;
        int totalSpend = 0;
        int totalExternalSpend = 0;

        // 读取账户消费历史
        result.AppendLine(Langs.PurchaseHistorySummary);
        var accountHistory = await GetAccountHistoryAjax(bot).ConfigureAwait(false);
        if (accountHistory == null)
        {
            return Langs.NetworkError;
        }

        // 解析表格元素
        var tbodyElement = accountHistory?.Content?.QuerySelector("table>tbody");
        if (tbodyElement == null)
        {
            return Langs.ParseHtmlFailed;
        }

        // 获取下一页指针(为null代表没有下一页)
        var cursor = HtmlParser.ParseCursorData(accountHistory);

        var historyData = HtmlParser.ParseHistory(tbodyElement, exchangeRate.Rates, myCurrency);

        while (cursor != null)
        {
            AccountHistoryResponse? ajaxHistoryResponse = await AjaxLoadMoreHistory(bot, cursor).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(ajaxHistoryResponse?.HtmlContent))
            {
                tbodyElement.InnerHtml = ajaxHistoryResponse.HtmlContent;
                cursor = ajaxHistoryResponse.Cursor;
                historyData += HtmlParser.ParseHistory(tbodyElement, exchangeRate.Rates, myCurrency);
            }
            else
            {
                cursor = null;
            }
        }

        giftedSpend = historyData.GiftPurchase;
        totalSpend = historyData.StorePurchase + historyData.InGamePurchase;
        totalExternalSpend = historyData.StorePurchase - historyData.StorePurchaseWallet + historyData.GiftPurchase - historyData.GiftPurchaseWallet;

        result.AppendLine(Langs.PurchaseHistoryGroupType);
        result.AppendLineFormat(Langs.PurchaseHistoryTypeStorePurchase, historyData.StorePurchase / 100.0, symbol);
        result.AppendLineFormat(Langs.PurchaseHistoryTypeExternal, (historyData.StorePurchase - historyData.StorePurchaseWallet) / 100.0, symbol);
        result.AppendLineFormat(Langs.PurchaseHistoryTypeWallet, historyData.StorePurchaseWallet / 100.0, symbol);
        result.AppendLineFormat(Langs.PurchaseHistoryTypeGiftPurchase, historyData.GiftPurchase / 100.0, symbol);
        result.AppendLineFormat(Langs.PurchaseHistoryTypeExternal, (historyData.GiftPurchase - historyData.GiftPurchaseWallet) / 100.0, symbol);
        result.AppendLineFormat(Langs.PurchaseHistoryTypeWallet, historyData.GiftPurchaseWallet / 100.0, symbol);
        result.AppendLineFormat(Langs.PurchaseHistoryTypeInGamePurchase, historyData.InGamePurchase / 100.0, symbol);
        result.AppendLineFormat(Langs.PurchaseHistoryTypeMarketPurchase, historyData.MarketPurchase / 100.0, symbol);
        result.AppendLineFormat(Langs.PurchaseHistoryTypeMarketSelling, historyData.MarketSelling / 100.0, symbol);

        result.AppendLine(Langs.PurchaseHistoryGroupOther);
        result.AppendLineFormat(Langs.PurchaseHistoryTypeWalletPurchase, historyData.WalletPurchase / 100.0, symbol);
        result.AppendLineFormat(Langs.PurchaseHistoryTypeOther, historyData.Other / 100.0, symbol);
        result.AppendLineFormat(Langs.PurchaseHistoryTypeRefunded, historyData.RefundPurchase / 100.0, symbol);
        result.AppendLineFormat(Langs.PurchaseHistoryTypeExternal, (historyData.RefundPurchase - historyData.RefundPurchaseWallet) / 100.0, symbol);
        result.AppendLineFormat(Langs.PurchaseHistoryTypeWallet, historyData.RefundPurchaseWallet / 100.0, symbol);

        result.AppendLine(Langs.PurchaseHistoryGroupStatus);
        result.AppendLineFormat(Langs.PurchaseHistoryStatusTotalPurchase, totalSpend / 100.0, symbol);
        result.AppendLineFormat(Langs.PurchaseHistoryStatusTotalExternalPurchase, totalExternalSpend / 100.0, symbol);
        result.AppendLineFormat(Langs.PurchaseHistoryStatusTotalGift, giftedSpend / 100.0, symbol);
        result.AppendLineFormat(Langs.PurchaseHistoryGroupGiftCredit);
        result.AppendLineFormat(Langs.PurchaseHistoryCreditMin, (totalSpend - giftedSpend) / 100, symbol);
        result.AppendLineFormat(Langs.PurchaseHistoryCreditMax, (totalSpend * 1.8 - giftedSpend) / 100, symbol);
        result.AppendLineFormat(Langs.PurchaseHistoryExternalMin, (totalExternalSpend - giftedSpend) / 100, symbol);
        result.AppendLineFormat(Langs.PurchaseHistoryExternalMax, (totalExternalSpend * 1.8 - giftedSpend) / 100, symbol);

        var updateTime = DateTimeOffset.FromUnixTimeSeconds(exchangeRate.UpdateTime).UtcDateTime;

        result.AppendLine(Langs.PurchaseHistoryGroupAbout);
        result.AppendLineFormat(Langs.PurchaseHistoryAboutBaseRate, exchangeRate.Base);
        result.AppendLineFormat(Langs.PurchaseHistoryAboutPlugin, nameof(ASFEnhance));
        result.AppendLineFormat(Langs.PurchaseHistoryAboutUpdateTime, updateTime);
        result.AppendLineFormat(Langs.PurchaseHistoryAboutRateSource);

        return result.ToString();
    }

    /// <summary>
    /// 获取许可证信息
    /// </summary>
    /// <param name="bot"></param>
    /// <returns></returns>
    internal static async Task<List<LicensesData>?> GetOwnedLicenses(Bot bot)
    {
        var request = new Uri(SteamStoreURL, "/account/licenses/?l=schinese");
        var response = await bot.ArchiWebHandler.UrlGetToHtmlDocumentWithSession(request, referer: SteamStoreURL).ConfigureAwait(false);
        return HtmlParser.ParseLincensesPage(response);
    }

    /// <summary>
    /// 移除许可证
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="subId"></param>
    /// <returns></returns>
    internal static async Task<bool> RemoveLicense(Bot bot, uint subId)
    {
        var request = new Uri(SteamStoreURL, "/account/removelicense");
        var referer = new Uri(SteamStoreURL, "/account/licenses/");

        var data = new Dictionary<string, string>(2) {
            { "packageid", subId.ToString() },
        };

        var response = await bot.ArchiWebHandler.UrlPostToHtmlDocumentWithSession(request, data: data, referer: referer).ConfigureAwait(false);
        return response?.StatusCode == HttpStatusCode.OK;
    }

    /// <summary>
    /// 获取邮箱通知偏好
    /// </summary>
    /// <param name="bot"></param>
    /// <returns></returns>
    internal static async Task<EmailOptions?> GetAccountEmailOptions(Bot bot)
    {
        var request = new Uri(SteamStoreURL, "/account/emailoptout");
        var response = await bot.ArchiWebHandler.UrlGetToHtmlDocumentWithSession(request, referer: SteamStoreURL).ConfigureAwait(false);
        return HtmlParser.ParseEmailOptionPage(response);
    }

    /// <summary>
    /// 设置邮箱通知偏好
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="option"></param>
    /// <returns></returns>
    internal static async Task<EmailOptions?> SetAccountEmailOptions(Bot bot, EmailOptions option)
    {
        var request = new Uri(SteamStoreURL, "/account/emailoptout");

        var data = new Dictionary<string, string>(11) {
            { "action", "save" },
            { "opt_out_all",option.EnableEmailNotification ? "0" : "1" },
        };

        if (option.EnableEmailNotification)
        {
            if (option.WhenWishlistDiscount)
            {
                data.Add("opt_out_wishlist_inverse", "on");
            }
            if (option.WhenWishlistRelease)
            {
                data.Add("opt_out_wishlist_releases_inverse", "on");
            }
            if (option.WhenGreenLightRelease)
            {
                data.Add("opt_out_greenlight_releases_inverse", "on");
            }
            if (option.WhenFollowPublisherRelease)
            {
                data.Add("opt_out_creator_home_releases_inverse", "on");
            }
            if (option.WhenSaleEvent)
            {
                data.Add("opt_out_seasonal_inverse", "on");
            }
            if (option.WhenReceiveCuratorReview)
            {
                data.Add("opt_out_curator_connect_inverse", "on");
            }
            if (option.WhenReceiveCommunityReward)
            {
                data.Add("opt_out_loyalty_awards_inverse", "on");
            }
            if (option.WhenGameEventNotification)
            {
                data.Add("opt_out_in_library_events_inverse", "on");
            }
        }

        var response = await bot.ArchiWebHandler.UrlPostToHtmlDocumentWithSession(request, referer: SteamStoreURL, data: data).ConfigureAwait(false);
        return HtmlParser.ParseEmailOptionPage(response);
    }


    /// <summary>
    /// 获取通知偏好
    /// </summary>
    /// <param name="bot"></param>
    /// <returns></returns>
    internal static async Task<NotificationOptions?> GetAccountNotificationOptions(Bot bot)
    {
        var request = new Uri(SteamStoreURL, "/account/notificationsettings");
        var response = await bot.ArchiWebHandler.UrlGetToHtmlDocumentWithSession(request, referer: SteamStoreURL).ConfigureAwait(false);
        return HtmlParser.ParseNotificationOptionPage(response);
    }

    /// <summary>
    /// 设置通知偏好
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="option"></param>
    /// <returns></returns>
    internal static async Task<ResultResponse?> SetAccountNotificationOptions(Bot bot, NotificationOptions option)
    {
        var request = new Uri(SteamStoreURL, "/account/ajaxsetnotificationsettings");

        var optionList = new List<NotificationPayload>
        {
            new(NotificationType.ReceivedGift, option.ReceivedGift),
            new(NotificationType.SubscribedDissionReplyed,option.SubscribedDissionReplyed),
            new(NotificationType.ReceivedNewItem,option.ReceivedNewItem),
            new(NotificationType.MajorSaleStart,option.MajorSaleStart),
            new(NotificationType.ItemInWishlistOnSale,option.ItemInWishlistOnSale),
            new(NotificationType.ReceivedTradeOffer,option.ReceivedTradeOffer),
            new(NotificationType.ReceivedSteamSupportReply,option.ReceivedSteamSupportReply),
            new(NotificationType.SteamTurnNotification,option.SteamTurnNotification),
        };

        var json = JsonConvert.SerializeObject(optionList);

        var data = new Dictionary<string, string>(11) {
            { "notificationpreferences", json },
        };

        var response = await bot.ArchiWebHandler.UrlPostToJsonObjectWithSession<ResultResponse>(request, referer: SteamStoreURL, data: data).ConfigureAwait(false);
        return response?.Content;
    }

    /// <summary>
    /// 获取用户封禁状态
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="token"></param>
    /// <param name="steamids"></param>
    /// <returns></returns>
    internal static async Task<GetPlayerBansResponse?> GetPlayerBans(Bot bot, string token, ulong steamids)
    {
        var request = new Uri(SteamApiURL, $"/ISteamUser/GetPlayerBans/v1/?key={token}&steamids={steamids}");
        var response = await bot.ArchiWebHandler.UrlGetToJsonObjectWithSession<GetPlayerBansResponse>(request, referer: SteamStoreURL).ConfigureAwait(false);
        return response?.Content;
    }

    internal static async Task<string?> GetAccountBans(Bot bot)
    {
        var request = new Uri(SteamCommunityURL, $"/profiles/{bot.SteamID}/currentbans");
        var response = await bot.ArchiWebHandler.UrlGetToHtmlDocumentWithSession(request, referer: SteamStoreURL).ConfigureAwait(false);

        return HtmlParser.ParseAccountBans(response?.Content);
    }

    /// <summary>
    /// 获取礼物Id
    /// </summary>
    /// <param name="bot"></param>
    /// <returns></returns>
    internal static async Task<HashSet<ulong>?> GetReceivedGift(Bot bot)
    {
        var request = new Uri(SteamStoreURL, "/gifts");

        var response = await bot.ArchiWebHandler.UrlGetToHtmlDocumentWithSession(request).ConfigureAwait(false);

        return HtmlParser.ParseGiftPage(response);
    }

    /// <summary>
    /// 接收礼物
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="giftId"></param>
    /// <returns></returns>
    internal static async Task<UnpackGiftResponse?> AcceptReceivedGift(Bot bot, ulong giftId)
    {
        var request = new Uri(SteamStoreURL, $"/gifts/{giftId}/unpack");

        var response = await bot.ArchiWebHandler.UrlPostToJsonObjectWithSession<UnpackGiftResponse>(request, null, null).ConfigureAwait(false);

        return response?.Content;
    }

    /// <summary>
    /// 获取游戏游玩时间
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    internal static async Task<Dictionary<uint, GetOwnedGamesResponse.GameData>?> GetGamePlayTime(Bot bot, string apiKey)
    {
        var request = new Uri(SteamApiURL, $"/IPlayerService/GetOwnedGames/v1/?key={apiKey}&steamid={bot.SteamID}&include_appinfo=true&include_played_free_games=true&include_free_sub=true&skip_unvetted_apps=true&language={Langs.Language}&include_extended_appinfo=true");
        var response = await bot.ArchiWebHandler.UrlGetToJsonObjectWithSession<GetOwnedGamesResponse>(request, referer: SteamStoreURL).ConfigureAwait(false);

        if (response?.Content?.Response?.Games != null)
        {
            var result = new Dictionary<uint, GetOwnedGamesResponse.GameData>();

            foreach (var game in response.Content.Response.Games)
            {
                result.TryAdd(game.AppId, game);
            }

            return result;
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// 获取账号邮箱
    /// </summary>
    /// <param name="bot"></param>
    /// <returns></returns>
    internal static async Task<string?> GetAccountEmail(Bot bot)
    {
        var request = new Uri(SteamStoreURL, "/account");
        var response = await bot.ArchiWebHandler.UrlGetToHtmlDocumentWithSession(request, referer: SteamStoreURL).ConfigureAwait(false);
        return HtmlParser.ParseAccountEmail(response?.Content);
    }
}
