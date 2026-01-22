using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using SevenThree.Models;

namespace SevenThree.Services
{
    /// <summary>
    /// Service for interacting with the PSKReporter API
    /// </summary>
    public class PskReporterService : IDisposable
    {
        private readonly ILogger<PskReporterService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ConcurrentDictionary<ulong, DateTime> _userCooldowns = new();
        private readonly CancellationTokenSource _cleanupCts = new();

        // Cache for paginated results
        private readonly ConcurrentDictionary<string, CachedSpotResult> _spotCache = new();
        private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(10);

        private const string BASE_URL = "https://retrieve.pskreporter.info/query";
        private const string APP_CONTACT = "seventhree-discord-bot";
        public const int PAGE_SIZE = 10;
        public const string BUTTON_PREFIX = "psk";

        // Rate limiting: 5 minutes recommended by PSKReporter
        private static readonly TimeSpan UserCooldown = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan GlobalCooldown = TimeSpan.FromSeconds(5);
        private DateTime _lastGlobalRequest = DateTime.MinValue;
        private readonly SemaphoreSlim _globalLock = new(1, 1);

        public PskReporterService(ILogger<PskReporterService> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SevenThree-Discord-Bot/1.0");

            // Cleanup expired cache entries periodically with cancellation support
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!_cleanupCts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5), _cleanupCts.Token);
                        CleanupCache();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected on shutdown
                }
            });
        }

        public void Dispose()
        {
            _cleanupCts.Cancel();
            _cleanupCts.Dispose();
            _httpClient.Dispose();
            _globalLock.Dispose();
        }

        /// <summary>
        /// Cached spot result with metadata
        /// </summary>
        public class CachedSpotResult
        {
            public List<SpotInfo> Spots { get; set; }
            public string Title { get; set; }
            public string QueryType { get; set; } // "spots", "hearing", "band"
            public string Callsign { get; set; }
            public string Band { get; set; }
            public string Mode { get; set; }
            public int Minutes { get; set; }
            public int ActiveReceivers { get; set; }
            public DateTime CachedAt { get; set; }
            public ulong UserId { get; set; }

            public int TotalPages => (int)Math.Ceiling(Spots.Count / (double)PAGE_SIZE);
        }

        /// <summary>
        /// Store spots in cache and return session ID
        /// </summary>
        public string CacheSpots(CachedSpotResult result)
        {
            var sessionId = Guid.NewGuid().ToString("N")[..8];
            result.CachedAt = DateTime.UtcNow;
            _spotCache[sessionId] = result;
            return sessionId;
        }

        /// <summary>
        /// Get cached spots by session ID
        /// </summary>
        public CachedSpotResult GetCachedSpots(string sessionId)
        {
            if (_spotCache.TryGetValue(sessionId, out var result))
            {
                if (DateTime.UtcNow - result.CachedAt < CacheExpiration)
                {
                    return result;
                }
                _spotCache.TryRemove(sessionId, out _);
            }
            return null;
        }

        /// <summary>
        /// Get a page of spots from cached results
        /// </summary>
        public List<SpotInfo> GetPage(CachedSpotResult result, int page)
        {
            return result.Spots
                .Skip(page * PAGE_SIZE)
                .Take(PAGE_SIZE)
                .ToList();
        }

        private void CleanupCache()
        {
            var expired = _spotCache
                .Where(kv => DateTime.UtcNow - kv.Value.CachedAt > CacheExpiration)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in expired)
            {
                _spotCache.TryRemove(key, out _);
            }

            if (expired.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} expired PSK cache entries", expired.Count);
            }
        }

        /// <summary>
        /// Check if user is on cooldown
        /// </summary>
        public bool IsOnCooldown(ulong userId, out TimeSpan remaining)
        {
            if (_userCooldowns.TryGetValue(userId, out var lastRequest))
            {
                var elapsed = DateTime.UtcNow - lastRequest;
                if (elapsed < UserCooldown)
                {
                    remaining = UserCooldown - elapsed;
                    return true;
                }
            }
            remaining = TimeSpan.Zero;
            return false;
        }

        /// <summary>
        /// Get reception reports for a sender callsign (who's hearing them)
        /// </summary>
        public async Task<PskReporterXml.ReceptionReports> GetSenderSpotsAsync(
            string callsign,
            ulong userId,
            int limitMinutes = 60,
            int maxReports = 100)
        {
            _userCooldowns[userId] = DateTime.UtcNow;

            var flowStartSeconds = -limitMinutes * 60;
            var url = $"{BASE_URL}?senderCallsign={Uri.EscapeDataString(callsign)}" +
                      $"&flowStartSeconds={flowStartSeconds}" +
                      $"&rronly=1" +
                      $"&rptlimit={maxReports}" +
                      $"&appcontact={APP_CONTACT}";

            return await FetchAndParseAsync(url);
        }

        /// <summary>
        /// Get reception reports for a receiver callsign (what they're hearing)
        /// </summary>
        public async Task<PskReporterXml.ReceptionReports> GetReceiverSpotsAsync(
            string callsign,
            ulong userId,
            int limitMinutes = 60,
            int maxReports = 100)
        {
            _userCooldowns[userId] = DateTime.UtcNow;

            var flowStartSeconds = -limitMinutes * 60;
            var url = $"{BASE_URL}?receiverCallsign={Uri.EscapeDataString(callsign)}" +
                      $"&flowStartSeconds={flowStartSeconds}" +
                      $"&rronly=1" +
                      $"&rptlimit={maxReports}" +
                      $"&appcontact={APP_CONTACT}";

            return await FetchAndParseAsync(url);
        }

        /// <summary>
        /// Get band activity (active receivers on a frequency range)
        /// </summary>
        public async Task<PskReporterXml.ReceptionReports> GetBandActivityAsync(
            ulong userId,
            long freqLow,
            long freqHigh,
            string mode = null,
            int limitMinutes = 15,
            int maxReports = 500)
        {
            _userCooldowns[userId] = DateTime.UtcNow;

            var flowStartSeconds = -limitMinutes * 60;
            var url = $"{BASE_URL}?frange={freqLow}-{freqHigh}" +
                      $"&flowStartSeconds={flowStartSeconds}" +
                      $"&rptlimit={maxReports}" +
                      $"&appcontact={APP_CONTACT}";

            if (!string.IsNullOrEmpty(mode))
            {
                url += $"&mode={Uri.EscapeDataString(mode)}";
            }

            return await FetchAndParseAsync(url);
        }

        private async Task<PskReporterXml.ReceptionReports> FetchAndParseAsync(string url)
        {
            // Global rate limiting with async wait
            await _globalLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var elapsed = DateTime.UtcNow - _lastGlobalRequest;
                if (elapsed < GlobalCooldown)
                {
                    var waitTime = GlobalCooldown - elapsed;
                    await Task.Delay(waitTime).ConfigureAwait(false);
                }
                _lastGlobalRequest = DateTime.UtcNow;
            }
            finally
            {
                _globalLock.Release();
            }

            try
            {
                _logger.LogDebug("Fetching PSKReporter data: {Url}", url);
                var response = await _httpClient.GetStringAsync(url);

                var serializer = new XmlSerializer(typeof(PskReporterXml.ReceptionReports));
                using var reader = new StringReader(response);
                var result = (PskReporterXml.ReceptionReports)serializer.Deserialize(reader);

                _logger.LogDebug("Received {Count} reception reports", result?.ReceptionReportList?.Count ?? 0);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching PSKReporter data");
                return new PskReporterXml.ReceptionReports();
            }
        }

        /// <summary>
        /// Convert raw reports to SpotInfo with calculated distances
        /// </summary>
        public List<SpotInfo> ConvertToSpotInfo(PskReporterXml.ReceptionReports reports)
        {
            if (reports?.ReceptionReportList == null)
                return new List<SpotInfo>();

            return reports.ReceptionReportList
                .Where(r => r != null)
                .Select(r =>
                {
                    var spot = new SpotInfo
                    {
                        ReceiverCallsign = r.ReceiverCallsign,
                        ReceiverLocator = r.ReceiverLocator,
                        ReceiverDXCC = r.ReceiverDXCC,
                        SenderCallsign = r.SenderCallsign,
                        SenderLocator = r.SenderLocator,
                        FrequencyHz = r.Frequency,
                        Band = FrequencyToBand(r.Frequency),
                        Mode = r.Mode,
                        SNR = r.SNR,
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds(r.FlowStartSeconds).UtcDateTime
                    };

                    // Calculate distance if both locators are available
                    if (!string.IsNullOrEmpty(r.SenderLocator) && !string.IsNullOrEmpty(r.ReceiverLocator))
                    {
                        var distance = CalculateGridDistance(r.SenderLocator, r.ReceiverLocator);
                        if (distance.HasValue)
                        {
                            spot.DistanceKm = distance.Value;
                            spot.DistanceMi = distance.Value * 0.621371;
                        }
                    }

                    return spot;
                })
                .OrderByDescending(s => s.Timestamp)
                .ToList();
        }

        /// <summary>
        /// Build propagation statistics from spots
        /// </summary>
        public PropagationStats BuildPropagationStats(string callsign, List<SpotInfo> spots, int timeWindowMinutes)
        {
            var stats = new PropagationStats
            {
                Callsign = callsign,
                TotalSpots = spots.Count,
                UniqueReceivers = spots.Select(s => s.ReceiverCallsign).Distinct().Count(),
                UniqueCountries = spots.Where(s => !string.IsNullOrEmpty(s.ReceiverDXCC))
                    .Select(s => s.ReceiverDXCC).Distinct().Count(),
                QueryTime = DateTime.UtcNow,
                TimeWindowMinutes = timeWindowMinutes
            };

            // Group by band
            stats.SpotsByBand = spots
                .Where(s => !string.IsNullOrEmpty(s.Band))
                .GroupBy(s => s.Band)
                .ToDictionary(g => g.Key, g => g.Count());

            // Group by country
            stats.SpotsByCountry = spots
                .Where(s => !string.IsNullOrEmpty(s.ReceiverDXCC))
                .GroupBy(s => s.ReceiverDXCC)
                .ToDictionary(g => g.Key, g => g.Count());

            // Group by mode
            stats.SpotsByMode = spots
                .Where(s => !string.IsNullOrEmpty(s.Mode))
                .GroupBy(s => s.Mode)
                .ToDictionary(g => g.Key, g => g.Count());

            // Find furthest spot
            stats.FurthestSpot = spots
                .Where(s => s.DistanceKm.HasValue)
                .OrderByDescending(s => s.DistanceKm)
                .FirstOrDefault();

            return stats;
        }

        /// <summary>
        /// Convert frequency in Hz to amateur band name
        /// </summary>
        public static string FrequencyToBand(long frequencyHz)
        {
            var freqMhz = frequencyHz / 1_000_000.0;

            return freqMhz switch
            {
                >= 1.8 and < 2.0 => "160m",
                >= 3.5 and < 4.0 => "80m",
                >= 5.3 and < 5.4 => "60m",
                >= 7.0 and < 7.3 => "40m",
                >= 10.1 and < 10.15 => "30m",
                >= 14.0 and < 14.35 => "20m",
                >= 18.068 and < 18.168 => "17m",
                >= 21.0 and < 21.45 => "15m",
                >= 24.89 and < 24.99 => "12m",
                >= 28.0 and < 29.7 => "10m",
                >= 50.0 and < 54.0 => "6m",
                >= 144.0 and < 148.0 => "2m",
                >= 420.0 and < 450.0 => "70cm",
                _ => $"{freqMhz:F3} MHz"
            };
        }

        /// <summary>
        /// Get band frequency range in Hz
        /// </summary>
        public static (long Low, long High)? GetBandFrequencyRange(string band)
        {
            return band.ToLowerInvariant() switch
            {
                "160m" => (1_800_000, 2_000_000),
                "80m" => (3_500_000, 4_000_000),
                "60m" => (5_300_000, 5_400_000),
                "40m" => (7_000_000, 7_300_000),
                "30m" => (10_100_000, 10_150_000),
                "20m" => (14_000_000, 14_350_000),
                "17m" => (18_068_000, 18_168_000),
                "15m" => (21_000_000, 21_450_000),
                "12m" => (24_890_000, 24_990_000),
                "10m" => (28_000_000, 29_700_000),
                "6m" => (50_000_000, 54_000_000),
                "2m" => (144_000_000, 148_000_000),
                "70cm" => (420_000_000, 450_000_000),
                _ => null
            };
        }

        /// <summary>
        /// Calculate great circle distance between two Maidenhead grid squares
        /// </summary>
        public static double? CalculateGridDistance(string grid1, string grid2)
        {
            var coord1 = GridToLatLon(grid1);
            var coord2 = GridToLatLon(grid2);

            if (!coord1.HasValue || !coord2.HasValue)
                return null;

            return HaversineDistance(coord1.Value.Lat, coord1.Value.Lon, coord2.Value.Lat, coord2.Value.Lon);
        }

        /// <summary>
        /// Convert Maidenhead grid locator to lat/lon
        /// </summary>
        public static (double Lat, double Lon)? GridToLatLon(string grid)
        {
            if (string.IsNullOrEmpty(grid) || grid.Length < 4)
                return null;

            try
            {
                grid = grid.ToUpperInvariant();

                // Field (18x18 zones, 20° lon x 10° lat each)
                double lon = (grid[0] - 'A') * 20 - 180;
                double lat = (grid[1] - 'A') * 10 - 90;

                // Square (10x10 subdivisions, 2° lon x 1° lat each)
                lon += (grid[2] - '0') * 2;
                lat += grid[3] - '0';

                // Subsquare if available (24x24 subdivisions)
                if (grid.Length >= 6)
                {
                    lon += (grid[4] - 'A') * (2.0 / 24) + (1.0 / 24);
                    lat += (grid[5] - 'A') * (1.0 / 24) + (0.5 / 24);
                }
                else
                {
                    // Center of square
                    lon += 1;
                    lat += 0.5;
                }

                return (lat, lon);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Calculate distance using Haversine formula
        /// </summary>
        private static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Earth's radius in km

            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180;
    }
}
