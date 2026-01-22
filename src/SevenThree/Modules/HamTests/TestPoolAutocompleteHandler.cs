using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SevenThree.Database;

namespace SevenThree.Modules.HamTests
{
    /// <summary>
    /// Autocomplete handler for test pool selection in quiz commands.
    /// Shows available question pools with date ranges and status (current/upcoming/expired).
    /// </summary>
    public class TestPoolAutocompleteHandler : AutocompleteHandler
    {
        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
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
                return AutocompletionResult.FromSuccess(Enumerable.Empty<AutocompleteResult>());
            }

            var dbFactory = services.GetRequiredService<IDbContextFactory<SevenThreeContext>>();
            using var db = dbFactory.CreateDbContext();

            var today = DateTime.UtcNow.Date;
            var pools = await db.HamTest
                .Where(t => t.TestName == subCommandName)
                .ToListAsync();

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

            return AutocompletionResult.FromSuccess(results.Take(25));
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
