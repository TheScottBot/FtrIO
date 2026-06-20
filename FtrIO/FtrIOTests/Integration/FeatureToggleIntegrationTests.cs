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
        public void TestActionFakeMethodThatReturnsTrueWillReturnFalseIfConfigItemIsToggledToFalse()
        {
            IFeatureToggle<bool> featureToggler = new FeatureToggle<bool>();

            var result = featureToggler.ExecuteMethodIfToggleOn(FakeMethodThatReturnsTrue, "FakeFalse");

            Assert.IsFalse(result);
        }

        [Test]
        public void TestExecuteMethodIfToggleOnReturnsTrueWhenToggleAttributeMethodIsToggledOn()
        {
            IFeatureToggle<bool> featureToggler = new FeatureToggle<bool>();

            var result = featureToggler.ExecuteMethodIfToggleOn(FakeMethodThatReturnsTrue);

            Assert.IsTrue(result);
        }

        [Test]
        public void TestExecuteMethodIfToggleOnThrowsWhenMethodHasNoToggleAttributeAndNoKeyNameProvided()
        {
            IFeatureToggle<bool> featureToggler = new FeatureToggle<bool>();

            Assert.Throws<ToggleAttributeMissingException>(() => featureToggler.ExecuteMethodIfToggleOn(FakeMethodWithNoToggleAttribute));
        }

        [Test]
        public void TestDirectCallToToggleDecoratedMethodRunsWhenToggledOn()
        {
            // No FeatureToggle/ExecuteMethodIfToggleOn involved at all here -
            // this calls the [Toggle]-decorated method directly to prove the
            // AspectInjector-woven gating is what's deciding the outcome.
            var result = FakeAutoGatedMethodToggledOn();

            Assert.IsTrue(result);
        }

        [Test]
        public void TestDirectCallToToggleDecoratedMethodIsSkippedWhenToggledOff()
        {
            // The method body always returns true, but config says this
            // toggle is off, so a direct call should never reach the body
            // and should come back with default(bool) (false) instead.
            var result = FakeAutoGatedMethodToggledOff();

            Assert.IsFalse(result);
        }

        [Test]
        public void TestDirectCallToToggleDecoratedVoidMethodIsSkippedWhenToggledOff()
        {
            _voidMethodRan = false;

            FakeAutoGatedVoidMethodToggledOff();

            Assert.IsFalse(_voidMethodRan);
        }

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

        [Toggle]
        protected bool FakeMethodThatReturnsTrue()
        {
            return true;
        }

        protected bool FakeMethodWithNoToggleAttribute()
        {
            return true;
        }

        private bool _voidMethodRan;

        [Toggle]
        protected bool FakeAutoGatedMethodToggledOn()
        {
            return true;
        }

        [Toggle]
        protected bool FakeAutoGatedMethodToggledOff()
        {
            return true;
        }

        [Toggle]
        protected void FakeAutoGatedVoidMethodToggledOff()
        {
            _voidMethodRan = true;
        }
    }
}