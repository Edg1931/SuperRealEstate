using NUnit.Framework;
using UnityEngine;
using SuperRealEstate.Collaboration;
using SuperRealEstate.CollaborationBackend;
using SuperRealEstate.Staging;

namespace SuperRealEstate.Tests
{
    public class RealtimeFrameParserTests
    {
        private const float Tol = 0.0001f;

        private const string InsertFrame =
            "{\"topic\":\"realtime:layout-abc\",\"event\":\"postgres_changes\",\"payload\":{\"data\":{" +
            "\"schema\":\"public\",\"table\":\"staging_placements\",\"type\":\"INSERT\"," +
            "\"record\":{\"id\":\"p1\",\"layout_id\":\"abc\",\"furniture_asset_id\":\"f1\",\"catalog_item_id\":null," +
            "\"pos_x\":1.5,\"pos_y\":0,\"pos_z\":-2.25,\"rot_y_deg\":90,\"scale\":1,\"anchor_id\":\"a1\"," +
            "\"updated_at\":\"2026-06-27T00:00:00Z\"},\"old_record\":{}},\"ids\":[1]},\"ref\":null}";

        private const string DeleteFrame =
            "{\"topic\":\"realtime:layout-abc\",\"event\":\"postgres_changes\",\"payload\":{\"data\":{" +
            "\"schema\":\"public\",\"table\":\"staging_placements\",\"type\":\"DELETE\"," +
            "\"record\":{},\"old_record\":{\"id\":\"p9\",\"layout_id\":\"abc\"}},\"ids\":[1]},\"ref\":null}";

        private const string PresenceFrame =
            "{\"topic\":\"realtime:layout-abc\",\"event\":\"broadcast\",\"payload\":{\"type\":\"broadcast\"," +
            "\"event\":\"presence\",\"payload\":{\"uid\":\"user-7\",\"px\":1,\"py\":2,\"pz\":3," +
            "\"qx\":0,\"qy\":0,\"qz\":0,\"qw\":1,\"hasPointer\":1," +
            "\"rox\":4,\"roy\":5,\"roz\":6,\"rdx\":0,\"rdy\":0,\"rdz\":1}},\"ref\":null}";

        [Test]
        public void EventOf_ReadsTopLevelEvent()
        {
            Assert.AreEqual("postgres_changes", RealtimeFrameParser.EventOf(InsertFrame));
            Assert.AreEqual("broadcast", RealtimeFrameParser.EventOf(PresenceFrame));
        }

        [Test]
        public void Insert_ParsesRecordIntoPlacement()
        {
            bool ok = RealtimeFrameParser.TryParsePlacementChange(InsertFrame, out var change, out Placement p);
            Assert.IsTrue(ok);
            Assert.AreEqual(RealtimeChangeType.Insert, change);
            Assert.AreEqual("p1", p.Id);
            Assert.AreEqual("f1", p.FurnitureAssetId);
            Assert.That(Vector3.Distance(p.Position, new Vector3(1.5f, 0f, -2.25f)), Is.LessThan(Tol));
            Assert.That(p.YawDegrees, Is.EqualTo(90f).Within(Tol));
            Assert.AreEqual("a1", p.AnchorId);
        }

        [Test]
        public void Delete_TakesIdFromOldRecord()
        {
            bool ok = RealtimeFrameParser.TryParsePlacementChange(DeleteFrame, out var change, out Placement p);
            Assert.IsTrue(ok);
            Assert.AreEqual(RealtimeChangeType.Delete, change);
            Assert.AreEqual("p9", p.Id);
        }

        [Test]
        public void Presence_ParsesPoseAndPointer()
        {
            bool ok = RealtimeFrameParser.TryParsePresence(PresenceFrame, out PresenceUpdate pr);
            Assert.IsTrue(ok);
            Assert.AreEqual("user-7", pr.UserId);
            Assert.That(Vector3.Distance(pr.HeadPose.position, new Vector3(1f, 2f, 3f)), Is.LessThan(Tol));
            Assert.IsTrue(pr.HasPointer);
            Assert.That(Vector3.Distance(pr.Pointer.origin, new Vector3(4f, 5f, 6f)), Is.LessThan(Tol));
        }

        [Test]
        public void ExtractJsonObject_IgnoresOldRecordWhenAskedForRecord()
        {
            string data = RealtimeFrameParser.ExtractJsonObject(InsertFrame, "data");
            string record = RealtimeFrameParser.ExtractJsonObject(data, "record");
            Assert.IsTrue(record.Contains("\"id\":\"p1\""));
            Assert.IsFalse(record.Contains("old_record"));
        }

        [Test]
        public void ExtractStringValue_HandlesNestedKeys()
        {
            Assert.AreEqual("realtime:layout-abc", RealtimeFrameParser.TopicOf(InsertFrame));
        }

        [Test]
        public void NonChangeFrame_ReturnsFalse()
        {
            const string reply = "{\"topic\":\"realtime:layout-abc\",\"event\":\"phx_reply\",\"payload\":{\"status\":\"ok\",\"response\":{}},\"ref\":\"1\"}";
            Assert.IsFalse(RealtimeFrameParser.TryParsePlacementChange(reply, out _, out _));
        }
    }
}
