namespace FtrIOTests.Integration
{
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
    }
}