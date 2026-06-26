using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SuperRealEstate.ProjectsBackend;
using SuperRealEstate.Renovation;
using SuperRealEstate.Staging;

namespace SuperRealEstate.Tests
{
    /// <summary>
    /// Round-trip tests that LOCK the project payload contract: what the
    /// serializer writes is exactly what the parser reads. The web/desktop
    /// design surface must emit this same shape (mirrored in web/lib/projectPayload.ts).
    /// </summary>
    public class ProjectPayloadSerializerTests
    {
        [Test]
        public void Edits_RoundTrip()
        {
            var plan = new RenovationPlan();
            plan.Edits.Add(new RenovationEdit { Kind = EditKind.RemoveWall, TargetId = "w1" });
            plan.Edits.Add(new RenovationEdit { Kind = EditKind.ChangeFloorFinish, TargetId = "r1", MaterialId = "lvp" });
            plan.Edits.Add(new RenovationEdit { Kind = EditKind.AddWall, Start = new Vector2(0, 0), End = new Vector2(4, 0), HeightM = 2.5f });

            string json = ProjectPayloadSerializer.SerializeEdits(plan);
            var parsed = ProjectPayloadParser.ParseEdits(json);

            Assert.AreEqual(3, parsed.Edits.Count);
            Assert.AreEqual(EditKind.RemoveWall, parsed.Edits[0].Kind);
            Assert.AreEqual("w1", parsed.Edits[0].TargetId);
            Assert.AreEqual(EditKind.ChangeFloorFinish, parsed.Edits[1].Kind);
            Assert.AreEqual("lvp", parsed.Edits[1].MaterialId);
            Assert.AreEqual(EditKind.AddWall, parsed.Edits[2].Kind);
            Assert.AreEqual(new Vector2(4, 0), parsed.Edits[2].End);
            Assert.AreEqual(2.5f, parsed.Edits[2].HeightM, 0.001f);
        }

        [Test]
        public void Placements_RoundTrip_SplitsBlueprintAndAnchor()
        {
            var blueprint = new List<BlueprintPlacement>
            {
                new BlueprintPlacement { CatalogItemId = "sofa", PlanPosition = new Vector2(2, 3), PlanYawDegrees = 90f, Scale = 1.25f },
            };
            var anchor = new List<Placement>
            {
                new Placement("chair", new Vector3(1, 0, 1), yawDegrees: 45f),
            };

            string json = ProjectPayloadSerializer.SerializePlacements(blueprint, anchor);
            var (bp, an) = ProjectPayloadParser.ParsePlacements(json);

            Assert.AreEqual(1, bp.Count);
            Assert.AreEqual("sofa", bp[0].CatalogItemId);
            Assert.AreEqual(new Vector2(2, 3), bp[0].PlanPosition);
            Assert.AreEqual(90f, bp[0].PlanYawDegrees, 0.001f);
            Assert.AreEqual(1.25f, bp[0].Scale, 0.001f);

            Assert.AreEqual(1, an.Count);
            Assert.AreEqual("chair", an[0].FurnitureAssetId);
            Assert.AreEqual(new Vector3(1, 0, 1), an[0].Position);
            Assert.AreEqual(45f, an[0].YawDegrees, 0.001f);
        }

        [Test]
        public void Empty_Plan_Serializes_To_EmptyArray()
        {
            string json = ProjectPayloadSerializer.SerializeEdits(new RenovationPlan());
            Assert.AreEqual("[]", json);
            Assert.AreEqual(0, ProjectPayloadParser.ParseEdits(json).Edits.Count);
        }
    }
}
