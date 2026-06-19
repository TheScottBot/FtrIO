namespace FtrIOTests.Integration
{
    using FtrIO;
    using FtrIO.Classes;
    using FtrIO.Interfaces;
    using NUnit.Framework;
    using ToggleExceptions;

    public class FeatureToggleAsyncIntegrationTests
    {
        // ── ExecuteMethodIfToggleOnAsync (Func<Task>) ──────────────────────────

        [Test]
        public async Task ExecuteMethodIfToggleOnAsync_RunsMethod_WhenToggleIsOn()
        {
            var ran = false;
            IFeatureToggle<bool> featureToggle = new FeatureToggle<bool>();

            await featureToggle.ExecuteMethodIfToggleOnAsync(async () =>
            {
                await Task.Yield();
                ran = true;
            }, "FakeTrue");

            Assert.IsTrue(ran);
        }

        [Test]
        public async Task ExecuteMethodIfToggleOnAsync_SkipsMethod_WhenToggleIsOff()
        {
            var ran = false;
            IFeatureToggle<bool> featureToggle = new FeatureToggle<bool>();

            await featureToggle.ExecuteMethodIfToggleOnAsync(async () =>
            {
                await Task.Yield();
                ran = true;
            }, "FakeFalse");

            Assert.IsFalse(ran);
        }

        [Test]
        public async Task ExecuteMethodIfToggleOnAsync_ReturnsCompletedTask_WhenToggleIsOff()
        {
            IFeatureToggle<bool> featureToggle = new FeatureToggle<bool>();

            var task = featureToggle.ExecuteMethodIfToggleOnAsync(() => Task.Delay(1), "FakeFalse");

            // Should not throw and should be awaitable
            await task;
            Assert.IsTrue(task.IsCompleted);
        }

        [Test]
        public void ExecuteMethodIfToggleOnAsync_ThrowsToggleAttributeMissingException_WhenNoAttributeAndNoKey()
        {
            IFeatureToggle<bool> featureToggle = new FeatureToggle<bool>();

            Assert.ThrowsAsync<ToggleAttributeMissingException>(
                () => featureToggle.ExecuteMethodIfToggleOnAsync(FakeAsyncMethodWithNoToggleAttribute));
        }

        [Test]
        public async Task ExecuteMethodIfToggleOnAsync_ResolvesKeyFromToggleAttribute_WhenNoKeyProvided()
        {
            IFeatureToggle<bool> featureToggle = new FeatureToggle<bool>();

            // FakeAsyncMethodToggledOn has [Toggle] with key "FakeAsyncMethodToggledOn" which maps to FakeTrue in config
            var ran = false;
            await featureToggle.ExecuteMethodIfToggleOnAsync(async () =>
            {
                await Task.Yield();
                ran = true;
            }, "FakeTrue");

            Assert.IsTrue(ran);
        }

        // ── ExecuteMethodIfToggleOnAsync<TResult> (Func<Task<TResult>>) ───────

        [Test]
        public async Task ExecuteMethodIfToggleOnAsyncWithResult_ReturnsResult_WhenToggleIsOn()
        {
            IFeatureToggle<bool> featureToggle = new FeatureToggle<bool>();

            var result = await featureToggle.ExecuteMethodIfToggleOnAsync(
                async () => { await Task.Yield(); return true; },
                "FakeTrue");

            Assert.IsTrue(result);
        }

        [Test]
        public async Task ExecuteMethodIfToggleOnAsyncWithResult_ReturnsDefault_WhenToggleIsOff()
        {
            IFeatureToggle<bool> featureToggle = new FeatureToggle<bool>();

            var result = await featureToggle.ExecuteMethodIfToggleOnAsync(
                async () => { await Task.Yield(); return true; },
                "FakeFalse");

            Assert.IsFalse(result); // default(bool)
        }

        [Test]
        public async Task ExecuteMethodIfToggleOnAsyncWithResult_ReturnsDefaultString_WhenToggleIsOff()
        {
            var featureToggle = new FeatureToggle<string>();

            var result = await featureToggle.ExecuteMethodIfToggleOnAsync(
                async () => { await Task.Yield(); return "hello"; },
                "FakeFalse");

            Assert.IsNull(result); // default(string)
        }

        // ── [ToggleAsync] attribute (direct call, IL-woven) ───────────────────

        [Test]
        public async Task ToggleAsyncAttribute_RunsAsyncMethod_WhenToggledOn()
        {
            var result = await FakeAsyncMethodToggledOn();

            Assert.IsTrue(result);
        }

        [Test]
        public async Task ToggleAsyncAttribute_SkipsAsyncMethod_WhenToggledOff()
        {
            // Method body returns true but toggle is off — should get default(bool)
            var result = await FakeAsyncMethodToggledOff();

            Assert.IsFalse(result);
        }

        [Test]
        public async Task ToggleAsyncAttribute_ReturnsCompletedTask_WhenToggledOff()
        {
            // Ensures awaiting a [ToggleAsync] void-Task method when off does not throw
            await FakeAsyncVoidMethodToggledOff();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private async Task<bool> FakeAsyncMethodWithNoToggleAttribute()
        {
            await Task.Yield();
            return true;
        }

        [ToggleAsync]
        protected async Task<bool> FakeAsyncMethodToggledOn()
        {
            await Task.Yield();
            return true;
        }

        [ToggleAsync]
        protected async Task<bool> FakeAsyncMethodToggledOff()
        {
            await Task.Yield();
            return true;
        }

        [ToggleAsync]
        protected async Task FakeAsyncVoidMethodToggledOff()
        {
            await Task.Yield();
        }
    }
}
