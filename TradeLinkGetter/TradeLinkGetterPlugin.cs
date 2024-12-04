using ArchiSteamFarm.Core;
using ArchiSteamFarm.Localization;
using ArchiSteamFarm.Plugins.Interfaces;
using ArchiSteamFarm.Steam.Interaction;
using ArchiSteamFarm.Steam;
using SteamKit2;
using System.Collections.Generic;
using System.ComponentModel;
using System.Composition;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System;
using static ArchiSteamFarm.Steam.Integration.ArchiWebHandler;

namespace TradeLinkGetter;

[Export(typeof(IPlugin))]
internal sealed class TradeLinkGetterPlugin : IBotCommand2, IGitHubPluginUpdates {
	private static Uri TradeOfferURL = new(SteamCommunityURL, "/tradeoffer/new");

	public string Name => nameof(TradeLinkGetterPlugin);
	public string RepositoryName => "dm1tz/TradeLinkGetter";
	public Version Version => typeof(TradeLinkGetterPlugin).Assembly.GetName().Version ?? throw new InvalidOperationException(nameof(Version));

	public Task OnLoaded() => Task.CompletedTask;

	public async Task<string?> OnBotCommand(Bot bot, EAccess access, string message, string[] args, ulong steamID = 0) {
		ArgumentNullException.ThrowIfNull(bot);

		if (!Enum.IsDefined(access)) {
			throw new InvalidEnumArgumentException(nameof(access), (int) access, typeof(EAccess));
		}

		ArgumentException.ThrowIfNullOrEmpty(message);

		if ((args == null) || (args.Length == 0)) {
			throw new ArgumentNullException(nameof(args));
		}

		if ((steamID != 0) && !new SteamID(steamID).IsIndividualAccount) {
			throw new ArgumentOutOfRangeException(nameof(steamID));
		}

		return args[0].ToUpperInvariant() switch {
			"TRADELINK" or "TL" when args.Length > 1 => await ResponseTradeLink(access, Utilities.GetArgsAsText(args, 1, ","), steamID).ConfigureAwait(false),
			"TRADELINK" or "TL" => await ResponseTradeLink(bot, access).ConfigureAwait(false),
			"TRADELINKGETTER" or "TLG" => ResponseVersion(access),
			_ => null
		};
	}

	private async static Task<string?> ResponseTradeLink(Bot bot, EAccess access) {
		if (!bot.IsConnectedAndLoggedOn) {
			return bot.Commands.FormatBotResponse(Strings.BotNotConnected);
		}

		if (access < EAccess.FamilySharing) {
			return access > EAccess.None ? bot.Commands.FormatBotResponse(Strings.ErrorAccessDenied) : null;
		}

		string? tradeToken = await bot.ArchiHandler.GetTradeToken().ConfigureAwait(false);

		if (string.IsNullOrEmpty(tradeToken)) {
			return bot.Commands.FormatBotResponse(string.Format(CultureInfo.CurrentCulture, Strings.ErrorIsEmpty, nameof(tradeToken)));
		}

		uint partnerID = new SteamID(bot.SteamID).AccountID;

		return bot.Commands.FormatBotResponse($"{TradeOfferURL}/?partner={partnerID}&token={tradeToken}");
	}

	private static async Task<string?> ResponseTradeLink(EAccess access, string botNames, ulong steamID = 0) {
		ArgumentException.ThrowIfNullOrEmpty(botNames);

		if ((steamID != 0) && !new SteamID(steamID).IsIndividualAccount) {
			throw new ArgumentOutOfRangeException(nameof(steamID));
		}

		HashSet<Bot>? bots = Bot.GetBots(botNames);

		if ((bots == null) || (bots.Count == 0)) {
			return access >= EAccess.Owner ? Commands.FormatStaticResponse(string.Format(CultureInfo.CurrentCulture, Strings.BotNotFound, botNames)) : null;
		}

		IList<string?> results = await Utilities.InParallel(bots.Select(bot => Task.Run(() => ResponseTradeLink(bot, Commands.GetProxyAccess(bot, access, steamID))))).ConfigureAwait(false);

		List<string> responses = [..results.Where(static result => !string.IsNullOrEmpty(result))!];

		return responses.Count > 0 ? string.Join(Environment.NewLine, responses) : null;
	}

	private static string? ResponseVersion(EAccess access) {
		return access >= EAccess.FamilySharing ? Commands.FormatStaticResponse(string.Format(CultureInfo.CurrentCulture, Strings.BotVersion, nameof(TradeLinkGetterPlugin), typeof(TradeLinkGetterPlugin).Assembly.GetName().Version)) : null;
	}
}
