using Xunit;
using SevenThree.Modules;

namespace SevenThree.Tests
{
    public class HelpCommandTests
    {
        #region CategorizeCommand Tests

        [Theory]
        [InlineData("tech", "QuickStartSlashCommands", "Quick Start")]
        [InlineData("general", "QuickStartSlashCommands", "Quick Start")]
        [InlineData("extra", "QuickStartSlashCommands", "Quick Start")]
        public void CategorizeCommand_QuickStartCommands_ReturnsQuickStart(string commandName, string moduleName, string expected)
        {
            var result = HelpSlashCommands.CategorizeCommand(commandName, moduleName);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void CategorizeCommand_QuizModule_ReturnsPracticeExams()
        {
            var result = HelpSlashCommands.CategorizeCommand("start", "quiz");
            Assert.Equal("Practice Exams", result);
        }

        [Fact]
        public void CategorizeCommand_StudyModule_ReturnsStudyAndReview()
        {
            var result = HelpSlashCommands.CategorizeCommand("missed", "study");
            Assert.Equal("Study & Review", result);
        }

        [Fact]
        public void CategorizeCommand_QrzModule_ReturnsQrzLookup()
        {
            var result = HelpSlashCommands.CategorizeCommand("lookup", "qrz");
            Assert.Equal("QRZ Lookup", result);
        }

        [Fact]
        public void CategorizeCommand_PskModule_ReturnsPskReporter()
        {
            var result = HelpSlashCommands.CategorizeCommand("spots", "psk");
            Assert.Equal("PSK Reporter", result);
        }

        [Fact]
        public void CategorizeCommand_CallsignModule_ReturnsCallsign()
        {
            var result = HelpSlashCommands.CategorizeCommand("set", "callsign");
            Assert.Equal("Callsign", result);
        }

        [Fact]
        public void CategorizeCommand_ConditionsCommand_ReturnsBandConditions()
        {
            var result = HelpSlashCommands.CategorizeCommand("conditions", "SomeModule");
            Assert.Equal("Band Conditions", result);
        }

        [Fact]
        public void CategorizeCommand_QuizSettingsModule_ReturnsServerSettings()
        {
            var result = HelpSlashCommands.CategorizeCommand("channel", "quizsettings");
            Assert.Equal("Server Settings", result);
        }

        [Theory]
        [InlineData("import")]
        [InlineData("playing")]
        public void CategorizeCommand_AdminCommands_ReturnsAdmin(string commandName)
        {
            var result = HelpSlashCommands.CategorizeCommand(commandName, "AdminModule");
            Assert.Equal("Admin", result);
        }

        [Fact]
        public void CategorizeCommand_UnknownCommand_ReturnsOther()
        {
            var result = HelpSlashCommands.CategorizeCommand("unknowncommand", "UnknownModule");
            Assert.Equal("Other", result);
        }

        [Fact]
        public void CategorizeCommand_TechWithNonQuickStartModule_ReturnsPracticeExams()
        {
            // "tech" command name but in the "quiz" module should be Practice Exams, not Quick Start
            var result = HelpSlashCommands.CategorizeCommand("tech", "quiz");
            Assert.Equal("Practice Exams", result);
        }

        #endregion

        #region FormatCommandName Tests

        [Theory]
        [InlineData("start", "quiz", "/quiz start")]
        [InlineData("lookup", "qrz", "/qrz lookup")]
        [InlineData("spots", "psk", "/psk spots")]
        [InlineData("set", "callsign", "/callsign set")]
        [InlineData("channel", "quizsettings", "/quizsettings channel")]
        [InlineData("missed", "study", "/study missed")]
        public void FormatCommandName_GroupedModule_ReturnsGroupedFormat(string commandName, string moduleName, string expected)
        {
            var result = HelpSlashCommands.FormatCommandName(commandName, moduleName);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("help", "HelpSlashCommands", "/help")]
        [InlineData("conditions", "BandConditionsModule", "/conditions")]
        [InlineData("tech", "QuickStartSlashCommands", "/tech")]
        public void FormatCommandName_UngroupedModule_ReturnsSimpleFormat(string commandName, string moduleName, string expected)
        {
            var result = HelpSlashCommands.FormatCommandName(commandName, moduleName);
            Assert.Equal(expected, result);
        }

        #endregion
    }
}
