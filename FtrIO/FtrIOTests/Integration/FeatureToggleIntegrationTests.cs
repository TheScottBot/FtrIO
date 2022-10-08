namespace FtrIOTests.Integration
{
    using FtrIO;
    using FtrIO.Classes;
    using FtrIO.Enums;
    using FtrIO.Interfaces;
    using NUnit.Framework;
    using ToggleExceptions;
    
    public class FeatureToggleIntegrationTests
    {
        [Test]
        public void TestToggleStatusActiveIsReturnedWhenParsingAnItemThatIsToggledOn()
        {
            IToggleParser configParser = new ToggleParser();
            IFeatureToggle<bool> featureToggle = new FeatureToggle<bool>();

            var toggleStatus = featureToggle.GetToggleState(configParser, "ButtonToggle");
            Assert.AreEqual(ToggleStatus.Active, toggleStatus);
        }

        [Test]
        public void TestToggleStatusInactiveIsReturnedWhenParsingAnItemThatIsToggledOff()
        {
            IToggleParser configParser = new ToggleParser();
            IFeatureToggle<bool> featureToggle = new FeatureToggle<bool>();

            var toggleStatus = featureToggle.GetToggleState(configParser, "NotFinished");
            Assert.AreEqual(ToggleStatus.Inactive, toggleStatus);
        }

        [Test]
        public void TestToggleParsedOutOfRangeExceptionIsReturnedWhenParsingAnItemThatIsToggledAsdf()
        {
            var configParser = new ToggleParser();
            IFeatureToggle<bool> featureToggle = new FeatureToggle<bool>();
            Assert.Throws<ToggleParsedOutOfRangeException>(() => featureToggle.GetToggleState(configParser, "asdf"));
        }

        [Test]
        public void TestToggleDoesNotExistExceptionIsReturnedWhenParsingAnItemThatDoesNotExist()
        {
            var configParser = new ToggleParser();
            IFeatureToggle<bool> featureToggle = new FeatureToggle<bool>();
            Assert.Throws<ToggleDoesNotExistException>(() => featureToggle.GetToggleState(configParser, "wewewewewewewewe"));
        }

        [Test]
        public void TestFakeMethodWillNotChangeValueIfConfigItemIsToggledToFalse()
        {
            var changeMe = "Unchanged";

            FakeMethod("FakeFalse", out changeMe);

            Assert.AreEqual("Unchanged", changeMe);
        }

        [Test]
        public void TestFakeMethodWillChangeValueIfConfigItemIsToggledToTrue()
        {
            var changeMe = "Unchanged";

            FakeMethod("FakeTrue", out changeMe);

            Assert.AreEqual("has been changed", changeMe);
        }

        [Test]
        public void TestActionFakeMethodThatReturnsTrueWillReturnTrueIfConfigItemIsToggledToTrue()
        {
            IFeatureToggle<bool> featureToggler = new FeatureToggle<bool>();

            var result = featureToggler.ExecuteMethodIfToggleOn(FakeMethodThatReturnsTrue, "FakeTrue");

            Assert.IsTrue(result);
        }
        
        [Test]
        public void TestActionFakeMethodThatReturnsTrueWillReturnTrueIfConfigItemIsToggledToTrueWithReflection()
        {
            IFeatureToggle<bool> featureToggler = new FeatureToggle<bool>();

            var result = FakeMethodThatReturnsTrue();

            Assert.IsTrue(result);
        }

        [Test]
        public void TestActionFakeMethodThatReturnsTrueWillReturnFalseIfConfigItemIsToggledToFalse()
        {
            IFeatureToggle<bool> featureToggler = new FeatureToggle<bool>();

            var result = featureToggler.ExecuteMethodIfToggleOn(FakeMethodThatReturnsTrue, "FakeFalse");

            Assert.IsFalse(result);
        }
        
        
        [Test]
        public void TestActionFakeMethodThatReturnsTrueWillReturnFalseIfConfigItemIsToggledToFalseWithReflection()
        {
            IFeatureToggle<bool> featureToggler = new FeatureToggle<bool>();

            var result = featureToggler.ExecuteMethodIfToggleOn(FakeMethodThatReturnsTrue);

            Assert.IsFalse(result);
        }
        
        [Io(Enabled = false)]
        protected void FakeMethod(string keyName, out string changeMe)
        {
                IToggleParser configParser = new ToggleParser();
                IFeatureToggle<bool> featureToggle = new FeatureToggle<bool>();

                var response = featureToggle.GetToggleState(configParser, keyName);
                if (response == ToggleStatus.Active)
                {
                    changeMe = "has been changed";
                    return;
                }
                changeMe = "Unchanged";
        }
        
        [Io]
        protected bool FakeMethodThatReturnsTrue()
        {
            return true;
        }
    }
}