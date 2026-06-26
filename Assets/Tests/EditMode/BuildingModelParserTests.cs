using NUnit.Framework;
using SuperRealEstate.Acquisition;
using SuperRealEstate.Renovation;

namespace SuperRealEstate.Tests
{
    /// <summary>
    /// Unit tests for <see cref="BuildingModelParser"/> — the pure, network-free
    /// conversion both the CubiCasa and Matterport importers share. Exercises a
    /// well-formed sample (walls + room) plus empty/malformed safety.
    /// </summary>
    public class BuildingModelParserTests
    {
        private const float Tol = 0.01f;

        // A 4m x 3m room (12 m² floor) with its two lower walls. Mirrors the
        // edge-function BuildingModel JSON shape (see BuildingModelDto.cs).
        private const string SampleJson = @"
        {
            ""id"": ""cubicasa:job-123"",
            ""sourceType"": ""cubicasa"",
            ""walls"": [
                {
                    ""id"": ""w0"",
                    ""start"": { ""x"": 0.0, ""y"": 0.0 },
                    ""end"":   { ""x"": 4.0, ""y"": 0.0 },
                    ""thicknessM"": 0.12,
                    ""heightM"": 2.5,
                    ""isExterior"": true,
                    ""openings"": [
                        { ""id"": ""o0"", ""kind"": ""Door"", ""offsetM"": 1.0,
                          ""widthM"": 0.9, ""heightM"": 2.0, ""sillHeightM"": 0.0 }
                    ]
                },
                {
                    ""id"": ""w1"",
                    ""start"": { ""x"": 4.0, ""y"": 0.0 },
                    ""end"":   { ""x"": 4.0, ""y"": 3.0 },
                    ""thicknessM"": 0.1,
                    ""heightM"": 2.5,
                    ""isExterior"": false,
                    ""openings"": []
                }
            ],
            ""rooms"": [
                {
                    ""id"": ""r0"",
                    ""name"": ""Living"",
                    ""ceilingHeightM"": 2.7,
                    ""floorOutline"": [
                        { ""x"": 0.0, ""y"": 0.0 },
                        { ""x"": 4.0, ""y"": 0.0 },
                        { ""x"": 4.0, ""y"": 3.0 },
                        { ""x"": 0.0, ""y"": 3.0 }
                    ]
                }
            ]
        }";

        [Test]
        public void Parse_Sample_HasTwoWalls()
        {
            BuildingModel model = BuildingModelParser.Parse(SampleJson);

            Assert.IsNotNull(model);
            Assert.AreEqual(2, model.Walls.Count);
            Assert.AreEqual("cubicasa", model.SourceType);
        }

        [Test]
        public void Parse_Sample_WallLengthAndOpening()
        {
            BuildingModel model = BuildingModelParser.Parse(SampleJson);

            Wall w0 = model.Walls[0];
            Assert.AreEqual(4.0f, w0.LengthM, Tol);
            Assert.AreEqual(0.12f, w0.ThicknessM, Tol);
            Assert.IsTrue(w0.IsExterior);

            // Second wall runs (4,0)->(4,3): length 3m.
            Assert.AreEqual(3.0f, model.Walls[1].LengthM, Tol);

            Assert.AreEqual(1, w0.Openings.Count);
            Assert.AreEqual(OpeningKind.Door, w0.Openings[0].Kind);
            Assert.AreEqual(0.9f, w0.Openings[0].WidthM, Tol);
        }

        [Test]
        public void Parse_Sample_RoomFloorArea()
        {
            BuildingModel model = BuildingModelParser.Parse(SampleJson);

            Assert.AreEqual(1, model.Rooms.Count);
            RoomDef room = model.Rooms[0];
            Assert.AreEqual("Living", room.Name);
            Assert.AreEqual(2.7f, room.CeilingHeightM, Tol);
            // 4m x 3m rectangle = 12 m² (shoelace via MeasurementService).
            Assert.AreEqual(12.0f, room.FloorAreaM2, Tol);
        }

        [Test]
        public void Parse_Null_ReturnsEmptyModel()
        {
            BuildingModel model = BuildingModelParser.Parse(null);

            Assert.IsNotNull(model);
            Assert.IsNotNull(model.Walls);
            Assert.IsNotNull(model.Rooms);
            Assert.AreEqual(0, model.Walls.Count);
            Assert.AreEqual(0, model.Rooms.Count);
        }

        [Test]
        public void Parse_Empty_ReturnsEmptyModel()
        {
            BuildingModel model = BuildingModelParser.Parse("   ");

            Assert.IsNotNull(model);
            Assert.AreEqual(0, model.Walls.Count);
            Assert.AreEqual(0, model.Rooms.Count);
        }

        [Test]
        public void Parse_Malformed_ReturnsEmptyModelSafely()
        {
            BuildingModel model = BuildingModelParser.Parse("{ this is not valid json ][");

            Assert.IsNotNull(model);
            Assert.AreEqual(0, model.Walls.Count);
            Assert.AreEqual(0, model.Rooms.Count);
        }

        [Test]
        public void Parse_MissingArrays_ReturnsEmptyCollections()
        {
            BuildingModel model = BuildingModelParser.Parse(@"{ ""sourceType"": ""matterport"" }");

            Assert.IsNotNull(model);
            Assert.AreEqual("matterport", model.SourceType);
            Assert.AreEqual(0, model.Walls.Count);
            Assert.AreEqual(0, model.Rooms.Count);
        }
    }
}
