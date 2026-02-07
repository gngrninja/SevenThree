using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SevenThree.Database;

namespace SevenThree.Modules.Study
{
    /// <summary>
    /// Autocomplete handler for study filter pool selection.
    /// Only shows pools the user has answer history with.
    /// </summary>
    public class StudyPoolAutocompleteHandler : AutocompleteHandler
    {
        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
            IInteractionContext context,
            IAutocompleteInteraction autocomplete,
            IParameterInfo parameter,
            IServiceProvider services)
        {
            var dbFactory = services.GetRequiredService<IDbContextFactory<SevenThreeContext>>();
            using var db = dbFactory.CreateDbContext();

            var userId = (long)context.User.Id;

            // Check if the user has selected a type filter (sibling parameter)
            var typeOption = autocomplete.Data.Options
                .FirstOrDefault(o => o.Name == "type" && !o.Focused)?.Value?.ToString();

            var query = db.UserAnswer
                .Include(ua => ua.Question)
                    .ThenInclude(q => q.Test)
                .Where(ua => ua.UserId == userId);

            if (!string.IsNullOrEmpty(typeOption))
                query = query.Where(ua => ua.Question.Test.TestName == typeOption);

            var pools = await query
                .Select(ua => ua.Question.Test)
                .Distinct()
                .ToListAsync();

            var today = DateTime.UtcNow.Date;
            var userInput = (autocomplete.Data.Current.Value?.ToString() ?? "").ToLower();

            var results = pools
                .OrderByDescending(p => p.FromDate <= today && p.ToDate >= today)
                .ThenByDescending(p => p.FromDate)
                .Select(p =>
                {
                    var status = GetPoolStatus(p, today);
                    var label = $"{p.TestName.ToUpper()} {p.FromDate:yyyy-MM-dd} to {p.ToDate:yyyy-MM-dd} {status}";
                    return new AutocompleteResult(label, p.TestId);
                })
                .Where(r => string.IsNullOrEmpty(userInput) || r.Name.ToLower().Contains(userInput))
                .Take(25)
                .ToList();

            return AutocompletionResult.FromSuccess(results);
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
