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
    /// Autocomplete handler for study filter subelement selection.
    /// Only shows subelements the user has answer history with.
    /// </summary>
    public class StudySubelementAutocompleteHandler : AutocompleteHandler
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

            // Read sibling parameter values for narrowing
            var typeValue = autocomplete.Data.Options
                .FirstOrDefault(o => o.Name == "type" && !o.Focused)?.Value?.ToString();
            var poolValue = autocomplete.Data.Options
                .FirstOrDefault(o => o.Name == "pool" && !o.Focused)?.Value?.ToString();

            var query = db.UserAnswer
                .Include(ua => ua.Question)
                    .ThenInclude(q => q.Test)
                .Where(ua => ua.UserId == userId);

            if (!string.IsNullOrEmpty(typeValue))
                query = query.Where(ua => ua.Question.Test.TestName == typeValue);

            if (!string.IsNullOrEmpty(poolValue) && int.TryParse(poolValue, out var poolId))
                query = query.Where(ua => ua.Question.Test.TestId == poolId);

            var subelements = await query
                .Select(ua => new { ua.Question.SubelementName, ua.Question.SubelementDesc })
                .Distinct()
                .OrderBy(s => s.SubelementName)
                .ToListAsync();

            var userInput = (autocomplete.Data.Current.Value?.ToString() ?? "").ToLower();

            var results = subelements
                .Where(s => s.SubelementName != null)
                .Select(s =>
                {
                    var desc = s.SubelementDesc ?? "No description";
                    var label = $"{s.SubelementName} - {desc}";
                    if (label.Length > 100) label = label[..100];
                    return new AutocompleteResult(label, s.SubelementName);
                })
                .Where(r => string.IsNullOrEmpty(userInput) || r.Name.ToLower().Contains(userInput))
                .Take(25)
                .ToList();

            return AutocompletionResult.FromSuccess(results);
        }
    }
}
