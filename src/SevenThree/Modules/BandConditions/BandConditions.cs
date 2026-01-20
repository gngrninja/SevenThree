using Discord;
using Discord.Net;
using Discord.WebSocket;
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
using System.Net.Http;

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

        public async Task<string> GetConditionsHamQsl()
        {
            var fileName = "conditions.gif";       
            var filePath = Path.Combine(Environment.CurrentDirectory, fileName);
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(_hamQslUrl);
                if (response.IsSuccessStatusCode)
                {
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fileStream);
                    }
                }
            }
            return filePath;            
        }
    }
}
