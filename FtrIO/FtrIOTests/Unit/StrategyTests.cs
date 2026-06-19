namespace FtrIOTests.Unit
{
    using FtrIO.Strategies;
    using NUnit.Framework;
    using ToggleExceptions;

    [TestFixture]
    public class StrategyTests
    {
        // ── BooleanStrategy ────────────────────────────────────────────────────

        [TestCase("true")]
        [TestCase("True")]
        [TestCase("TRUE")]
        [TestCase("false")]
        [TestCase("False")]
        [TestCase("FALSE")]
        [TestCase("1")]
        [TestCase("0")]
        public void BooleanStrategy_CanHandle_ReturnsTrueForValidBooleanValues(string value)
            => Assert.IsTrue(new BooleanStrategy().CanHandle(value));

        [TestCase("yes")]
        [TestCase("no")]
        [TestCase("ASDF")]
        [TestCase("50%")]
        [TestCase("blue")]
        [TestCase("")]
        public void BooleanStrategy_CanHandle_ReturnsFalseForNonBooleanValues(string value)
            => Assert.IsFalse(new BooleanStrategy().CanHandle(value));

        [TestCase("true")]
        [TestCase("True")]
        [TestCase("TRUE")]
        [TestCase("1")]
        public void BooleanStrategy_ShouldExecute_ReturnsTrueForTruthyValues(string value)
            => Assert.IsTrue(new BooleanStrategy().ShouldExecute("key", value));

        [TestCase("false")]
        [TestCase("False")]
        [TestCase("FALSE")]
        [TestCase("0")]
        public void BooleanStrategy_ShouldExecute_ReturnsFalseForFalsyValues(string value)
            => Assert.IsFalse(new BooleanStrategy().ShouldExecute("key", value));

        // ── PercentageRolloutStrategy ──────────────────────────────────────────

        [TestCase("0%")]
        [TestCase("50%")]
        [TestCase("100%")]
        [TestCase("  25%  ")]
        public void PercentageRolloutStrategy_CanHandle_ReturnsTrueForPercentageValues(string value)
            => Assert.IsTrue(new PercentageRolloutStrategy().CanHandle(value));

        [TestCase("true")]
        [TestCase("blue")]
        [TestCase("50")]
        [TestCase("ASDF")]
        public void PercentageRolloutStrategy_CanHandle_ReturnsFalseForNonPercentageValues(string value)
            => Assert.IsFalse(new PercentageRolloutStrategy().CanHandle(value));

        [Test]
        public void PercentageRolloutStrategy_ShouldExecute_AlwaysReturnsFalseAtZeroPercent()
        {
            var strategy = new PercentageRolloutStrategy();
            for (var i = 0; i < 100; i++)
                Assert.IsFalse(strategy.ShouldExecute("key", "0%"));
        }

        [Test]
        public void PercentageRolloutStrategy_ShouldExecute_AlwaysReturnsTrueAtOneHundredPercent()
        {
            var strategy = new PercentageRolloutStrategy();
            for (var i = 0; i < 100; i++)
                Assert.IsTrue(strategy.ShouldExecute("key", "100%"));
        }

        [Test]
        public void PercentageRolloutStrategy_ShouldExecute_ReturnsMixOfTrueAndFalseAtFiftyPercent()
        {
            var strategy = new PercentageRolloutStrategy();
            var results = Enumerable.Range(0, 1000).Select(_ => strategy.ShouldExecute("key", "50%")).ToList();
            Assert.IsTrue(results.Any(r => r), "Expected at least one true in 1000 trials at 50%");
            Assert.IsTrue(results.Any(r => !r), "Expected at least one false in 1000 trials at 50%");
        }

        [TestCase("101%")]
        [TestCase("-1%")]
        [TestCase("abc%")]
        public void PercentageRolloutStrategy_ShouldExecute_ThrowsToggleParsedOutOfRangeExceptionForInvalidValues(string value)
            => Assert.Throws<ToggleParsedOutOfRangeException>(
                () => new PercentageRolloutStrategy().ShouldExecute("key", value));

        // ── BlueGreenStrategy ─────────────────────────────────────────────────

        [Test]
        public void BlueGreenStrategy_CanHandle_ReturnsTrueForKnownSlot()
        {
            var strategy = new BlueGreenStrategy("blue", "blue", "green");
            Assert.IsTrue(strategy.CanHandle("blue"));
            Assert.IsTrue(strategy.CanHandle("green"));
        }

        [Test]
        public void BlueGreenStrategy_CanHandle_ReturnsFalseForUnknownSlot()
        {
            var strategy = new BlueGreenStrategy("blue", "blue", "green");
            Assert.IsFalse(strategy.CanHandle("canary"));
            Assert.IsFalse(strategy.CanHandle("true"));
        }

        [Test]
        public void BlueGreenStrategy_CanHandle_IsCaseInsensitive()
        {
            var strategy = new BlueGreenStrategy("blue", "blue", "green");
            Assert.IsTrue(strategy.CanHandle("Blue"));
            Assert.IsTrue(strategy.CanHandle("GREEN"));
        }

        [Test]
        public void BlueGreenStrategy_ShouldExecute_ReturnsTrueWhenCurrentSlotMatches()
        {
            var strategy = new BlueGreenStrategy("blue", "blue", "green");
            Assert.IsTrue(strategy.ShouldExecute("key", "blue"));
        }

        [Test]
        public void BlueGreenStrategy_ShouldExecute_ReturnsFalseWhenCurrentSlotDoesNotMatch()
        {
            var strategy = new BlueGreenStrategy("blue", "blue", "green");
            Assert.IsFalse(strategy.ShouldExecute("key", "green"));
        }

        [Test]
        public void BlueGreenStrategy_ShouldExecute_IsCaseInsensitive()
        {
            var strategy = new BlueGreenStrategy("blue", "blue", "green");
            Assert.IsTrue(strategy.ShouldExecute("key", "Blue"));
            Assert.IsTrue(strategy.ShouldExecute("key", "BLUE"));
        }

        [Test]
        public void BlueGreenStrategy_ShouldExecute_WorksWithThreeOrMoreSlots()
        {
            var strategy = new BlueGreenStrategy("canary", "blue", "green", "canary");
            Assert.IsFalse(strategy.ShouldExecute("key", "blue"));
            Assert.IsFalse(strategy.ShouldExecute("key", "green"));
            Assert.IsTrue(strategy.ShouldExecute("key", "canary"));
        }

        [Test]
        public void BlueGreenStrategy_CanHandle_IgnoresWhitespace()
        {
            var strategy = new BlueGreenStrategy("blue", "blue", "green");
            Assert.IsTrue(strategy.CanHandle("  blue  "));
        }
    }
}
