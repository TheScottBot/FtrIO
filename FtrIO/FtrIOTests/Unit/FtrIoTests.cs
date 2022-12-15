namespace FtrIOTests.Unit
{
    using FtrIO.Classes;
    using FtrIO.Enums;
    using FtrIO.Interfaces;
    using NUnit.Framework;

    [TestFixture]
    public class FtrIoTests
    {

        [Test]
        public void TestSuccessfullParseReturnsToggleStatusActive()
        {
            IToggleParser testParser = new ToggleParserTestDouble();
            var toggle = new FeatureToggle<bool>();
            var toggleResponse = toggle.GetToggleState(testParser, "positive");
            Assert.AreEqual(ToggleStatus.Active, toggleResponse);
        }

        [Test]
        public void TestUnSuccessfullParseReturnsToggleStatusInactive()
        {
            IToggleParser testParser = new ToggleParserTestDouble();
            var toggle = new FeatureToggle<bool>();
            var toggleResponse = toggle.GetToggleState(testParser, "anythingElse");
            Assert.AreEqual(ToggleStatus.Inactive, toggleResponse);
        }

        [Test]
        public void TestSuccessfullFuncCallWhenToggleStatusActive()
        {
            IToggleParser testParser = new ToggleParserTestDouble();
            var toggle = new FeatureToggle<bool>();
            Func<bool> theAction = AlwaysReturnTrue;

            var toggleResponse = toggle.ExecuteMethodIfToggleOn(theAction, testParser, "positive");

            Assert.AreEqual(true, toggleResponse);
        }

        [Test]
        public void TestUnSuccessfullFuncCallWhenToggleStatusInactive()
        {
            IToggleParser testParser = new ToggleParserTestDouble();
            var toggle = new FeatureToggle<bool>();
            Func<bool> theAction = AlwaysReturnTrue;

            var toggleResponse = toggle.ExecuteMethodIfToggleOn(theAction, testParser, "anythingElse");
            Assert.AreEqual(false, toggleResponse);
        }

        [Test]
        public void TestSuccessfullFuncStringCallWhenToggleStatusActive()
        {
            IToggleParser testParser = new ToggleParserTestDouble();
            var toggle = new FeatureToggle<string>();
            Func<string> theAction = AlwaysReturnFire;

            var toggleResponse = toggle.ExecuteMethodIfToggleOn(theAction, testParser, "positive");

            Assert.AreEqual("Fire", toggleResponse);
        }

        [Test]
        public void TestUnSuccessfullFuncStringCallWhenToggleStatusInactive()
        {
            IToggleParser testParser = new ToggleParserTestDouble();
            var toggle = new FeatureToggle<string>();
            Func<string> theAction = AlwaysReturnFire;

            var toggleResponse = toggle.ExecuteMethodIfToggleOn(theAction, testParser, "anythingElse");
            Assert.AreNotEqual("Fire", toggleResponse);
        }

        [Test]
        public void TestSuccessfullFuncTestDataTypeCallWhenToggleStatusActive()
        {
            IToggleParser testParser = new ToggleParserTestDouble();
            var toggle = new FeatureToggle<TestDataType>();
            Func<TestDataType> theAction = AlwaysReturnNewTestDataType;

            var toggleResponse = toggle.ExecuteMethodIfToggleOn(theAction, testParser, "positive");

            Assert.AreEqual(new TestDataType().HappynessIs, toggleResponse.HappynessIs);
        }

        [Test]
        public void TestUnSuccessfullFuncTestDataTypeCallWhenToggleStatusInactive()
        {
            IToggleParser testParser = new ToggleParserTestDouble();
            var toggle = new FeatureToggle<TestDataType>();
            Func<TestDataType> theAction = AlwaysReturnNewTestDataType;

            var toggleResponse = toggle.ExecuteMethodIfToggleOn(theAction, testParser, "anythingElse");
            Assert.IsNull(toggleResponse);
        }

        protected bool AlwaysReturnTrue()
        {
            return true;
        }

        protected string AlwaysReturnFire()
        {
            return "Fire";
        }

        protected TestDataType AlwaysReturnNewTestDataType()
        {
            return new TestDataType();
        }
    }

    public class TestDataType
    {
        public string HappynessIs = "Happy";
    }

    internal class ToggleParserTestDouble : IToggleParser
    {
        public bool ToggleConfigTagExists()
        {
            throw new System.NotImplementedException();
        }

        public bool GetToggleStatus(string toggle)
        {
            return toggle == "positive";
        }

        public bool ParseBoolValueFromSource(string status)
        {
            throw new System.NotImplementedException();
        }
    }
}