using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using SuperRealEstate.Collaboration;

namespace SuperRealEstate.Tests
{
    public class CoLocationTests
    {
        private const float Tol = 0.001f;

        [Test]
        public void AnchorFrame_Identity_IsPassThrough()
        {
            var f = new AnchorFrame(Pose.identity);
            Assert.AreEqual(new Vector3(2f, 0f, 3f), f.ToWorld(new Vector3(2f, 0f, 3f)));
        }

        [Test]
        public void AnchorFrame_RoundTrips_WorldToAnchorAndBack()
        {
            var pose = new Pose(new Vector3(10f, 1f, -4f), Quaternion.Euler(0f, 35f, 0f));
            var f = new AnchorFrame(pose);

            var world = new Vector3(3f, 2f, 5f);
            var local = f.ToAnchorLocal(world);
            var back = f.ToWorld(local);

            Assert.That(Vector3.Distance(back, world), Is.LessThan(0.001f));
        }

        [Test]
        public void AnchorFrame_OriginMapsToAnchorPosition()
        {
            var pose = new Pose(new Vector3(7f, 0f, 2f), Quaternion.Euler(0f, 90f, 0f));
            var f = new AnchorFrame(pose);
            Assert.That(Vector3.Distance(f.ToWorld(Vector3.zero), pose.position), Is.LessThan(Tol));
        }

        private sealed class FakeAnchors : ISpatialAnchorService
        {
            private readonly Pose _resolve;
            public FakeAnchors(Pose resolve) { _resolve = resolve; }
            public Task<string> CreateAnchorAsync(Pose pose, CancellationToken ct = default) => Task.FromResult("anchor-1");
            public Task<Pose> ResolveAnchorAsync(string anchorId, CancellationToken ct = default) => Task.FromResult(_resolve);
        }

        [Test]
        public async Task CoLocator_PublishThenResolve_SharesOrigin()
        {
            var sharedPose = new Pose(new Vector3(5f, 0f, 5f), Quaternion.identity);
            var co = new SessionCoLocator(new FakeAnchors(sharedPose));

            var payload = await co.PublishOriginAsync("sess-1", sharedPose, AnchorProvider.ArcoreCloud);
            Assert.AreEqual("anchor-1", payload.AnchorId);
            Assert.AreEqual(AnchorProvider.ArcoreCloud, payload.Provider);

            var frame = await co.ResolveAsync(payload);
            // A placement at the shared origin lands at the anchor position for every device.
            Assert.That(Vector3.Distance(frame.ToWorld(Vector3.zero), sharedPose.position), Is.LessThan(Tol));
        }
    }
}
