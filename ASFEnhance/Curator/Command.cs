using ArchiSteamFarm.Core;
using ArchiSteamFarm.Localization;
using ArchiSteamFarm.Steam;
using System.Text;

namespace ASFEnhance.Curator;

internal static class Command
{
    /// <summary>
    /// 关注或者取关鉴赏家
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="targetClanIds"></param>
    /// <param name="isFollow"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    internal static async Task<string?> ResponseFollowCurator(Bot bot, string targetClanIds, bool isFollow)
    {
        if (!bot.IsConnectedAndLoggedOn)
        {
            return bot.FormatBotResponse(Strings.BotNotConnected);
        }

        if (string.IsNullOrEmpty(targetClanIds))
        {
            throw new ArgumentNullException(nameof(targetClanIds));
        }

        var response = new StringBuilder();

        var curators = targetClanIds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string curator in curators)
        {
            if (!ulong.TryParse(curator, out ulong clanId) || (clanId == 0))
            {
                response.AppendLine(bot.FormatBotResponse(Strings.ErrorIsInvalid, nameof(clanId)));
                continue;
            }

            bool result = await WebRequest.FollowCurator(bot, clanId, isFollow).ConfigureAwait(false);

            response.AppendLine(bot.FormatBotResponse(Strings.BotAddLicense, clanId, result ? Langs.Success : Langs.Failure));
        }

        return response.Length > 0 ? response.ToString() : null;
    }

    /// <summary>
    /// 关注或者取关鉴赏家 (多个Bot)
    /// </summary>
    /// <param name="botNames"></param>
    /// <param name="targetClanIds"></param>
    /// <param name="isFollow"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    internal static async Task<string?> ResponseFollowCurator(string botNames, string targetClanIds, bool isFollow)
    {
        if (string.IsNullOrEmpty(botNames))
        {
            throw new ArgumentNullException(nameof(botNames));
        }

        var bots = Bot.GetBots(botNames);

        if ((bots == null) || (bots.Count == 0))
        {
            return FormatStaticResponse(Strings.BotNotFound, botNames);
        }

        var results = await Utilities.InParallel(bots.Select(bot => ResponseFollowCurator(bot, targetClanIds, isFollow))).ConfigureAwait(false);

        var responses = new List<string?>(results.Where(result => !string.IsNullOrEmpty(result)));

        return responses.Count > 0 ? string.Join(Environment.NewLine, responses) : null;
    }

    private const int ASFenhanceCuratorClanId = 39487086;

    /// <summary>
    /// 获取鉴赏家列表
    /// </summary>
    /// <param name="bot"></param>
    /// <returns></returns>
    internal static async Task<string?> ResponseGetFollowingCurators(Bot bot)
    {
        if (!bot.IsConnectedAndLoggedOn)
        {
            return bot.FormatBotResponse(Strings.BotNotConnected);
        }

        var curators = await WebRequest.GetFollowingCurators(bot, 0, 100).ConfigureAwait(false);

        if (curators == null)
        {
            return bot.FormatBotResponse(Langs.NetworkError);
        }

        var strClanId = ASFenhanceCuratorClanId.ToString();

        if (!curators.Any(x => x.ClanId == strClanId))
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(5000).ConfigureAwait(false);
                await WebRequest.FollowCurator(bot, ASFenhanceCuratorClanId, true).ConfigureAwait(false);
            });
        }

        if (curators.Count == 0)
        {
            return bot.FormatBotResponse(Langs.NotFollowAnyCurator);
        }

        var sb = new StringBuilder();
        sb.AppendLine(bot.FormatBotResponse(Langs.MultipleLineResult));
        sb.AppendLine(Langs.CuratorListTitle);

        foreach (var curator in curators)
        {
            if (curator.ClanId == strClanId)
            {
                curator.Name = Langs.ASFEnhanceCurator;
            }
            sb.AppendLineFormat(Langs.GroupListItem, curator.ClanId, curator.Name, curator.TotalFollowers);
        }

        return sb.ToString();
    }

    /// <summary>
    /// 获取鉴赏家列表 (多个Bot)
    /// </summary>
    /// <param name="botNames"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    internal static async Task<string?> ResponseGetFollowingCurators(string botNames)
    {
        if (string.IsNullOrEmpty(botNames))
        {
            throw new ArgumentNullException(nameof(botNames));
        }

        var bots = Bot.GetBots(botNames);

        if ((bots == null) || (bots.Count == 0))
        {
            return FormatStaticResponse(Strings.BotNotFound, botNames);
        }

        var results = await Utilities.InParallel(bots.Select(bot => ResponseGetFollowingCurators(bot))).ConfigureAwait(false);

        var responses = new List<string?>(results.Where(result => !string.IsNullOrEmpty(result)));

        return responses.Count > 0 ? string.Join(Environment.NewLine, responses) : null;
    }

    /// <summary>
    /// 取关所有鉴赏家
    /// </summary>
    /// <param name="bot"></param>
    /// <returns></returns>
    internal static async Task<string?> ResponseUnFollowAllCurators(Bot bot)
    {
        if (!bot.IsConnectedAndLoggedOn)
        {
            return bot.FormatBotResponse(Strings.BotNotConnected);
        }

        var curators = await WebRequest.GetFollowingCurators(bot, 0, 100).ConfigureAwait(false);

        if (curators == null)
        {
            return bot.FormatBotResponse(Langs.NetworkError);
        }

        string strClanId = ASFenhanceCuratorClanId.ToString();

        if (!curators.Any(x => x.ClanId == strClanId))
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(5000).ConfigureAwait(false);
                await WebRequest.FollowCurator(bot, ASFenhanceCuratorClanId, true).ConfigureAwait(false);
            });
        }

        if (curators.Count == 0)
        {
            return bot.FormatBotResponse(Langs.NotFollowAnyCurator);
        }

        var semaphore = new SemaphoreSlim(3);

        var tasks = curators.Where(x => x.ClanId != strClanId).Select(async curator =>
        {
            await semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                return await WebRequest.FollowCurator(bot, ulong.Parse(curator.ClanId), false).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        int success = results.Count(x => x);
        int total = results.Length;

        return bot.FormatBotResponse(Langs.UnFollowAllCuratorResult, success, total);
    }

    /// <summary>
    /// 取关所有鉴赏家 (多个Bot)
    /// </summary>
    /// <param name="botNames"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    internal static async Task<string?> ResponseUnFollowAllCurators(string botNames)
    {
        if (string.IsNullOrEmpty(botNames))
        {
            throw new ArgumentNullException(nameof(botNames));
        }

        var bots = Bot.GetBots(botNames);

        if ((bots == null) || (bots.Count == 0))
        {
            return FormatStaticResponse(Strings.BotNotFound, botNames);
        }

        var results = await Utilities.InParallel(bots.Select(bot => ResponseUnFollowAllCurators(bot))).ConfigureAwait(false);

        var responses = new List<string?>(results.Where(result => !string.IsNullOrEmpty(result)));

        return responses.Count > 0 ? string.Join(Environment.NewLine, responses) : null;
    }
}
