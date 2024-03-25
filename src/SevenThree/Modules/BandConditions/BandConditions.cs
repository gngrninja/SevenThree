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
using System.Net;
using System.IO;
using Microsoft.AspNetCore.Routing.Constraints;

namespace SevenThree.Modules.BandConditions
{
    public class BandConditions
    {
        private readonly IServiceProvider _services;
        private readonly IConfiguration _config;
        private string _hamQslUrl;

        public BandConditions(IServiceProvider services)
        {
            _services = services;
            _config = services.GetRequiredService<IConfiguration>();
            _hamQslUrl = _config["HamQslUrl"];
        }

        public string GetConditionsHamQsl()
        {
            var fileName = "conditions.gif";
            using (WebClient client = new WebClient()) 
            {
                client.DownloadFile(new Uri(_hamQslUrl), fileName);
            }
            return $"{Environment.CurrentDirectory}/{fileName}";
        }
    }
}
