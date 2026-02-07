using Discord;
using Discord.Interactions;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SevenThree.Constants;

namespace SevenThree.Modules
{
    [Group("qrz", "QRZ.com callsign lookup commands")]
    public class QrzLookupSlashCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly QrzApi _qrzApi;
        private readonly ILogger<QrzLookupSlashCommands> _logger;

        public QrzLookupSlashCommands(IServiceProvider services)
        {
            _logger = services.GetRequiredService<ILogger<QrzLookupSlashCommands>>();
            _qrzApi = services.GetRequiredService<QrzApi>();
        }

        [SlashCommand("lookup", "Look up a ham radio callsign on QRZ.com")]
        public async Task LookupCall(
            [Summary("callsign", "The callsign to look up")] string callsign)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                if (!_qrzApi.IsConfigured)
                {
                    await FollowupAsync("QRZ API is not configured. Please set QRZ credentials in environment variables.", ephemeral: true);
                    return;
                }

                Models.QrzApiXml.QRZDatabase result = null;
                string callSignLong = string.Empty;

                if (callsign.Contains("/"))
                {
                    callSignLong = callsign;
                    callsign = callsign.Split('/')[0].Trim();
                }

                if (!string.IsNullOrEmpty(callSignLong))
                {
                    result = await _qrzApi.GetCallInfo(callSignLong);
                    if (result.Session.Error != null && result.Session.Error.Contains("Not found:"))
                    {
                        result = await _qrzApi.GetCallInfo(callsign);
                    }
                }
                else
                {
                    result = await _qrzApi.GetCallInfo(callsign);
                }

                await FollowupAsync(embed: BuildCallEmbed(callsign, result, Context.User));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error looking up callsign {Callsign}", callsign);
                await FollowupAsync("An error occurred while looking up the callsign.", ephemeral: true);
            }
        }

        [SlashCommand("dxcc", "Look up DXCC information")]
        public async Task FindDxcc(
            [Summary("dxcc", "The DXCC entity to look up")] string dxcc)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                if (!_qrzApi.IsConfigured)
                {
                    await FollowupAsync("QRZ API is not configured. Please set QRZ credentials in environment variables.", ephemeral: true);
                    return;
                }

                Models.QrzApiXml.QRZDatabase result = null;
                string callSignLong = string.Empty;

                if (dxcc.Contains("/"))
                {
                    callSignLong = dxcc;
                    dxcc = dxcc.Split('/')[0].Trim();
                }

                if (!string.IsNullOrEmpty(callSignLong))
                {
                    result = await _qrzApi.GetDxccInfo(callSignLong);
                    if (result.Session.Error != null && result.Session.Error.Contains("Not found:"))
                    {
                        result = await _qrzApi.GetDxccInfo(dxcc);
                    }
                }
                else
                {
                    result = await _qrzApi.GetDxccInfo(dxcc);
                }

                var embed = new EmbedBuilder();
                if (result.Session.Error == null)
                {
                    embed.Title = $"DXCC information for [{result.DXCC.Dxcc}]";

                    if (result.DXCC.Cc != null)
                    {
                        embed.Fields.Add(new EmbedFieldBuilder
                        {
                            Name = "CC",
                            Value = $"{result.DXCC.Cc}",
                            IsInline = true
                        });
                    }

                    embed.ThumbnailUrl = QuizConstants.BOT_THUMBNAIL_URL;
                    embed.WithColor(new Color(0, 255, 50));
                }
                else
                {
                    embed.Title = $"Error looking up [{dxcc}]!";
                    embed.WithFields(new EmbedFieldBuilder
                    {
                        Name = "Error Details",
                        Value = result.Session.Error
                    });
                    embed.WithColor(new Color(255, 0, 0));
                }

                embed.WithAuthor(new EmbedAuthorBuilder
                {
                    Name = $"DXCC look up performed by [{Context.User.Username}]!",
                    IconUrl = Context.User.GetAvatarUrl()
                });

                embed.WithFooter(new EmbedFooterBuilder
                {
                    Text = "SevenThree, your local ham radio Discord bot!",
                    IconUrl = QuizConstants.BOT_THUMBNAIL_URL
                });

                await FollowupAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error looking up DXCC {Dxcc}", dxcc);
                await FollowupAsync("An error occurred while looking up DXCC information.", ephemeral: true);
            }
        }

        internal static Embed BuildCallEmbed(string callsign, Models.QrzApiXml.QRZDatabase result, IUser user)
        {
            var embed = new EmbedBuilder();

            if (result.Session.Error == null)
            {
                embed.Title = $"Callsign information for [{result.Callsign.Call}]";

                if (result.Callsign.Fname != null && result.Callsign.Name != null)
                {
                    embed.Fields.Add(new EmbedFieldBuilder
                    {
                        Name = "Name",
                        Value = $"{result.Callsign.Fname} {result.Callsign.Name}",
                        IsInline = true
                    });
                }

                if (result.Callsign.Class != null)
                {
                    embed.Fields.Add(new EmbedFieldBuilder
                    {
                        Name = "Class",
                        Value = result.Callsign.Class,
                        IsInline = true
                    });
                }

                if (result.Callsign.U_views != null)
                {
                    embed.Fields.Add(new EmbedFieldBuilder
                    {
                        Name = "Profile Views",
                        Value = result.Callsign.U_views,
                        IsInline = true
                    });
                }

                if (result.Callsign.Lat != null && result.Callsign.Lon != null)
                {
                    embed.Fields.Add(new EmbedFieldBuilder
                    {
                        Name = "Lat/Long",
                        Value = $"{result.Callsign.Lat}/{result.Callsign.Lon}",
                        IsInline = true
                    });
                }

                if (result.Callsign.Land != null)
                {
                    embed.Fields.Add(new EmbedFieldBuilder
                    {
                        Name = "Country",
                        Value = $"{result.Callsign.Land}",
                        IsInline = true
                    });
                }

                if (result.Callsign.TimeZone != null)
                {
                    embed.Fields.Add(new EmbedFieldBuilder
                    {
                        Name = "Timezone",
                        Value = $"{result.Callsign.TimeZone}",
                        IsInline = true
                    });
                }

                if (result.Callsign.Efdate != null)
                {
                    embed.Fields.Add(new EmbedFieldBuilder
                    {
                        Name = "License Granted",
                        Value = $"{result.Callsign.Efdate}",
                        IsInline = true
                    });
                }

                if (result.Callsign.Expdate != null)
                {
                    embed.Fields.Add(new EmbedFieldBuilder
                    {
                        Name = "License Expires",
                        Value = $"{result.Callsign.Expdate}",
                        IsInline = true
                    });
                }

                embed.Fields.Add(new EmbedFieldBuilder
                {
                    Name = "QRZ Profile Link",
                    Value = $"https://qrz.com/db/{result.Callsign.Call}"
                });

                if (!string.IsNullOrEmpty(result.Callsign.Image))
                {
                    embed.ImageUrl = result.Callsign.Image;
                    embed.ThumbnailUrl = result.Callsign.Image;
                }
                else
                {
                    embed.ThumbnailUrl = QuizConstants.BOT_THUMBNAIL_URL;
                }

                embed.WithColor(new Color(0, 255, 50));
            }
            else
            {
                embed.Title = $"Error looking up [{callsign}]!";
                embed.WithFields(new EmbedFieldBuilder
                {
                    Name = "Error Details",
                    Value = result.Session.Error
                });
                embed.WithColor(new Color(255, 0, 0));
            }

            embed.WithAuthor(new EmbedAuthorBuilder
            {
                Name = $"Callsign look up performed by [{user.Username}]!",
                IconUrl = user.GetAvatarUrl()
            });

            embed.WithFooter(new EmbedFooterBuilder
            {
                Text = "SevenThree, your local ham radio Discord bot!",
                IconUrl = QuizConstants.BOT_THUMBNAIL_URL
            });

            return embed.Build();
        }
    }
}
