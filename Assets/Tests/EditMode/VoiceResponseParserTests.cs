using NUnit.Framework;
using SuperRealEstate.Voice;

namespace SuperRealEstate.Tests
{
    public class VoiceResponseParserTests
    {
        [Test]
        public void Parse_MulchEstimate_MapsActionAndMaterial()
        {
            string json = "{\"reply\":\"You'll need about a yard of mulch.\"," +
                          "\"action\":{\"type\":\"estimate_material\",\"target\":\"\",\"material\":\"mulch\",\"params\":\"depth=3in\"}}";
            var r = VoiceResponseParser.Parse(json);

            Assert.AreEqual(VoiceActionType.EstimateMaterial, r.Action.Type);
            Assert.AreEqual("mulch", r.Action.Material);
            Assert.AreEqual("depth=3in", r.Action.Params);
            StringAssert.Contains("mulch", r.Reply);
        }

        [Test]
        public void Parse_RemoveWall_CarriesTarget()
        {
            string json = "{\"reply\":\"Opening it up now.\",\"action\":{\"type\":\"remove_wall\",\"target\":\"wall-3\",\"material\":\"\",\"params\":\"\"}}";
            var r = VoiceResponseParser.Parse(json);
            Assert.AreEqual(VoiceActionType.RemoveWall, r.Action.Type);
            Assert.AreEqual("wall-3", r.Action.Target);
        }

        [Test]
        public void ParseType_HandlesAllKnownAndUnknown()
        {
            Assert.AreEqual(VoiceActionType.MeasureRoom, VoiceResponseParser.ParseType("measure_room"));
            Assert.AreEqual(VoiceActionType.IdentifyPlant, VoiceResponseParser.ParseType("identify_plant"));
            Assert.AreEqual(VoiceActionType.ShowComps, VoiceResponseParser.ParseType("show_comps"));
            Assert.AreEqual(VoiceActionType.AutoStage, VoiceResponseParser.ParseType("auto_stage"));
            Assert.AreEqual(VoiceActionType.None, VoiceResponseParser.ParseType("none"));
            Assert.AreEqual(VoiceActionType.None, VoiceResponseParser.ParseType(""));
            Assert.AreEqual(VoiceActionType.Unknown, VoiceResponseParser.ParseType("teleport"));
        }

        [Test]
        public void Parse_Empty_ReturnsSafeDefault()
        {
            var r = VoiceResponseParser.Parse("");
            Assert.IsNotNull(r.Action);
            Assert.AreEqual(VoiceActionType.None, r.Action.Type);
        }
    }
}
