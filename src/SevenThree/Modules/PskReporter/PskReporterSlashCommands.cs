using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SevenThree.Models;
using SevenThree.Services;

namespace SevenThree.Modules.PskReporter
{
    [Group("psk", "PSKReporter propagation and spot commands")]
    public class PskReporterSlashCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly PskReporterService _pskService;
        private readonly ILogger<PskReporterSlashCommands> _logger;

        public PskReporterSlashCommands(IServiceProvider services)
        {
            _pskService = services.GetRequiredService<PskReporterService>();
            _logger = services.GetRequiredService<ILogger<PskReporterSlashCommands>>();
        }

        [SlashCommand("spots", "Show recent spots for a callsign (who's hearing them)")]
        public async Task GetSpots(
            [Summary("callsign", "The callsign to look up")] string callsign,
            [Summary("minutes", "Time window in minutes (default: 60, max: 360)")] int minutes = 60)
        {
            // Defer IMMEDIATELY to avoid 3-second timeout
            await DeferAsync();

            try
            {
                if (_pskService.IsOnCooldown(Context.User.Id, out var remaining))
                {
                    await FollowupAsync(
                        $"Please wait {remaining.TotalSeconds:F0} seconds before making another PSKReporter request.");
                    return;
                }

                minutes = Math.Clamp(minutes, 5, 360);
                callsign = callsign.ToUpperInvariant().Trim();

                var reports = await _pskService.GetSenderSpotsAsync(callsign, Context.User.Id, minutes, 500);
                var spots = _pskService.ConvertToSpotInfo(reports);

                var cached = new PskReporterService.CachedSpotResult
                {
                    Spots = spots,
                    Title = $"Spotted by: {callsign}",
                    QueryType = "spots",
                    Callsign = callsign,
                    Minutes = minutes,
                    UserId = Context.User.Id
                };
                var sessionId = _pskService.CacheSpots(cached);

                var embed = BuildPaginatedEmbed(cached, 0);
                var components = BuildNavigationButtons(sessionId, 0, cached.TotalPages);

                await FollowupAsync(embed: embed, components: components);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching spots for {Callsign}", callsign);
                await FollowupAsync($"Error fetching spots for {callsign}. Please try again later.");
            }
        }

        [SlashCommand("hearing", "Show what stations a callsign is hearing")]
        public async Task GetHearing(
            [Summary("callsign", "The receiving station callsign")] string callsign,
            [Summary("minutes", "Time window in minutes (default: 60, max: 360)")] int minutes = 60)
        {
            // Defer IMMEDIATELY to avoid 3-second timeout
            await DeferAsync();

            try
            {
                if (_pskService.IsOnCooldown(Context.User.Id, out var remaining))
                {
                    await FollowupAsync(
                        $"Please wait {remaining.TotalSeconds:F0} seconds before making another PSKReporter request.");
                    return;
                }

                minutes = Math.Clamp(minutes, 5, 360);
                callsign = callsign.ToUpperInvariant().Trim();

                var reports = await _pskService.GetReceiverSpotsAsync(callsign, Context.User.Id, minutes, 500);
                var spots = _pskService.ConvertToSpotInfo(reports);

                var cached = new PskReporterService.CachedSpotResult
                {
                    Spots = spots,
                    Title = $"Hearing: {callsign}",
                    QueryType = "hearing",
                    Callsign = callsign,
                    Minutes = minutes,
                    UserId = Context.User.Id
                };
                var sessionId = _pskService.CacheSpots(cached);

                var embed = BuildPaginatedEmbed(cached, 0);
                var components = BuildNavigationButtons(sessionId, 0, cached.TotalPages);

                await FollowupAsync(embed: embed, components: components);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching hearing data for {Callsign}", callsign);
                await FollowupAsync($"Error fetching data for {callsign}. Please try again later.");
            }
        }

        [SlashCommand("propagation", "Show propagation statistics for a callsign")]
        public async Task GetPropagation(
            [Summary("callsign", "The callsign to analyze")] string callsign,
            [Summary("minutes", "Time window in minutes (default: 60, max: 360)")] int minutes = 60)
        {
            // Defer IMMEDIATELY to avoid 3-second timeout
            await DeferAsync();

            try
            {
                if (_pskService.IsOnCooldown(Context.User.Id, out var remaining))
                {
                    await FollowupAsync(
                        $"Please wait {remaining.TotalSeconds:F0} seconds before making another PSKReporter request.");
                    return;
                }

                minutes = Math.Clamp(minutes, 5, 360);
                callsign = callsign.ToUpperInvariant().Trim();

                var reports = await _pskService.GetSenderSpotsAsync(callsign, Context.User.Id, minutes, 500);
                var spots = _pskService.ConvertToSpotInfo(reports);
                var stats = _pskService.BuildPropagationStats(callsign, spots, minutes);

                var embed = BuildPropagationEmbed(stats);
                var components = new ComponentBuilder()
                    .WithButton("View on PSKReporter", style: ButtonStyle.Link,
                        url: $"https://pskreporter.info/pskmap.html?preset&callsign={Uri.EscapeDataString(callsign)}&what=all")
                    .Build();

                await FollowupAsync(embed: embed, components: components);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching propagation for {Callsign}", callsign);
                await FollowupAsync($"Error fetching propagation data for {callsign}. Please try again later.");
            }
        }

        [SlashCommand("band", "Show activity on a specific band")]
        public async Task GetBandActivity(
            [Summary("band", "Band to check (e.g., 20m, 40m, 6m)")]
            [Choice("160m", "160m")]
            [Choice("80m", "80m")]
            [Choice("60m", "60m")]
            [Choice("40m", "40m")]
            [Choice("30m", "30m")]
            [Choice("20m", "20m")]
            [Choice("17m", "17m")]
            [Choice("15m", "15m")]
            [Choice("12m", "12m")]
            [Choice("10m", "10m")]
            [Choice("6m", "6m")]
            [Choice("2m", "2m")]
            string band,
            [Summary("mode", "Filter by mode (optional)")]
            [Choice("FT8", "FT8")]
            [Choice("FT4", "FT4")]
            [Choice("CW", "CW")]
            [Choice("PSK31", "PSK31")]
            [Choice("RTTY", "RTTY")]
            [Choice("JS8", "JS8")]
            string mode = null,
            [Summary("minutes", "Time window in minutes (default: 15, max: 60)")] int minutes = 15)
        {
            // Defer IMMEDIATELY to avoid 3-second timeout
            await DeferAsync();

            try
            {
                // Now we have 15 minutes - do validation
                if (_pskService.IsOnCooldown(Context.User.Id, out var remaining))
                {
                    await FollowupAsync(
                        $"Please wait {remaining.TotalSeconds:F0} seconds before making another PSKReporter request.");
                    return;
                }

                var freqRange = PskReporterService.GetBandFrequencyRange(band);
                if (!freqRange.HasValue)
                {
                    await FollowupAsync($"Unknown band: {band}");
                    return;
                }

                minutes = Math.Clamp(minutes, 5, 60);

                var reports = await _pskService.GetBandActivityAsync(
                    Context.User.Id,
                    freqRange.Value.Low,
                    freqRange.Value.High,
                    mode,
                    minutes,
                    500);

                var spots = _pskService.ConvertToSpotInfo(reports);
                var modeStr = string.IsNullOrEmpty(mode) ? "" : $" {mode}";

                var cached = new PskReporterService.CachedSpotResult
                {
                    Spots = spots,
                    Title = $"Band Activity: {band}{modeStr}",
                    QueryType = "band",
                    Band = band,
                    Mode = mode,
                    Minutes = minutes,
                    ActiveReceivers = reports.ActiveReceivers?.Count ?? 0,
                    UserId = Context.User.Id
                };
                var sessionId = _pskService.CacheSpots(cached);

                var embed = BuildPaginatedEmbed(cached, 0);
                var components = BuildNavigationButtons(sessionId, 0, cached.TotalPages);

                await FollowupAsync(embed: embed, components: components);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching band activity for {Band}", band);
                await FollowupAsync($"Error fetching band activity. Please try again later.");
            }
        }

        [SlashCommand("grid", "Show spots from a Maidenhead grid square")]
        public async Task GetGridSpots(
            [Summary("grid", "Maidenhead grid square (e.g., EN37, FN42hn)")] string grid,
            [Summary("minutes", "Time window in minutes (default: 60, max: 360)")] int minutes = 60)
        {
            await DeferAsync();

            try
            {
                if (_pskService.IsOnCooldown(Context.User.Id, out var remaining))
                {
                    await FollowupAsync(
                        $"Please wait {remaining.TotalSeconds:F0} seconds before making another PSKReporter request.");
                    return;
                }

                grid = grid.ToUpperInvariant().Trim();

                if (PskReporterService.GridToLatLon(grid) == null)
                {
                    await FollowupAsync(
                        $"Invalid grid square: **{grid}**. Please provide a valid Maidenhead grid (e.g., EN37, FN42hn).");
                    return;
                }

                minutes = Math.Clamp(minutes, 5, 360);

                var reports = await _pskService.GetGridSpotsAsync(grid, Context.User.Id, minutes, 500);
                var spots = _pskService.ConvertToSpotInfo(reports);

                var cached = new PskReporterService.CachedSpotResult
                {
                    Spots = spots,
                    Title = $"Grid Activity: {grid}",
                    QueryType = "grid",
                    Callsign = grid,
                    Minutes = minutes,
                    UserId = Context.User.Id
                };
                var sessionId = _pskService.CacheSpots(cached);

                var embed = BuildPaginatedEmbed(cached, 0);
                var components = BuildNavigationButtons(sessionId, 0, cached.TotalPages);

                await FollowupAsync(embed: embed, components: components);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching grid spots for {Grid}", grid);
                await FollowupAsync($"Error fetching spots for grid {grid}. Please try again later.");
            }
        }

        /// <summary>
        /// Build a paginated embed for spots
        /// </summary>
        public static Embed BuildPaginatedEmbed(PskReporterService.CachedSpotResult cached, int page)
        {
            var color = cached.QueryType switch
            {
                "spots" => new Color(0, 150, 255),
                "hearing" => new Color(0, 150, 255),
                "band" => new Color(255, 165, 0),
                "grid" => new Color(0, 200, 150),
                _ => new Color(100, 100, 100)
            };

            var embed = new EmbedBuilder()
                .WithTitle(cached.Title)
                .WithColor(color)
                .WithTimestamp(DateTimeOffset.UtcNow);

            if (cached.Spots.Count == 0)
            {
                embed.WithDescription($"No spots found in the last {cached.Minutes} minutes.")
                    .WithFooter($"PSKReporter | Last {cached.Minutes} minutes");
                return embed.Build();
            }

            var pageSpots = cached.Spots
                .Skip(page * PskReporterService.PAGE_SIZE)
                .Take(PskReporterService.PAGE_SIZE)
                .ToList();

            var sb = new StringBuilder();

            // Summary line
            var uniqueStations = cached.Spots.Select(s =>
                cached.QueryType == "hearing" ? s.SenderCallsign : s.ReceiverCallsign).Distinct().Count();
            sb.AppendLine($"**{cached.Spots.Count}** spots from **{uniqueStations}** unique stations");

            if (cached.ActiveReceivers > 0)
            {
                sb.AppendLine($"**{cached.ActiveReceivers}** active receivers monitoring");
            }
            sb.AppendLine();

            // Spot list
            foreach (var spot in pageSpots)
            {
                var timeAgo = FormatTimeAgo(spot.Timestamp);
                var snr = spot.SNR.HasValue ? $" [{spot.SNR:+0;-0}]" : "";
                var distance = spot.DistanceMi.HasValue ? $" ({spot.DistanceMi:F0} mi)" : "";

                if (cached.QueryType == "hearing")
                {
                    sb.AppendLine($"`{spot.Band,-4}` **{spot.SenderCallsign}** {spot.Mode}{snr} - {timeAgo}");
                }
                else if (cached.QueryType == "band")
                {
                    sb.AppendLine($"**{spot.SenderCallsign}** -> {spot.ReceiverCallsign}{snr} - {timeAgo}");
                }
                else // spots, grid
                {
                    var country = !string.IsNullOrEmpty(spot.ReceiverDXCC) ? $" ({spot.ReceiverDXCC})" : "";
                    sb.AppendLine($"`{spot.Band,-4}` **{spot.ReceiverCallsign}**{country} {spot.Mode}{snr}{distance} - {timeAgo}");
                }
            }

            embed.WithDescription(sb.ToString());

            // Add band breakdown for spots/hearing
            if (cached.QueryType != "band" && cached.Spots.Count > 0)
            {
                var bands = cached.Spots
                    .GroupBy(s => s.Band)
                    .OrderByDescending(g => g.Count())
                    .Take(5);
                var bandSummary = string.Join(", ", bands.Select(g => $"{g.Key}: {g.Count()}"));
                if (!string.IsNullOrEmpty(bandSummary))
                {
                    embed.AddField("Bands", bandSummary, inline: true);
                }

                var countries = cached.Spots
                    .Where(s => !string.IsNullOrEmpty(s.ReceiverDXCC))
                    .Select(s => s.ReceiverDXCC).Distinct().Count();
                embed.AddField("Countries", countries.ToString(), inline: true);
            }

            // Mode breakdown for band activity
            if (cached.QueryType == "band" && string.IsNullOrEmpty(cached.Mode))
            {
                var modeCounts = cached.Spots
                    .GroupBy(s => s.Mode)
                    .OrderByDescending(g => g.Count())
                    .Take(5);
                var modeStr = string.Join(", ", modeCounts.Select(g => $"{g.Key}: {g.Count()}"));
                if (!string.IsNullOrEmpty(modeStr))
                {
                    embed.AddField("Modes", modeStr, inline: true);
                }
            }

            var footer = $"PSKReporter | Last {cached.Minutes} min | Page {page + 1}/{cached.TotalPages} | {cached.Spots.Count} total";
            embed.WithFooter(footer);

            return embed.Build();
        }

        /// <summary>
        /// Build navigation buttons for pagination
        /// </summary>
        public static MessageComponent BuildNavigationButtons(string sessionId, int currentPage, int totalPages)
        {
            var builder = new ComponentBuilder();

            // Previous button
            builder.WithButton(
                label: "Previous",
                customId: $"{PskReporterService.BUTTON_PREFIX}:prev:{sessionId}:{currentPage}",
                style: ButtonStyle.Secondary,
                emote: new Emoji("◀️"),
                disabled: currentPage <= 0);

            // Page indicator (disabled button showing current page)
            builder.WithButton(
                label: $"{currentPage + 1} / {totalPages}",
                customId: $"{PskReporterService.BUTTON_PREFIX}:page:{sessionId}",
                style: ButtonStyle.Secondary,
                disabled: true);

            // Next button
            builder.WithButton(
                label: "Next",
                customId: $"{PskReporterService.BUTTON_PREFIX}:next:{sessionId}:{currentPage}",
                style: ButtonStyle.Secondary,
                emote: new Emoji("▶️"),
                disabled: currentPage >= totalPages - 1);

            // View on PSKReporter link
            builder.WithButton(
                label: "PSKReporter",
                style: ButtonStyle.Link,
                url: "https://pskreporter.info/pskmap.html",
                emote: new Emoji("🌐"));

            return builder.Build();
        }

        private Embed BuildPropagationEmbed(PropagationStats stats)
        {
            var embed = new EmbedBuilder()
                .WithTitle($"Propagation: {stats.Callsign}")
                .WithColor(new Color(50, 205, 50))
                .WithFooter($"PSKReporter | Last {stats.TimeWindowMinutes} minutes")
                .WithTimestamp(DateTimeOffset.UtcNow);

            if (stats.TotalSpots == 0)
            {
                embed.WithDescription($"No spots found for **{stats.Callsign}** in the last {stats.TimeWindowMinutes} minutes.");
                return embed.Build();
            }

            var sb = new StringBuilder();
            sb.AppendLine($"**{stats.Callsign}** spotted by **{stats.UniqueReceivers}** stations");
            sb.AppendLine($"in **{stats.UniqueCountries}** countries ({stats.TotalSpots} total spots)");
            embed.WithDescription(sb.ToString());

            if (stats.SpotsByBand.Count > 0)
            {
                var bandStr = string.Join("\n", stats.SpotsByBand
                    .OrderByDescending(kv => kv.Value)
                    .Take(6)
                    .Select(kv => $"`{kv.Key,-4}` {kv.Value} spots"));
                embed.AddField("By Band", bandStr, inline: true);
            }

            if (stats.SpotsByCountry.Count > 0)
            {
                var countryStr = string.Join("\n", stats.SpotsByCountry
                    .OrderByDescending(kv => kv.Value)
                    .Take(6)
                    .Select(kv => $"{GetFlagEmoji(kv.Key)} {kv.Value}"));
                embed.AddField("Top Countries", countryStr, inline: true);
            }

            if (stats.SpotsByMode.Count > 0)
            {
                var modeStr = string.Join(", ", stats.SpotsByMode
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => $"{kv.Key}: {kv.Value}"));
                embed.AddField("Modes", modeStr, inline: false);
            }

            if (stats.FurthestSpot != null)
            {
                var dx = stats.FurthestSpot;
                embed.AddField("Best DX",
                    $"**{dx.ReceiverCallsign}** ({dx.ReceiverDXCC ?? "?"})\n" +
                    $"{dx.DistanceMi:F0} mi / {dx.DistanceKm:F0} km on {dx.Band}",
                    inline: false);
            }

            return embed.Build();
        }

        private static string FormatTimeAgo(DateTime timestamp)
        {
            var diff = DateTime.UtcNow - timestamp;
            if (diff.TotalMinutes < 1)
                return "just now";
            if (diff.TotalMinutes < 60)
                return $"{diff.TotalMinutes:F0}m ago";
            if (diff.TotalHours < 24)
                return $"{diff.TotalHours:F1}h ago";
            return $"{diff.TotalDays:F0}d ago";
        }

        private static string GetFlagEmoji(string countryName)
        {
            var flags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["United States"] = ":flag_us:",
                ["Canada"] = ":flag_ca:",
                ["United Kingdom"] = ":flag_gb:",
                ["England"] = ":flag_gb:",
                ["Germany"] = ":flag_de:",
                ["Fed. Rep. of Germany"] = ":flag_de:",
                ["France"] = ":flag_fr:",
                ["Japan"] = ":flag_jp:",
                ["Australia"] = ":flag_au:",
                ["Brazil"] = ":flag_br:",
                ["Italy"] = ":flag_it:",
                ["Spain"] = ":flag_es:",
                ["Netherlands"] = ":flag_nl:",
                ["Poland"] = ":flag_pl:",
                ["Russia"] = ":flag_ru:",
                ["European Russia"] = ":flag_ru:",
                ["Asiatic Russia"] = ":flag_ru:",
                ["Ukraine"] = ":flag_ua:",
                ["Mexico"] = ":flag_mx:",
                ["Argentina"] = ":flag_ar:",
                ["Chile"] = ":flag_cl:",
                ["South Africa"] = ":flag_za:",
                ["New Zealand"] = ":flag_nz:",
                ["Switzerland"] = ":flag_ch:",
                ["Austria"] = ":flag_at:",
                ["Belgium"] = ":flag_be:",
                ["Sweden"] = ":flag_se:",
                ["Norway"] = ":flag_no:",
                ["Finland"] = ":flag_fi:",
                ["Denmark"] = ":flag_dk:",
                ["Portugal"] = ":flag_pt:",
                ["Czech Republic"] = ":flag_cz:",
                ["Hungary"] = ":flag_hu:",
                ["Puerto Rico"] = ":flag_pr:",
                ["Hawaii"] = ":flag_us:",
                ["Alaska"] = ":flag_us:",
            };

            return flags.TryGetValue(countryName, out var flag) ? flag : ":globe_with_meridians:";
        }
    }
}
