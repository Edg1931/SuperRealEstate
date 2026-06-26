using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SuperRealEstate.ProjectsBackend;
using SuperRealEstate.Renovation;
using SuperRealEstate.Staging;

namespace SuperRealEstate.Tests
{
    /// <summary>
    /// Unit tests for <see cref="ProjectPayloadParser"/> — the pure, network-free
    /// mapping the <c>SupabaseProjectStore</c> delegates JSON parsing to. Covers
    /// the renovation edit JSONB array and the staging-placement plan/world split,
    /// plus empty / malformed safety.
    /// </summary>
    public class ProjectPayloadParserTests
    {
        private const float Tol = 0.001f;

        // renovation_plans.edits: a RemoveWall (acts on wall w3) + a
        // ChangeFloorFinish (room r1 gets material m-oak). Mirrors the JSONB array.
        private const string EditsJson = @"
        [
            { ""id"": ""e0"", ""kind"": ""RemoveWall"", ""targetId"": ""w3"" },
            { ""id"": ""e1"", ""kind"": ""ChangeFloorFinish"",
              ""targetId"": ""r1"", ""materialId"": ""m-oak"" }
        ]";

        [Test]
        public void ParseEdits_MapsKindsAndCount()
        {
            RenovationPlan plan = ProjectPayloadParser.ParseEdits(EditsJson);

            Assert.IsNotNull(plan);
            Assert.AreEqual(2, plan.Edits.Count);

            Assert.AreEqual(EditKind.RemoveWall, plan.Edits[0].Kind);
            Assert.AreEqual("w3", plan.Edits[0].TargetId);

            Assert.AreEqual(EditKind.ChangeFloorFinish, plan.Edits[1].Kind);
            Assert.AreEqual("r1", plan.Edits[1].TargetId);
            Assert.AreEqual("m-oak", plan.Edits[1].MaterialId);
        }

        [Test]
        public void ParseEdits_CarriesGeometryAndValue()
        {
            const string json = @"
            [
                { ""id"": ""e2"", ""kind"": ""AddWall"",
                  ""start"": { ""x"": 1.0, ""y"": 2.0 },
                  ""end"":   { ""x"": 5.0, ""y"": 2.0 },
                  ""heightM"": 2.4, ""thicknessM"": 0.15 },
                { ""id"": ""e3"", ""kind"": ""ChangeCeilingHeight"",
                  ""targetId"": ""r0"", ""value"": 3.1 }
            ]";

            RenovationPlan plan = ProjectPayloadParser.ParseEdits(json);

            Assert.AreEqual(2, plan.Edits.Count);

            RenovationEdit add = plan.Edits[0];
            Assert.AreEqual(EditKind.AddWall, add.Kind);
            Assert.AreEqual(new Vector2(1f, 2f), add.Start);
            Assert.AreEqual(new Vector2(5f, 2f), add.End);
            Assert.AreEqual(2.4f, add.HeightM, Tol);
            Assert.AreEqual(0.15f, add.ThicknessM, Tol);

            Assert.AreEqual(EditKind.ChangeCeilingHeight, plan.Edits[1].Kind);
            Assert.AreEqual(3.1f, plan.Edits[1].Value, Tol);
        }

        [Test]
        public void ParseEdits_NullEmptyMalformed_AreSafe()
        {
            Assert.AreEqual(0, ProjectPayloadParser.ParseEdits(null).Edits.Count);
            Assert.AreEqual(0, ProjectPayloadParser.ParseEdits("   ").Edits.Count);
            Assert.AreEqual(0, ProjectPayloadParser.ParseEdits("[]").Edits.Count);
            Assert.AreEqual(0, ProjectPayloadParser.ParseEdits("{ not valid ][").Edits.Count);
        }

        // staging_placements rows: one blueprint-authored (plan_x/plan_y set,
        // carries a catalog item) + one world/anchor placement (pos_* set,
        // carries a furniture asset + anchor id).
        private const string PlacementsJson = @"
        [
            { ""id"": ""p0"", ""catalog_item_id"": ""cat-sofa"",
              ""plan_x"": 2.5, ""plan_y"": 1.0, ""plan_yaw_deg"": 90.0, ""scale"": 1.0 },
            { ""id"": ""p1"", ""furniture_asset_id"": ""fa-lamp"",
              ""pos_x"": 3.0, ""pos_y"": 0.0, ""pos_z"": -2.0,
              ""rot_y_deg"": 45.0, ""scale"": 1.0, ""anchor_id"": ""anchor-7"" }
        ]";

        [Test]
        public void ParsePlacements_SplitsPlanVsWorld()
        {
            (List<BlueprintPlacement> blueprint, List<Placement> anchor) =
                ProjectPayloadParser.ParsePlacements(PlacementsJson);

            Assert.AreEqual(1, blueprint.Count);
            Assert.AreEqual(1, anchor.Count);

            BlueprintPlacement bp = blueprint[0];
            Assert.AreEqual("p0", bp.Id);
            Assert.AreEqual("cat-sofa", bp.CatalogItemId);
            Assert.AreEqual(new Vector2(2.5f, 1.0f), bp.PlanPosition);
            Assert.AreEqual(90f, bp.PlanYawDegrees, Tol);

            Placement wp = anchor[0];
            Assert.AreEqual("p1", wp.Id);
            Assert.AreEqual("fa-lamp", wp.FurnitureAssetId);
            Assert.AreEqual(new Vector3(3f, 0f, -2f), wp.Position);
            Assert.AreEqual(45f, wp.YawDegrees, Tol);
            Assert.AreEqual("anchor-7", wp.AnchorId);
        }

        [Test]
        public void ParsePlacements_HasPlanFlag_ForcesBlueprintAtOrigin()
        {
            // A placement legitimately at plan origin (0,0) is only treated as
            // blueprint when the explicit flag says so.
            const string json = @"
            [
                { ""id"": ""p2"", ""catalog_item_id"": ""cat-rug"",
                  ""plan_x"": 0.0, ""plan_y"": 0.0, ""hasPlan"": true, ""scale"": 1.0 }
            ]";

            (List<BlueprintPlacement> blueprint, List<Placement> anchor) =
                ProjectPayloadParser.ParsePlacements(json);

            Assert.AreEqual(1, blueprint.Count);
            Assert.AreEqual(0, anchor.Count);
            Assert.AreEqual(Vector2.zero, blueprint[0].PlanPosition);
        }

        [Test]
        public void ParsePlacements_ZeroScale_DefaultsToOne()
        {
            const string json = @"
            [ { ""id"": ""p3"", ""furniture_asset_id"": ""fa"", ""pos_x"": 1.0 } ]";

            (_, List<Placement> anchor) = ProjectPayloadParser.ParsePlacements(json);

            Assert.AreEqual(1, anchor.Count);
            // scale column absent -> 0 in JSON -> normalized to 1.
            Assert.AreEqual(1f, anchor[0].Scale, Tol);
        }

        [Test]
        public void ParsePlacements_NullEmptyMalformed_AreSafe()
        {
            foreach (string bad in new[] { null, "   ", "[]", "{ broken ][" })
            {
                (List<BlueprintPlacement> bp, List<Placement> wp) =
                    ProjectPayloadParser.ParsePlacements(bad);
                Assert.IsNotNull(bp);
                Assert.IsNotNull(wp);
                Assert.AreEqual(0, bp.Count);
                Assert.AreEqual(0, wp.Count);
            }
        }
    }
}
