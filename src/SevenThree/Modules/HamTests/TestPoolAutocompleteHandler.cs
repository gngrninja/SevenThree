using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using SevenThree.Database;
using SevenThree.Services;

namespace SevenThree.Modules.HamTests
{
    /// <summary>
    /// Autocomplete handler for test pool selection in quiz commands.
    /// Shows available question pools with date ranges and status (current/upcoming/expired).
    /// Reads from HamTestService's in-memory pool cache — autocomplete cannot defer and must
    /// answer within Discord's 3-second window, so it never makes a (remote) DB call here.
    /// </summary>
    public class TestPoolAutocompleteHandler : AutocompleteHandler
    {
        public override Task<AutocompletionResult> GenerateSuggestionsAsync(
            IInteractionContext context,
            IAutocompleteInteraction autocomplete,
            IParameterInfo parameter,
            IServiceProvider services)
        {
            // Determine license type from the subcommand name (tech/general/extra)
            // The command structure is /quiz tech, /quiz general, /quiz extra
            var subCommandName = autocomplete.Data.Options
                .FirstOrDefault(o => o.Type == ApplicationCommandOptionType.SubCommand)?.Name;

            if (string.IsNullOrEmpty(subCommandName))
            {
                return Task.FromResult(AutocompletionResult.FromSuccess(Enumerable.Empty<AutocompleteResult>()));
            }

            var hamTestService = services.GetRequiredService<HamTestService>();
            var today = DateTime.UtcNow.Date;
            var pools = hamTestService.GetPools(subCommandName);

            // Sort: current pools first, then by FromDate descending
            var sortedPools = pools
                .OrderByDescending(p => p.FromDate <= today && p.ToDate >= today)
                .ThenByDescending(p => p.FromDate)
                .ToList();

            var results = sortedPools.Select(p =>
            {
                var status = GetPoolStatus(p, today);
                var label = $"{p.FromDate:yyyy-MM-dd} to {p.ToDate:yyyy-MM-dd} {status}";
                return new AutocompleteResult(label, p.TestId);
            }).ToList();

            return Task.FromResult(AutocompletionResult.FromSuccess(results.Take(25)));
        }

        private static string GetPoolStatus(HamTest pool, DateTime today)
        {
            if (pool.FromDate <= today && pool.ToDate >= today)
                return "(current)";
            if (pool.FromDate > today)
                return "(upcoming)";
            return "(expired)";
        }
    }
}
