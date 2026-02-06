using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SevenThree.Database;
using SevenThree.Models;

namespace SevenThree.Modules
{
    public enum ImportTarget
    {
        Tech,
        General,
        Extra,
        All
    }

    public class AdminSlashCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DiscordSocketClient _client;
        private readonly ILogger<AdminSlashCommands> _logger;
        private readonly IDbContextFactory<SevenThreeContext> _contextFactory;

        public AdminSlashCommands(IServiceProvider services)
        {
            _client = services.GetRequiredService<DiscordSocketClient>();
            _logger = services.GetRequiredService<ILogger<AdminSlashCommands>>();
            _contextFactory = services.GetRequiredService<IDbContextFactory<SevenThreeContext>>();
        }

        [SlashCommand("playing", "Set the bot's playing status (bot owner only)")]
        [RequireOwner]
        public async Task ChangePlaying(
            [Summary("status", "The status message to display")] string status)
        {
            await _client.SetGameAsync(status);
            await RespondAsync($"Playing status set to: {status}", ephemeral: true);
        }

        [SlashCommand("ping", "Check if the bot is responsive")]
        public async Task Ping()
        {
            var latency = _client.Latency;
            await RespondAsync($"Pong! Latency: {latency}ms", ephemeral: true);
        }

        [SlashCommand("import", "Import question pool(s) from JSON (bot owner only)")]
        [RequireOwner]
        public async Task ImportQuestions(
            [Summary("target", "License class to import (or All for all classes)")] ImportTarget target)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                if (target == ImportTarget.All)
                {
                    await ImportAllLicenseTypes();
                }
                else
                {
                    var result = await ImportLicenseType(target.ToString().ToLower());
                    await FollowupAsync(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing questions");
                await FollowupAsync($"Error importing: {ex.Message}");
            }
        }

        private async Task ImportAllLicenseTypes()
        {
            var results = new List<string>();
            var licenseTypes = new[] { "tech", "general", "extra" };

            foreach (var licenseType in licenseTypes)
            {
                var result = await ImportLicenseType(licenseType);
                results.Add($"**{licenseType.ToUpper()}**:\n{result}");
            }

            await FollowupAsync(string.Join("\n\n", results));
        }

        private async Task<string> ImportLicenseType(string testName)
        {
            using var db = _contextFactory.CreateDbContext();
            var testDesc = $"U.S. Ham Radio test for the {testName} class license.";

            var importDir = $"import/{testName}";
            if (!Directory.Exists(importDir))
            {
                return $"Import directory not found: {importDir}";
            }

            var importResults = new List<string>();
            var totalFiguresImported = 0;

            // Check for dated subfolders (e.g., "2022-2026", "2026-2030")
            var subDirs = Directory.GetDirectories(importDir)
                .Where(d => Path.GetFileName(d).Contains('-'))
                .ToList();

            if (subDirs.Count > 0)
            {
                // Process each subfolder as a separate pool with its own figures
                foreach (var subDir in subDirs)
                {
                    var result = await ImportPoolFromDirectory(db, testName, testDesc, subDir);
                    importResults.Add(result.Message);
                    totalFiguresImported += result.FiguresImported;
                }
            }
            else
            {
                // Flat structure (backwards compatible) - single pool
                var result = await ImportPoolFromDirectory(db, testName, testDesc, importDir);
                importResults.Add(result.Message);
                totalFiguresImported += result.FiguresImported;
            }

            if (importResults.Count == 0 || importResults.All(r => r.StartsWith("Skipped") || r.StartsWith("No ")))
            {
                return $"No valid import files found for {testName}!";
            }

            var summary = string.Join("\n", importResults);
            return $"{summary}\nTotal figures: {totalFiguresImported}";
        }

        /// <summary>
        /// Import a single test pool from a directory (subfolder or main folder).
        /// Uses upsert to preserve UserAnswer history - questions are matched by QuestionSection.
        /// </summary>
        private async Task<(string Message, int FiguresImported)> ImportPoolFromDirectory(
            SevenThreeContext db, string testName, string testDesc, string directory)
        {
            // Find JSON file in this directory
            var jsonFiles = Directory.GetFiles(directory, $"{testName}_*.json");
            if (jsonFiles.Length == 0)
            {
                return ($"No JSON file in {Path.GetFileName(directory)}", 0);
            }

            var curFile = jsonFiles[0]; // Use first JSON file found
            var fileName = Path.GetFileNameWithoutExtension(curFile);
            var fileNameParts = fileName.Split('_');

            if (fileNameParts.Length < 3)
            {
                return ($"Skipped {fileName}: invalid format", 0);
            }

            if (!DateTime.TryParse(fileNameParts[1], out var parsedStartDate) ||
                !DateTime.TryParse(fileNameParts[2], out var parsedEndDate))
            {
                return ($"Skipped {fileName}: could not parse dates", 0);
            }

            DateTime startDate = DateTime.SpecifyKind(parsedStartDate, DateTimeKind.Utc);
            DateTime endDate = DateTime.SpecifyKind(parsedEndDate, DateTimeKind.Utc);
            var importTime = DateTime.UtcNow;

            // Find existing test pool by TestName + date range
            var test = await db.HamTest
                .Where(t => t.TestName == testName && t.FromDate == startDate && t.ToDate == endDate)
                .FirstOrDefaultAsync();

            bool isNewPool = test == null;
            if (isNewPool)
            {
                // Create new test pool
                test = new HamTest
                {
                    TestName = testName,
                    TestDescription = testDesc,
                    FromDate = startDate,
                    ToDate = endDate
                };
                await db.AddAsync(test);
                await db.SaveChangesAsync();
            }

            // Load existing questions for this pool (keyed by QuestionSection)
            var existingQuestions = await db.Questions
                .Where(q => q.Test.TestId == test.TestId)
                .ToDictionaryAsync(q => q.QuestionSection);

            // Track import stats
            int newCount = 0;
            int updatedCount = 0;
            int archivedCount = 0;
            var importedSections = new HashSet<string>();

            // Import questions from JSON
            var questionData = QuestionIngest.FromJson(await File.ReadAllTextAsync(curFile));

            foreach (var item in questionData)
            {
                importedSections.Add(item.QuestionId);

                if (existingQuestions.TryGetValue(item.QuestionId, out var existingQuestion))
                {
                    // UPDATE existing question (preserves QuestionId, so UserAnswers stay linked)
                    existingQuestion.QuestionText = item.Question;
                    existingQuestion.FccPart = item.FccPart;
                    existingQuestion.SubelementDesc = item.SubelementDesc;
                    existingQuestion.FigureName = item.Figure;
                    existingQuestion.SubelementName = item.SubelementName?.ToString();
                    existingQuestion.IsArchived = false;
                    existingQuestion.LastImportedAt = importTime;

                    // Delete old answers and recreate (safe - no FK from UserAnswer)
                    var oldAnswers = await db.Answer
                        .Where(a => a.Question.QuestionId == existingQuestion.QuestionId)
                        .ToListAsync();
                    db.RemoveRange(oldAnswers);

                    // Add new answers
                    var answerChar = item.AnswerKey.ToString()[0];
                    foreach (var answer in item.PossibleAnswer)
                    {
                        var posAnswerChar = answer[0];
                        var posAnswerText = answer.Length > 3 ? answer.Substring(3) : answer;

                        db.Answer.Add(new Answer
                        {
                            Question = existingQuestion,
                            AnswerText = posAnswerText,
                            IsAnswer = answerChar == posAnswerChar
                        });
                    }

                    updatedCount++;
                }
                else
                {
                    // INSERT new question
                    var newQuestion = new Questions
                    {
                        QuestionText = item.Question,
                        QuestionSection = item.QuestionId,
                        FccPart = item.FccPart,
                        SubelementDesc = item.SubelementDesc,
                        FigureName = item.Figure,
                        SubelementName = item.SubelementName?.ToString(),
                        Test = test,
                        IsArchived = false,
                        LastImportedAt = importTime
                    };
                    db.Questions.Add(newQuestion);

                    var answerChar = item.AnswerKey.ToString()[0];
                    foreach (var answer in item.PossibleAnswer)
                    {
                        var posAnswerChar = answer[0];
                        var posAnswerText = answer.Length > 3 ? answer.Substring(3) : answer;

                        db.Answer.Add(new Answer
                        {
                            Question = newQuestion,
                            AnswerText = posAnswerText,
                            IsAnswer = answerChar == posAnswerChar
                        });
                    }

                    newCount++;
                }
            }

            // Archive questions that were NOT in this import (removed from pool)
            foreach (var existingQuestion in existingQuestions.Values)
            {
                if (!importedSections.Contains(existingQuestion.QuestionSection) && !existingQuestion.IsArchived)
                {
                    existingQuestion.IsArchived = true;
                    archivedCount++;
                }
            }

            await db.SaveChangesAsync();

            // Import figures - upsert by (TestId, FigureName)
            var existingFigures = await db.Figure
                .Where(f => f.Test.TestId == test.TestId)
                .ToDictionaryAsync(f => f.FigureName);

            var figureFiles = Directory.EnumerateFiles(directory, $"{testName}_*.png");
            var figuresImported = 0;

            foreach (var file in figureFiles)
            {
                var figureName = Path.GetFileNameWithoutExtension(file)
                    .Replace($"{testName}_", "")
                    .Trim();

                var contents = await File.ReadAllBytesAsync(file);

                if (existingFigures.TryGetValue(figureName, out var existingFigure))
                {
                    // Update existing figure
                    existingFigure.FigureImage = contents;
                }
                else
                {
                    // Add new figure
                    db.Figure.Add(new Figure
                    {
                        Test = test,
                        FigureName = figureName,
                        FigureImage = contents
                    });
                }
                figuresImported++;
            }

            if (figuresImported > 0)
            {
                await db.SaveChangesAsync();
            }

            var poolLabel = isNewPool ? "NEW" : "updated";
            var stats = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd} ({poolLabel}): " +
                        $"{newCount} new, {updatedCount} updated, {archivedCount} archived, {figuresImported} figures";
            return (stats, figuresImported);
        }
    }
}
