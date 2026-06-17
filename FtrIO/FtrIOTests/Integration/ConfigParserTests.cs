namespace FtrIOTests.Integration
{
    using System;
    using System.IO;
    using FtrIO.Classes;
    using NUnit.Framework;
    using ToggleExceptions;

    [TestFixture]
    public class ConfigParserTests
    {
        [Test]
        public void TestTrueIsReturnedWhenParsingAnItemThatIsToggledOn()
        {
            var configParser = new ToggleParser();
            Assert.IsTrue(configParser.GetToggleStatus("ButtonToggle"));
        }

        [Test]
        public void TestFalseIsReturnedWhenParsingAnItemThatIsToggledOff()
        {
            var configParser = new ToggleParser();
            Assert.IsFalse(configParser.GetToggleStatus("NotFinished"));
        }

        [Test]
        public void TestToggleParsedOutOfRangeExceptionIsReturnedWhenParsingAnItemThatIsToggledAsdf()
        {
            var configParser = new ToggleParser();
            Assert.Throws<ToggleParsedOutOfRangeException>(() => configParser.GetToggleStatus("asdf"));
        }

        [Test]
        public void TestToggleDoesNotExistExceptionIsReturnedWhenParsingAnItemThatDoesNotExist()
        {
            var configParser = new ToggleParser();
            Assert.Throws<ToggleDoesNotExistException>(() => configParser.GetToggleStatus("wewewewewewewewe"));
        }

        [Test]
        public void TestEverythingIsTreatedAsOnWhenAppSettingsFileIsMissing()
        {
            var originalDirectory = Directory.GetCurrentDirectory();
            var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDirectory);

            try
            {
                // No appsettings.json exists in this brand new directory.
                Directory.SetCurrentDirectory(tempDirectory);

                var configParser = new ToggleParser();

                Assert.IsFalse(configParser.ToggleConfigTagExists());
                Assert.IsTrue(configParser.GetToggleStatus("SomeMadeUpToggleThatHasNeverExisted"));
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDirectory);
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}