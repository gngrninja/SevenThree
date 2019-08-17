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
using Microsoft.EntityFrameworkCore;
using SevenThree.Database;
using SevenThree.Models;
using SevenThree.Services;
using System.IO;

namespace SevenThree.Modules
{
    public class HamTestChannelCommands
    {

        private readonly ILogger _logger;
        private readonly SevenThreeContext _db;        
        private readonly HamTestService _hamTestService;

        public HamTestChannelCommands(IServiceProvider services)
        {
            _logger          = services.GetRequiredService<ILogger<HamTestChannelCommands>>();
            _db              = services.GetRequiredService<SevenThreeContext>();
            _hamTestService  = services.GetRequiredService<HamTestService>();
        }



    }
}