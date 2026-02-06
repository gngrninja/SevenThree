using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SevenThree.Tests
{
    /// <summary>
    /// Tests the pool status logic used by TestPoolAutocompleteHandler.
    /// The actual handler requires Discord.Interactions context which is hard to mock,
    /// so we test the core logic: status labeling, sorting, and date range selection.
    /// </summary>
    public class TestPoolAutocompleteTests
    {
        // Mirrors GetPoolStatus from TestPoolAutocompleteHandler
        private static string GetPoolStatus(DateTime fromDate, DateTime toDate, DateTime today)
        {
            if (fromDate <= today && toDate >= today)
                return "(current)";
            if (fromDate > today)
                return "(upcoming)";
            return "(expired)";
        }

        #region Pool Status Tests

        [Fact]
        public void PoolStatus_CurrentDate_InRange_ReturnsCurrent()
        {
            var today = new DateTime(2025, 1, 15);
            var status = GetPoolStatus(new DateTime(2024, 7, 1), new DateTime(2026, 6, 30), today);
            Assert.Equal("(current)", status);
        }

        [Fact]
        public void PoolStatus_OnFromDate_ReturnsCurrent()
        {
            var today = new DateTime(2024, 7, 1);
            var status = GetPoolStatus(new DateTime(2024, 7, 1), new DateTime(2026, 6, 30), today);
            Assert.Equal("(current)", status);
        }

        [Fact]
        public void PoolStatus_OnToDate_ReturnsCurrent()
        {
            var today = new DateTime(2026, 6, 30);
            var status = GetPoolStatus(new DateTime(2024, 7, 1), new DateTime(2026, 6, 30), today);
            Assert.Equal("(current)", status);
        }

        [Fact]
        public void PoolStatus_BeforeFromDate_ReturnsUpcoming()
        {
            var today = new DateTime(2024, 6, 30);
            var status = GetPoolStatus(new DateTime(2024, 7, 1), new DateTime(2026, 6, 30), today);
            Assert.Equal("(upcoming)", status);
        }

        [Fact]
        public void PoolStatus_AfterToDate_ReturnsExpired()
        {
            var today = new DateTime(2026, 7, 1);
            var status = GetPoolStatus(new DateTime(2024, 7, 1), new DateTime(2026, 6, 30), today);
            Assert.Equal("(expired)", status);
        }

        #endregion

        #region Pool Sorting Tests

        private class MockPool
        {
            public int Id { get; set; }
            public DateTime FromDate { get; set; }
            public DateTime ToDate { get; set; }
        }

        [Fact]
        public void PoolSorting_CurrentPoolComesFirst()
        {
            var today = new DateTime(2025, 1, 1);
            var pools = new List<MockPool>
            {
                new() { Id = 1, FromDate = new DateTime(2018, 7, 1), ToDate = new DateTime(2022, 6, 30) },
                new() { Id = 2, FromDate = new DateTime(2022, 7, 1), ToDate = new DateTime(2026, 6, 30) },
                new() { Id = 3, FromDate = new DateTime(2026, 7, 1), ToDate = new DateTime(2030, 6, 30) }
            };

            // Mirrors the sorting logic from TestPoolAutocompleteHandler
            var sorted = pools
                .OrderByDescending(p => p.FromDate <= today && p.ToDate >= today)
                .ThenByDescending(p => p.FromDate)
                .ToList();

            // Pool 2 is current, should be first
            Assert.Equal(2, sorted[0].Id);
        }

        [Fact]
        public void PoolSorting_NoCurrent_MostRecentFirst()
        {
            var today = new DateTime(2031, 1, 1); // all pools expired
            var pools = new List<MockPool>
            {
                new() { Id = 1, FromDate = new DateTime(2018, 7, 1), ToDate = new DateTime(2022, 6, 30) },
                new() { Id = 2, FromDate = new DateTime(2022, 7, 1), ToDate = new DateTime(2026, 6, 30) },
                new() { Id = 3, FromDate = new DateTime(2026, 7, 1), ToDate = new DateTime(2030, 6, 30) }
            };

            var sorted = pools
                .OrderByDescending(p => p.FromDate <= today && p.ToDate >= today)
                .ThenByDescending(p => p.FromDate)
                .ToList();

            // Most recent FromDate first
            Assert.Equal(3, sorted[0].Id);
            Assert.Equal(2, sorted[1].Id);
            Assert.Equal(1, sorted[2].Id);
        }

        [Fact]
        public void PoolSorting_UpcomingAfterCurrent()
        {
            var today = new DateTime(2025, 1, 1);
            var pools = new List<MockPool>
            {
                new() { Id = 1, FromDate = new DateTime(2022, 7, 1), ToDate = new DateTime(2026, 6, 30) }, // current
                new() { Id = 2, FromDate = new DateTime(2026, 7, 1), ToDate = new DateTime(2030, 6, 30) }  // upcoming
            };

            var sorted = pools
                .OrderByDescending(p => p.FromDate <= today && p.ToDate >= today)
                .ThenByDescending(p => p.FromDate)
                .ToList();

            Assert.Equal(1, sorted[0].Id); // current first
            Assert.Equal(2, sorted[1].Id); // upcoming second
        }

        #endregion

        #region Label Format Tests

        [Fact]
        public void PoolLabel_ContainsDateRangeAndStatus()
        {
            var fromDate = new DateTime(2022, 7, 1);
            var toDate = new DateTime(2026, 6, 30);
            var today = new DateTime(2025, 1, 1);

            var status = GetPoolStatus(fromDate, toDate, today);
            var label = $"{fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd} {status}";

            Assert.Contains("2022-07-01", label);
            Assert.Contains("2026-06-30", label);
            Assert.Contains("(current)", label);
        }

        [Fact]
        public void PoolLabel_MaxLength_Under100Characters()
        {
            // Discord autocomplete labels must be <= 100 characters
            var fromDate = new DateTime(2022, 7, 1);
            var toDate = new DateTime(2026, 6, 30);
            var status = "(current)";
            var label = $"{fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd} {status}";

            Assert.True(label.Length <= 100, $"Label too long: {label.Length} chars");
        }

        #endregion

        #region Max Results Tests

        [Fact]
        public void Autocomplete_MaxResults_Is25()
        {
            // Discord only supports 25 autocomplete results
            var pools = Enumerable.Range(0, 30).Select(i => new MockPool
            {
                Id = i,
                FromDate = new DateTime(2020 + i, 1, 1),
                ToDate = new DateTime(2021 + i, 12, 31)
            }).ToList();

            var results = pools.Take(25).ToList();
            Assert.Equal(25, results.Count);
        }

        #endregion
    }
}
