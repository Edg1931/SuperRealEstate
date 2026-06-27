using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using SuperRealEstate.Collaboration;

namespace SuperRealEstate.Tests
{
    public class AnchorProviderSelectorTests
    {
        [Test]
        public void ArcoreCloud_Resolves_OnAndroidAndIos_NotVisionPro()
        {
            Assert.IsTrue(AnchorProviderSelector.CanResolve(DeviceKind.AndroidXR, AnchorProvider.ArcoreCloud));
            Assert.IsTrue(AnchorProviderSelector.CanResolve(DeviceKind.AndroidPhone, AnchorProvider.ArcoreCloud));
            Assert.IsTrue(AnchorProviderSelector.CanResolve(DeviceKind.IosPhone, AnchorProvider.ArcoreCloud));
            Assert.IsFalse(AnchorProviderSelector.CanResolve(DeviceKind.VisionPro, AnchorProvider.ArcoreCloud));
            Assert.IsFalse(AnchorProviderSelector.CanResolve(DeviceKind.Web, AnchorProvider.ArcoreCloud));
        }

        [Test]
        public void ArkitWorld_Resolves_OnAppleOnly()
        {
            Assert.IsTrue(AnchorProviderSelector.CanResolve(DeviceKind.VisionPro, AnchorProvider.ArkitWorld));
            Assert.IsTrue(AnchorProviderSelector.CanResolve(DeviceKind.IosPhone, AnchorProvider.ArkitWorld));
            Assert.IsFalse(AnchorProviderSelector.CanResolve(DeviceKind.AndroidXR, AnchorProvider.ArkitWorld));
            Assert.IsFalse(AnchorProviderSelector.CanResolve(DeviceKind.AndroidPhone, AnchorProvider.ArkitWorld));
        }

        [Test]
        public void OpenXrPersistent_Resolves_OnAndroidXrOnly()
        {
            Assert.IsTrue(AnchorProviderSelector.CanResolve(DeviceKind.AndroidXR, AnchorProvider.OpenXrPersistent));
            Assert.IsFalse(AnchorProviderSelector.CanResolve(DeviceKind.VisionPro, AnchorProvider.OpenXrPersistent));
            Assert.IsFalse(AnchorProviderSelector.CanResolve(DeviceKind.IosPhone, AnchorProvider.OpenXrPersistent));
        }

        [Test]
        public void Candidates_PrimaryFirst_ThenFallback_FilteredByDevice()
        {
            // Host (Galaxy XR) advertises ARCore Cloud, with an OpenXR fallback.
            var payload = new SharedAnchorPayload
            {
                SessionId = "s1",
                Provider = AnchorProvider.ArcoreCloud,
                AnchorId = "cloud-1",
                AltProvider = nameof(AnchorProvider.OpenXrPersistent),
                AltAnchorId = "openxr-1",
            };

            // A buyer's Galaxy XR can resolve both → cloud first, then openxr.
            IReadOnlyList<AnchorCandidate> xr = AnchorProviderSelector.Candidates(DeviceKind.AndroidXR, payload);
            Assert.AreEqual(2, xr.Count);
            Assert.AreEqual(AnchorProvider.ArcoreCloud, xr[0].Provider);
            Assert.AreEqual("cloud-1", xr[0].AnchorId);
            Assert.AreEqual(AnchorProvider.OpenXrPersistent, xr[1].Provider);

            // A spouse's iPhone can resolve only the ARCore Cloud one.
            IReadOnlyList<AnchorCandidate> ios = AnchorProviderSelector.Candidates(DeviceKind.IosPhone, payload);
            Assert.AreEqual(1, ios.Count);
            Assert.AreEqual(AnchorProvider.ArcoreCloud, ios[0].Provider);
        }

        [Test]
        public void Candidates_VisionPro_GivenOnlyArcore_CannotCoLocate()
        {
            var payload = new SharedAnchorPayload
            {
                SessionId = "s1",
                Provider = AnchorProvider.ArcoreCloud,
                AnchorId = "cloud-1",
            };

            Assert.IsFalse(AnchorProviderSelector.CanCoLocate(DeviceKind.VisionPro, payload));
            Assert.AreEqual(0, AnchorProviderSelector.Candidates(DeviceKind.VisionPro, payload).Count);
        }

        [Test]
        public void Candidates_SkipsEntriesWithoutAnchorId()
        {
            var payload = new SharedAnchorPayload
            {
                SessionId = "s1",
                Provider = AnchorProvider.ArkitWorld,
                AnchorId = "", // missing
                AltProvider = nameof(AnchorProvider.ArkitWorld),
                AltAnchorId = "world-2",
            };

            IReadOnlyList<AnchorCandidate> ios = AnchorProviderSelector.Candidates(DeviceKind.IosPhone, payload);
            Assert.AreEqual(1, ios.Count);
            Assert.AreEqual("world-2", ios[0].AnchorId);
        }

        // --- device-aware resolve through SessionCoLocator ---

        private sealed class MapAnchors : ISpatialAnchorService
        {
            private readonly Dictionary<string, Pose> _map;
            public MapAnchors(Dictionary<string, Pose> map) { _map = map; }
            public Task<string> CreateAnchorAsync(Pose pose, CancellationToken ct = default) => Task.FromResult("x");
            public Task<Pose> ResolveAnchorAsync(string anchorId, CancellationToken ct = default)
            {
                if (_map.TryGetValue(anchorId, out Pose p)) return Task.FromResult(p);
                throw new System.InvalidOperationException($"unresolvable: {anchorId}");
            }
        }

        [Test]
        public async Task DeviceAwareResolve_FallsThrough_ToResolvableProvider()
        {
            var sharedPose = new Pose(new Vector3(4f, 0f, 9f), Quaternion.identity);
            // Only the OpenXR anchor is actually resolvable here; the cloud one isn't.
            var anchors = new MapAnchors(new Dictionary<string, Pose> { ["openxr-1"] = sharedPose });
            var co = new SessionCoLocator(anchors);

            var payload = new SharedAnchorPayload
            {
                SessionId = "s1",
                Provider = AnchorProvider.ArcoreCloud,
                AnchorId = "cloud-1",
                AltProvider = nameof(AnchorProvider.OpenXrPersistent),
                AltAnchorId = "openxr-1",
            };

            AnchorFrame frame = await co.ResolveAsync(payload, DeviceKind.AndroidXR);
            Assert.That(Vector3.Distance(frame.ToWorld(Vector3.zero), sharedPose.position), Is.LessThan(0.001f));
        }

        [Test]
        public void DeviceAwareResolve_Throws_WhenDeviceCannotResolveAnyProvider()
        {
            var anchors = new MapAnchors(new Dictionary<string, Pose>());
            var co = new SessionCoLocator(anchors);
            var payload = new SharedAnchorPayload
            {
                SessionId = "s1",
                Provider = AnchorProvider.ArcoreCloud,
                AnchorId = "cloud-1",
            };

            Assert.ThrowsAsync<System.InvalidOperationException>(
                async () => await co.ResolveAsync(payload, DeviceKind.VisionPro));
        }
    }
}
