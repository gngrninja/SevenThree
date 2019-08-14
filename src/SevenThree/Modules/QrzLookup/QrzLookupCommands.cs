using Discord;
using Discord.Net;
using Discord.WebSocket;
using Discord.Commands;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SevenThree.Modules
{
    public class QrzLookupCommands : ModuleBase
    {
        private readonly QrzApi _qrzApi;
        private readonly ILogger _logger;
        private readonly IConfiguration _config;

        public QrzLookupCommands(IServiceProvider services) 
        {
            _config = services.GetRequiredService<IConfiguration>();
            _logger = services.GetRequiredService<ILogger<QrzLookupCommands>>();
            _qrzApi = services.GetRequiredService<QrzApi>();
        }

        [Command("lookup")]
        public async Task LookupCall([Remainder] string callsign)
        {
            var result = await _qrzApi.GetCallInfo(callsign);
            var embed = new EmbedBuilder();
            embed.Title = $"{result.Callsign.Url}Callsign information for {result.Callsign.Call}";

            if (result.Callsign.Fname != null && result.Callsign.Name != null)
            {
                embed.Fields.Add(new EmbedFieldBuilder{
                    Name = "Name",
                    Value = $"{result.Callsign.Fname} {result.Callsign.Name}",
                    IsInline = true
                }); 
            }

            if (result.Callsign.Class != null)
            {
                embed.Fields.Add(new EmbedFieldBuilder{
                    Name = "Class",
                    Value = result.Callsign.Class,
                    IsInline = true
                });         
            }
               

            if (result.Callsign.U_views != null)
            {
                embed.Fields.Add(new EmbedFieldBuilder{
                    Name = "Profile Views",
                    Value = result.Callsign.U_views,
                    IsInline = true
                });
            }   

            if (result.Callsign.Lat != null && result.Callsign.Long != null)
            {
                embed.Fields.Add(new EmbedFieldBuilder{
                    Name = "Lat/Long",
                    Value = $"{result.Callsign.Lat}/{result.Callsign.Lon}",
                    IsInline = true
                });
            }

            if (result.Callsign.Land != null)
            {
                embed.Fields.Add(new EmbedFieldBuilder{
                    Name = "Country",
                    Value = $"{result.Callsign.Land}",
                    IsInline = true
                });
            }
            else if (result.Callsign.Country != null)
            {
                embed.Fields.Add(new EmbedFieldBuilder{
                    Name = "Country",
                    Value = $"{result.Callsign.Land}",
                    IsInline = true
                });
            }

            embed.Fields.Add(new EmbedFieldBuilder{
                Name = "Timezone",
                Value = $"{result.Callsign.TimeZone}",
                IsInline = true
            });

            if (result.Callsign.Efdate != null)
            {
                embed.Fields.Add(new EmbedFieldBuilder{
                    Name = "License Granted",
                    Value = $"{result.Callsign.Efdate}",
                    IsInline = true
                });
            } 

            if (result.Callsign.Expdate != null)
            {
                embed.Fields.Add(new EmbedFieldBuilder{
                    Name = "License Expires",
                    Value = $"{result.Callsign.Expdate}",
                    IsInline = true
                });
            }

            embed.Fields.Add(new EmbedFieldBuilder{
                Name = "QRZ Profile Link",
                Value = $"https://qrz.com/db/{result.Callsign.Call}"
            });

            embed.WithColor(new Color(0, 255, 50));

            if (!string.IsNullOrEmpty(result.Callsign.Image))
            {
                embed.ImageUrl = result.Callsign.Image;
            }            
            embed.ThumbnailUrl = Context.User.GetAvatarUrl();
            await ReplyAsync(null, false, embed.Build()); 
        }
    }
}