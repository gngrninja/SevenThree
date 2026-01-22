using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

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
            var fileName = "conditions.png";
            var filePath = Path.Combine(Environment.CurrentDirectory, fileName);

            using var client = new HttpClient();
            var response = await client.GetAsync(_hamQslUrl);
            if (response.IsSuccessStatusCode)
            {
                var gifBytes = await response.Content.ReadAsByteArrayAsync();

                // Convert GIF to PNG to work around Discord rendering issues
                using var image = Image.Load(gifBytes);
                await image.SaveAsPngAsync(filePath);
            }

            return filePath;
        }
    }
}
