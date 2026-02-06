using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using SevenThree.Models;
using SevenThree.Services;

namespace SevenThree.Tests
{
    public class PskButtonHandlerTests
    {
        #region PSK Button ID Parsing Tests

        [Theory]
        [InlineData("psk:next:abc12345:0", "psk", "next", "abc12345", "0")]
        [InlineData("psk:prev:abc12345:1", "psk", "prev", "abc12345", "1")]
        [InlineData("psk:page:abc12345", null, null, null, null)] // page button has no page number
        public void PskButtonId_ParsesCorrectly(string buttonId, string expectedPrefix, string expectedAction, string expectedSession, string expectedPage)
        {
            var parts = buttonId.Split(':');

            if (expectedPrefix != null)
            {
                Assert.True(parts.Length >= 4);
                Assert.Equal(expectedPrefix, parts[0]);
                Assert.Equal(expectedAction, parts[1]);
                Assert.Equal(expectedSession, parts[2]);
                Assert.Equal(expectedPage, parts[3]);
            }
            else
            {
                // page button has 3 parts
                Assert.Equal(3, parts.Length);
            }
        }

        [Theory]
        [InlineData("psk:next:abc12345:0", true)]
        [InlineData("psk:prev:abc12345:1", true)]
        [InlineData("psk:page:abc12345", false)]   // page has no page number, 3 parts
        [InlineData("psk:abc12345", false)]         // too few
        [InlineData("psk", false)]
        public void PskButtonId_ValidatesMinimumPartsForNavigation(string buttonId, bool hasNavData)
        {
            var parts = buttonId.Split(':');
            Assert.Equal(hasNavData, parts.Length >= 4);
        }

        #endregion

        #region PSK Button Routing Tests

        [Fact]
        public void PskButton_MatchesPskPrefix()
        {
            var buttonId = $"{PskReporterService.BUTTON_PREFIX}:next:abc12345:0";
            Assert.StartsWith($"{PskReporterService.BUTTON_PREFIX}:", buttonId);
        }

        [Fact]
        public void PskButton_DoesNotMatchOtherPrefixes()
        {
            var buttonId = $"{PskReporterService.BUTTON_PREFIX}:next:abc12345:0";

            Assert.False(buttonId.StartsWith("quiz:"));
            Assert.False(buttonId.StartsWith("quizstop:"));
            Assert.False(buttonId.StartsWith("study:"));
            Assert.False(buttonId.StartsWith("studyretry:"));
        }

        #endregion

        #region PSK Pagination Logic Tests

        [Theory]
        [InlineData("prev", 2, 1, 5)]   // page 2, prev -> 1
        [InlineData("prev", 1, 0, 5)]   // page 1, prev -> 0
        [InlineData("prev", 0, 0, 5)]   // page 0, prev -> stays 0
        [InlineData("next", 0, 1, 5)]   // page 0, next -> 1
        [InlineData("next", 3, 4, 5)]   // page 3, next -> 4 (last page)
        [InlineData("next", 4, 4, 5)]   // page 4 (last), next -> stays 4
        public void PaginationLogic_CalculatesCorrectNewPage(string action, int currentPage, int expectedPage, int totalPages)
        {
            // Mirrors the logic in PskButtonHandler
            var newPage = action switch
            {
                "prev" => Math.Max(0, currentPage - 1),
                "next" => Math.Min(totalPages - 1, currentPage + 1),
                _ => currentPage
            };

            Assert.Equal(expectedPage, newPage);
        }

        [Theory]
        [InlineData("page", 2, 5)]  // disabled page button - stays same
        [InlineData("unknown", 2, 5)]
        public void PaginationLogic_UnknownAction_StaysSamePage(string action, int currentPage, int totalPages)
        {
            var newPage = action switch
            {
                "prev" => Math.Max(0, currentPage - 1),
                "next" => Math.Min(totalPages - 1, currentPage + 1),
                _ => currentPage
            };

            Assert.Equal(currentPage, newPage);
        }

        [Fact]
        public void PaginationLogic_SinglePage_BothDirectionsStay()
        {
            var totalPages = 1;
            var currentPage = 0;

            var prevPage = Math.Max(0, currentPage - 1);
            var nextPage = Math.Min(totalPages - 1, currentPage + 1);

            Assert.Equal(0, prevPage);
            Assert.Equal(0, nextPage);
        }

        #endregion

        #region Navigation Button ID Format Tests

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        [InlineData(4)]
        public void NavigationButtonIds_ContainCurrentPage(int currentPage)
        {
            var sessionId = "abc12345";

            var prevId = $"{PskReporterService.BUTTON_PREFIX}:prev:{sessionId}:{currentPage}";
            var nextId = $"{PskReporterService.BUTTON_PREFIX}:next:{sessionId}:{currentPage}";

            Assert.Contains(currentPage.ToString(), prevId);
            Assert.Contains(currentPage.ToString(), nextId);
            Assert.StartsWith($"{PskReporterService.BUTTON_PREFIX}:", prevId);
            Assert.StartsWith($"{PskReporterService.BUTTON_PREFIX}:", nextId);
        }

        [Fact]
        public void PageButtonId_HasNoPageNumber()
        {
            var sessionId = "abc12345";
            var pageId = $"{PskReporterService.BUTTON_PREFIX}:page:{sessionId}";
            var parts = pageId.Split(':');
            Assert.Equal(3, parts.Length);
        }

        #endregion
    }
}
