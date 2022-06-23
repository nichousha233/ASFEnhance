﻿#pragma warning disable CS8632 // 只能在 "#nullable" 注释上下文内的代码中使用可为 null 的引用类型的注释。

using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Localization;
using ArchiSteamFarm.Steam;
using ASFEnhance.Data;
using ASFEnhance.Localization;
using static ASFEnhance.Utils;

namespace ASFEnhance.Explorer
{
    internal static class Command
    {
        /// <summary>
        /// 浏览探索队列
        /// </summary>
        /// <param name="bot"></param>
        /// <param name="gruopID"></param>
        /// <returns></returns>
        internal static async Task<string?> ResponseExploreDiscoveryQueue(Bot bot)
        {
            if (!bot.IsConnectedAndLoggedOn)
            {
                return bot.FormatBotResponse(Strings.BotNotConnected);
            }

            var steamSaleEvent = Type.GetType("ArchiSteamFarm.Steam.Integration.SteamSaleEvent,ArchiSteamFarm");

            var steamSaleEventCls = bot.GetPrivateField("SteamSaleEvent", steamSaleEvent);
            var saleEventTimer = steamSaleEventCls.GetPrivateField<Timer>("SaleEventTimer");

            saleEventTimer.Change(TimeSpan.FromSeconds(5), TimeSpan.FromHours(8.1));

            return bot.FormatBotResponse(Langs.ExplorerStart);
        }

        /// <summary>
        /// 浏览探索队列 (多个Bot)
        /// </summary>
        /// <param name="botNames"></param>
        /// <param name="gruopID"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        internal static async Task<string?> ResponseExploreDiscoveryQueue(string botNames)
        {
            if (string.IsNullOrEmpty(botNames))
            {
                throw new ArgumentNullException(nameof(botNames));
            }

            HashSet<Bot>? bots = Bot.GetBots(botNames);

            if ((bots == null) || (bots.Count == 0))
            {
                return FormatStaticResponse(string.Format(Strings.BotNotFound, botNames));
            }

            IList<string?> results = await Utilities.InParallel(bots.Select(bot => ResponseExploreDiscoveryQueue(bot))).ConfigureAwait(false);

            List<string> responses = new(results.Where(result => !string.IsNullOrEmpty(result))!);

            return responses.Count > 0 ? string.Join(Environment.NewLine, responses) : null;
        }
    }
}
