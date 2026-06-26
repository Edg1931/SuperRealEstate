using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SuperRealEstate.App;
using SuperRealEstate.Platform;
using SuperRealEstate.UI;
using SuperRealEstate.Voice;

namespace SuperRealEstate.Tests
{
    public class ActionDispatcherTests
    {
        private sealed class FakeActions : IAppActions
        {
            public string LastCall;
            public string LastMaterial, LastWall;
            public Task MeasureRoomAsync(CancellationToken ct = default) { LastCall = "measure"; return Task.CompletedTask; }
            public Task IdentifyPlantAsync(CancellationToken ct = default) { LastCall = "plant"; return Task.CompletedTask; }
            public Task EstimateMaterialAsync(string m, string p, CancellationToken ct = default) { LastCall = "estimate"; LastMaterial = m; return Task.CompletedTask; }
            public Task RecognizeFinishAsync(CancellationToken ct = default) { LastCall = "finish"; return Task.CompletedTask; }
            public Task RemoveWallAsync(string id, CancellationToken ct = default) { LastCall = "removewall"; LastWall = id; return Task.CompletedTask; }
            public Task StageFurnitureAsync(string item, CancellationToken ct = default) { LastCall = "stage"; return Task.CompletedTask; }
            public Task ShowCompsAsync(CancellationToken ct = default) { LastCall = "comps"; return Task.CompletedTask; }
        }

        private static XrCapabilities Galaxy => XrCapabilityProfiles.For(XrPlatform.AndroidXrHeadset);
        private static XrCapabilities Glasses => XrCapabilityProfiles.For(XrPlatform.AndroidXrGlasses);

        [Test]
        public async Task EstimateMaterial_Dispatches_OnCapableDevice()
        {
            var fake = new FakeActions();
            var d = new ActionDispatcher(fake);
            var action = new VoiceAction { Type = VoiceActionType.EstimateMaterial, Material = "mulch", Params = "depth=3in" };

            var result = await d.DispatchAsync(action, Galaxy);

            Assert.IsTrue(result.Dispatched);
            Assert.AreEqual("estimate", fake.LastCall);
            Assert.AreEqual("mulch", fake.LastMaterial);
        }

        [Test]
        public async Task RemoveWall_Blocked_OnOpticalGlasses_WithReason_AndNoCall()
        {
            var fake = new FakeActions();
            var d = new ActionDispatcher(fake);
            var action = new VoiceAction { Type = VoiceActionType.RemoveWall, Target = "wall-3" };

            var result = await d.DispatchAsync(action, Glasses);

            Assert.IsFalse(result.Dispatched);
            Assert.IsFalse(result.Available);
            StringAssert.Contains("optical", result.Reason);
            Assert.IsNull(fake.LastCall); // never invoked
        }

        [Test]
        public async Task NoneAction_IsNoOp()
        {
            var fake = new FakeActions();
            var d = new ActionDispatcher(fake);
            var result = await d.DispatchAsync(new VoiceAction { Type = VoiceActionType.None }, Galaxy);
            Assert.IsFalse(result.Dispatched);
            Assert.IsNull(fake.LastCall);
        }

        [Test]
        public void ResolveTool_GatesByDevice()
        {
            Assert.IsFalse(ActionDispatcher.ResolveTool(ToolId.RemoveWall, Glasses).Available);
            var measure = ActionDispatcher.ResolveTool(ToolId.Measure, Galaxy);
            Assert.IsTrue(measure.Available);
            Assert.AreEqual(AppFeature.RoomMeasurement, measure.Feature);
        }
    }
}
