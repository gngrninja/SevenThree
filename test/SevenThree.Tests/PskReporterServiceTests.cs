using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SevenThree.Models;
using SevenThree.Services;

namespace SevenThree.Tests
{
    public class PskReporterServiceTests : IDisposable
    {
        private readonly PskReporterService _sut;

        public PskReporterServiceTests()
        {
            var mockLogger = new Mock<ILogger<PskReporterService>>();
            _sut = new PskReporterService(mockLogger.Object);
        }

        public void Dispose()
        {
            _sut.Dispose();
        }

        #region FrequencyToBand Tests

        [Theory]
        [InlineData(1_800_000, "160m")]
        [InlineData(1_999_000, "160m")]
        [InlineData(3_500_000, "80m")]
        [InlineData(3_750_000, "80m")]
        [InlineData(5_350_000, "60m")]
        [InlineData(7_000_000, "40m")]
        [InlineData(7_150_000, "40m")]
        [InlineData(10_100_000, "30m")]
        [InlineData(14_000_000, "20m")]
        [InlineData(14_074_000, "20m")]  // FT8 frequency
        [InlineData(18_100_000, "17m")]
        [InlineData(21_000_000, "15m")]
        [InlineData(24_915_000, "12m")]
        [InlineData(28_000_000, "10m")]
        [InlineData(28_074_000, "10m")]  // 10m FT8
        [InlineData(50_313_000, "6m")]   // 6m FT8
        [InlineData(144_174_000, "2m")]  // 2m FT8
        [InlineData(432_000_000, "70cm")]
        public void FrequencyToBand_KnownBands_ReturnsCorrectBand(long freqHz, string expectedBand)
        {
            var result = PskReporterService.FrequencyToBand(freqHz);
            Assert.Equal(expectedBand, result);
        }

        [Theory]
        [InlineData(500_000)]       // below 160m
        [InlineData(5_000_000)]     // between 80m and 60m
        [InlineData(900_000_000)]   // above 70cm
        public void FrequencyToBand_UnknownBands_ReturnsMhzString(long freqHz)
        {
            var result = PskReporterService.FrequencyToBand(freqHz);
            Assert.Contains("MHz", result);
        }

        [Fact]
        public void FrequencyToBand_ZeroHz_ReturnsMhzString()
        {
            var result = PskReporterService.FrequencyToBand(0);
            Assert.Contains("MHz", result);
        }

        #endregion

        #region GetBandFrequencyRange Tests

        [Theory]
        [InlineData("160m", 1_800_000, 2_000_000)]
        [InlineData("80m", 3_500_000, 4_000_000)]
        [InlineData("40m", 7_000_000, 7_300_000)]
        [InlineData("20m", 14_000_000, 14_350_000)]
        [InlineData("15m", 21_000_000, 21_450_000)]
        [InlineData("10m", 28_000_000, 29_700_000)]
        [InlineData("6m", 50_000_000, 54_000_000)]
        [InlineData("2m", 144_000_000, 148_000_000)]
        [InlineData("70cm", 420_000_000, 450_000_000)]
        public void GetBandFrequencyRange_KnownBands_ReturnsCorrectRange(string band, long expectedLow, long expectedHigh)
        {
            var result = PskReporterService.GetBandFrequencyRange(band);
            Assert.NotNull(result);
            Assert.Equal(expectedLow, result.Value.Low);
            Assert.Equal(expectedHigh, result.Value.High);
        }

        [Theory]
        [InlineData("unknown")]
        [InlineData("")]
        [InlineData("5m")]
        [InlineData("1m")]
        public void GetBandFrequencyRange_UnknownBands_ReturnsNull(string band)
        {
            var result = PskReporterService.GetBandFrequencyRange(band);
            Assert.Null(result);
        }

        [Fact]
        public void GetBandFrequencyRange_CaseInsensitive()
        {
            var lower = PskReporterService.GetBandFrequencyRange("20m");
            var upper = PskReporterService.GetBandFrequencyRange("20M");
            Assert.Equal(lower, upper);
        }

        [Fact]
        public void FrequencyToBand_RoundTrips_WithGetBandFrequencyRange()
        {
            // A frequency within "20m" range should return "20m" from FrequencyToBand
            var range = PskReporterService.GetBandFrequencyRange("20m");
            Assert.NotNull(range);

            var midFreq = (range.Value.Low + range.Value.High) / 2;
            var band = PskReporterService.FrequencyToBand(midFreq);
            Assert.Equal("20m", band);
        }

        #endregion

        #region GridToLatLon Tests

        [Fact]
        public void GridToLatLon_ValidFourCharGrid_ReturnsCoordinates()
        {
            // FN31 is roughly New York City area
            var result = PskReporterService.GridToLatLon("FN31");
            Assert.NotNull(result);
            // FN31 center: lon = (5*20-180) + (3*2) + 1 = -80+6+1 = -73, lat = (13*10-90) + (1*1) + 0.5 = 40+1+0.5 = 41.5
            Assert.InRange(result.Value.Lat, 40, 43);
            Assert.InRange(result.Value.Lon, -74, -72);
        }

        [Fact]
        public void GridToLatLon_ValidSixCharGrid_ReturnsMorePrecise()
        {
            // FN31pr is a subsquare in NE US
            var result = PskReporterService.GridToLatLon("FN31pr");
            Assert.NotNull(result);
            Assert.InRange(result.Value.Lat, 40, 43);
            Assert.InRange(result.Value.Lon, -74, -71);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("AB")]    // too short
        [InlineData("A")]     // way too short
        public void GridToLatLon_InvalidGrid_ReturnsNull(string grid)
        {
            var result = PskReporterService.GridToLatLon(grid);
            Assert.Null(result);
        }

        [Fact]
        public void GridToLatLon_CaseInsensitive()
        {
            var upper = PskReporterService.GridToLatLon("FN31");
            var lower = PskReporterService.GridToLatLon("fn31");
            Assert.NotNull(upper);
            Assert.NotNull(lower);
            Assert.Equal(upper.Value.Lat, lower.Value.Lat, 0.01);
            Assert.Equal(upper.Value.Lon, lower.Value.Lon, 0.01);
        }

        [Fact]
        public void GridToLatLon_JN58_CorrectApproximation()
        {
            // JN58: J=9, N=13, 5, 8 -> lon=(9*20-180)+(5*2)+1=11, lat=(13*10-90)+(8*1)+0.5=48.5
            var result = PskReporterService.GridToLatLon("JN58");
            Assert.NotNull(result);
            Assert.InRange(result.Value.Lat, 47, 50);
            Assert.InRange(result.Value.Lon, 10, 13);
        }

        #endregion

        #region CalculateGridDistance Tests

        [Fact]
        public void CalculateGridDistance_SameGrid_ReturnsNearZero()
        {
            var distance = PskReporterService.CalculateGridDistance("FN31", "FN31");
            Assert.NotNull(distance);
            Assert.InRange(distance.Value, 0, 1); // same grid center = 0 km
        }

        [Fact]
        public void CalculateGridDistance_KnownDistance_IsApproxCorrect()
        {
            // FN31 (NYC area) to JN58 (Switzerland area) - ~6300 km
            var distance = PskReporterService.CalculateGridDistance("FN31", "JN58");
            Assert.NotNull(distance);
            Assert.InRange(distance.Value, 6000, 6700);
        }

        [Theory]
        [InlineData(null, "FN31")]
        [InlineData("FN31", null)]
        [InlineData("", "FN31")]
        [InlineData("FN31", "")]
        [InlineData("AB", "FN31")]
        [InlineData("FN31", "X")]
        public void CalculateGridDistance_InvalidInput_ReturnsNull(string grid1, string grid2)
        {
            var distance = PskReporterService.CalculateGridDistance(grid1, grid2);
            Assert.Null(distance);
        }

        [Fact]
        public void CalculateGridDistance_Antipodal_ReturnsLargeDistance()
        {
            // Opposite sides of the earth should be ~20000 km (half circumference)
            // AA00 is at (-90, -180+1) roughly, RR99 is roughly (89.5, 177)
            var distance = PskReporterService.CalculateGridDistance("AA00", "RR99");
            Assert.NotNull(distance);
            Assert.True(distance.Value > 15000, "Antipodal grids should be very far apart");
        }

        #endregion

        #region CacheSpots / GetCachedSpots Tests

        [Fact]
        public void CacheSpots_ReturnsNonEmptySessionId()
        {
            var result = new PskReporterService.CachedSpotResult
            {
                Spots = new List<SpotInfo>(),
                Title = "Test",
                QueryType = "spots"
            };

            var sessionId = _sut.CacheSpots(result);
            Assert.NotNull(sessionId);
            Assert.Equal(8, sessionId.Length);
        }

        [Fact]
        public void GetCachedSpots_ExistingSession_ReturnsResult()
        {
            var result = new PskReporterService.CachedSpotResult
            {
                Spots = new List<SpotInfo> { new() { SenderCallsign = "W1AW" } },
                Title = "Test",
                QueryType = "spots"
            };

            var sessionId = _sut.CacheSpots(result);
            var cached = _sut.GetCachedSpots(sessionId);

            Assert.NotNull(cached);
            Assert.Single(cached.Spots);
            Assert.Equal("W1AW", cached.Spots[0].SenderCallsign);
        }

        [Fact]
        public void GetCachedSpots_NonexistentSession_ReturnsNull()
        {
            var result = _sut.GetCachedSpots("nonexistent");
            Assert.Null(result);
        }

        [Fact]
        public void CacheSpots_MultipleSessions_AreIndependent()
        {
            var r1 = new PskReporterService.CachedSpotResult { Spots = new List<SpotInfo>(), Title = "A" };
            var r2 = new PskReporterService.CachedSpotResult { Spots = new List<SpotInfo>(), Title = "B" };

            var id1 = _sut.CacheSpots(r1);
            var id2 = _sut.CacheSpots(r2);

            Assert.NotEqual(id1, id2);
            Assert.Equal("A", _sut.GetCachedSpots(id1).Title);
            Assert.Equal("B", _sut.GetCachedSpots(id2).Title);
        }

        #endregion

        #region GetPage Tests

        [Fact]
        public void GetPage_FirstPage_Returns10Items()
        {
            var spots = Enumerable.Range(0, 25).Select(i => new SpotInfo
            {
                SenderCallsign = $"CALL{i}"
            }).ToList();

            var result = new PskReporterService.CachedSpotResult { Spots = spots };
            var page = _sut.GetPage(result, 0);

            Assert.Equal(PskReporterService.PAGE_SIZE, page.Count);
            Assert.Equal("CALL0", page[0].SenderCallsign);
        }

        [Fact]
        public void GetPage_SecondPage_SkipsFirstPageItems()
        {
            var spots = Enumerable.Range(0, 25).Select(i => new SpotInfo
            {
                SenderCallsign = $"CALL{i}"
            }).ToList();

            var result = new PskReporterService.CachedSpotResult { Spots = spots };
            var page = _sut.GetPage(result, 1);

            Assert.Equal(PskReporterService.PAGE_SIZE, page.Count);
            Assert.Equal("CALL10", page[0].SenderCallsign);
        }

        [Fact]
        public void GetPage_LastPartialPage_ReturnsRemainingItems()
        {
            var spots = Enumerable.Range(0, 15).Select(i => new SpotInfo
            {
                SenderCallsign = $"CALL{i}"
            }).ToList();

            var result = new PskReporterService.CachedSpotResult { Spots = spots };
            var page = _sut.GetPage(result, 1);

            Assert.Equal(5, page.Count);
            Assert.Equal("CALL10", page[0].SenderCallsign);
        }

        [Fact]
        public void GetPage_EmptySpots_ReturnsEmpty()
        {
            var result = new PskReporterService.CachedSpotResult { Spots = new List<SpotInfo>() };
            var page = _sut.GetPage(result, 0);
            Assert.Empty(page);
        }

        [Fact]
        public void GetPage_BeyondLastPage_ReturnsEmpty()
        {
            var spots = Enumerable.Range(0, 5).Select(i => new SpotInfo()).ToList();
            var result = new PskReporterService.CachedSpotResult { Spots = spots };
            var page = _sut.GetPage(result, 5);
            Assert.Empty(page);
        }

        #endregion

        #region TotalPages Tests

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(10, 1)]
        [InlineData(11, 2)]
        [InlineData(20, 2)]
        [InlineData(25, 3)]
        [InlineData(100, 10)]
        public void TotalPages_CalculatesCorrectly(int spotCount, int expectedPages)
        {
            var result = new PskReporterService.CachedSpotResult
            {
                Spots = Enumerable.Range(0, spotCount).Select(_ => new SpotInfo()).ToList()
            };
            Assert.Equal(expectedPages, result.TotalPages);
        }

        #endregion

        #region IsOnCooldown Tests

        [Fact]
        public void IsOnCooldown_NewUser_NotOnCooldown()
        {
            var onCooldown = _sut.IsOnCooldown(99999UL, out var remaining);
            Assert.False(onCooldown);
            Assert.Equal(TimeSpan.Zero, remaining);
        }

        #endregion

        #region ConvertToSpotInfo Tests

        [Fact]
        public void ConvertToSpotInfo_NullReports_ReturnsEmpty()
        {
            var result = _sut.ConvertToSpotInfo(null);
            Assert.Empty(result);
        }

        [Fact]
        public void ConvertToSpotInfo_NullReportList_ReturnsEmpty()
        {
            var reports = new PskReporterXml.ReceptionReports { ReceptionReportList = null };
            var result = _sut.ConvertToSpotInfo(reports);
            Assert.Empty(result);
        }

        [Fact]
        public void ConvertToSpotInfo_EmptyReportList_ReturnsEmpty()
        {
            var reports = new PskReporterXml.ReceptionReports();
            var result = _sut.ConvertToSpotInfo(reports);
            Assert.Empty(result);
        }

        [Fact]
        public void ConvertToSpotInfo_ValidReport_MapsFields()
        {
            var reports = new PskReporterXml.ReceptionReports
            {
                ReceptionReportList = new List<PskReporterXml.ReceptionReport>
                {
                    new()
                    {
                        SenderCallsign = "W1AW",
                        SenderLocator = "FN31",
                        ReceiverCallsign = "K1ABC",
                        ReceiverLocator = "FN42",
                        ReceiverDXCC = "United States",
                        Frequency = 14_074_000,
                        Mode = "FT8",
                        SNRString = "-10",
                        FlowStartSeconds = 1700000000
                    }
                }
            };

            var result = _sut.ConvertToSpotInfo(reports);

            Assert.Single(result);
            var spot = result[0];
            Assert.Equal("W1AW", spot.SenderCallsign);
            Assert.Equal("K1ABC", spot.ReceiverCallsign);
            Assert.Equal("20m", spot.Band);
            Assert.Equal("FT8", spot.Mode);
            Assert.Equal(-10, spot.SNR);
            Assert.Equal(14_074_000, spot.FrequencyHz);
        }

        [Fact]
        public void ConvertToSpotInfo_WithLocators_CalculatesDistance()
        {
            var reports = new PskReporterXml.ReceptionReports
            {
                ReceptionReportList = new List<PskReporterXml.ReceptionReport>
                {
                    new()
                    {
                        SenderCallsign = "W1AW",
                        SenderLocator = "FN31",
                        ReceiverCallsign = "DL1ABC",
                        ReceiverLocator = "JN58",
                        Frequency = 14_074_000,
                        FlowStartSeconds = 1700000000
                    }
                }
            };

            var result = _sut.ConvertToSpotInfo(reports);
            Assert.Single(result);
            Assert.NotNull(result[0].DistanceKm);
            Assert.NotNull(result[0].DistanceMi);
            Assert.True(result[0].DistanceKm > 0);
            // Mile conversion is correct
            Assert.Equal(result[0].DistanceKm.Value * 0.621371, result[0].DistanceMi.Value, 0.01);
        }

        [Fact]
        public void ConvertToSpotInfo_WithoutLocators_NoDistance()
        {
            var reports = new PskReporterXml.ReceptionReports
            {
                ReceptionReportList = new List<PskReporterXml.ReceptionReport>
                {
                    new()
                    {
                        SenderCallsign = "W1AW",
                        ReceiverCallsign = "K1ABC",
                        Frequency = 7_074_000,
                        FlowStartSeconds = 1700000000
                    }
                }
            };

            var result = _sut.ConvertToSpotInfo(reports);
            Assert.Single(result);
            Assert.Null(result[0].DistanceKm);
            Assert.Null(result[0].DistanceMi);
        }

        [Fact]
        public void ConvertToSpotInfo_OrderedByTimestampDescending()
        {
            var reports = new PskReporterXml.ReceptionReports
            {
                ReceptionReportList = new List<PskReporterXml.ReceptionReport>
                {
                    new() { SenderCallsign = "FIRST", Frequency = 14_074_000, FlowStartSeconds = 1700000000 },
                    new() { SenderCallsign = "LAST", Frequency = 14_074_000, FlowStartSeconds = 1700000100 },
                    new() { SenderCallsign = "MIDDLE", Frequency = 14_074_000, FlowStartSeconds = 1700000050 }
                }
            };

            var result = _sut.ConvertToSpotInfo(reports);
            Assert.Equal(3, result.Count);
            Assert.Equal("LAST", result[0].SenderCallsign);
            Assert.Equal("MIDDLE", result[1].SenderCallsign);
            Assert.Equal("FIRST", result[2].SenderCallsign);
        }

        [Fact]
        public void ConvertToSpotInfo_NullSNR_IsNull()
        {
            var reports = new PskReporterXml.ReceptionReports
            {
                ReceptionReportList = new List<PskReporterXml.ReceptionReport>
                {
                    new() { SenderCallsign = "W1AW", Frequency = 14_074_000, SNRString = null, FlowStartSeconds = 1700000000 }
                }
            };

            var result = _sut.ConvertToSpotInfo(reports);
            Assert.Null(result[0].SNR);
        }

        #endregion

        #region BuildPropagationStats Tests

        [Fact]
        public void BuildPropagationStats_EmptySpots_ReturnsZeroCounts()
        {
            var stats = _sut.BuildPropagationStats("W1AW", new List<SpotInfo>(), 60);

            Assert.Equal("W1AW", stats.Callsign);
            Assert.Equal(0, stats.TotalSpots);
            Assert.Equal(0, stats.UniqueReceivers);
            Assert.Equal(0, stats.UniqueCountries);
            Assert.Equal(60, stats.TimeWindowMinutes);
            Assert.Empty(stats.SpotsByBand);
            Assert.Empty(stats.SpotsByMode);
            Assert.Null(stats.FurthestSpot);
        }

        [Fact]
        public void BuildPropagationStats_CountsCorrectly()
        {
            var spots = new List<SpotInfo>
            {
                new() { ReceiverCallsign = "K1ABC", ReceiverDXCC = "United States", Band = "20m", Mode = "FT8" },
                new() { ReceiverCallsign = "K1ABC", ReceiverDXCC = "United States", Band = "20m", Mode = "FT8" },
                new() { ReceiverCallsign = "DL1ABC", ReceiverDXCC = "Germany", Band = "40m", Mode = "CW" },
                new() { ReceiverCallsign = "JA1ABC", ReceiverDXCC = "Japan", Band = "20m", Mode = "FT8" }
            };

            var stats = _sut.BuildPropagationStats("W1AW", spots, 60);

            Assert.Equal(4, stats.TotalSpots);
            Assert.Equal(3, stats.UniqueReceivers);
            Assert.Equal(3, stats.UniqueCountries);

            Assert.Equal(2, stats.SpotsByBand.Count);
            Assert.Equal(3, stats.SpotsByBand["20m"]);
            Assert.Equal(1, stats.SpotsByBand["40m"]);

            Assert.Equal(2, stats.SpotsByMode.Count);
            Assert.Equal(3, stats.SpotsByMode["FT8"]);
            Assert.Equal(1, stats.SpotsByMode["CW"]);
        }

        [Fact]
        public void BuildPropagationStats_FindsFurthestSpot()
        {
            var spots = new List<SpotInfo>
            {
                new() { ReceiverCallsign = "NEAR", DistanceKm = 100 },
                new() { ReceiverCallsign = "FAR", DistanceKm = 10000 },
                new() { ReceiverCallsign = "MID", DistanceKm = 5000 }
            };

            var stats = _sut.BuildPropagationStats("W1AW", spots, 60);
            Assert.NotNull(stats.FurthestSpot);
            Assert.Equal("FAR", stats.FurthestSpot.ReceiverCallsign);
        }

        [Fact]
        public void BuildPropagationStats_NoDistances_FurthestIsNull()
        {
            var spots = new List<SpotInfo>
            {
                new() { ReceiverCallsign = "K1ABC" }
            };

            var stats = _sut.BuildPropagationStats("W1AW", spots, 60);
            Assert.Null(stats.FurthestSpot);
        }

        #endregion

        #region PAGE_SIZE Constant Test

        [Fact]
        public void PAGE_SIZE_Is10()
        {
            Assert.Equal(10, PskReporterService.PAGE_SIZE);
        }

        [Fact]
        public void BUTTON_PREFIX_IsPsk()
        {
            Assert.Equal("psk", PskReporterService.BUTTON_PREFIX);
        }

        #endregion
    }
}
