using System.Collections.Generic;
using UnityEngine;
using SuperRealEstate.Acquisition; // PointDto
using SuperRealEstate.Renovation;
using SuperRealEstate.Staging;

namespace SuperRealEstate.ProjectsBackend
{
    /// <summary>
    /// The inverse of <see cref="ProjectPayloadParser"/>: writes the canonical
    /// JSON for a project's renovation edits and placements. This LOCKS the
    /// contract — whatever authors a project (the desktop/web design surface)
    /// must emit exactly this shape, and the round-trip is unit-tested
    /// (serialize → parse → equal). See docs/PROJECT-PAYLOAD-CONTRACT.md and the
    /// TypeScript mirror in web/lib/projectPayload.ts.
    ///
    /// Output is a bare JSON array (what the JSONB columns store); the parser
    /// wraps it as {"items":[...]} for JsonUtility, so we unwrap to match.
    /// </summary>
    public static class ProjectPayloadSerializer
    {
        /// <summary>Serialize a renovation plan's edits to the canonical JSONB array.</summary>
        public static string SerializeEdits(RenovationPlan plan)
        {
            var items = new List<RenovationEditDto>();
            if (plan?.Edits != null)
            {
                foreach (var e in plan.Edits)
                {
                    if (e == null) continue;
                    items.Add(new RenovationEditDto
                    {
                        id = e.Id,
                        kind = e.Kind.ToString(),
                        targetId = e.TargetId,
                        materialId = e.MaterialId,
                        value = e.Value,
                        start = new PointDto { x = e.Start.x, y = e.Start.y },
                        end = new PointDto { x = e.End.x, y = e.End.y },
                        heightM = e.HeightM,
                        thicknessM = e.ThicknessM,
                    });
                }
            }
            return Unwrap(JsonUtility.ToJson(new RenovationEditList { items = items.ToArray() }));
        }

        /// <summary>Serialize blueprint + anchor/world placements to the canonical rows.</summary>
        public static string SerializePlacements(
            IReadOnlyList<BlueprintPlacement> blueprintPlacements,
            IReadOnlyList<Placement> anchorPlacements)
        {
            var rows = new List<StagingPlacementRow>();

            if (blueprintPlacements != null)
            {
                foreach (var bp in blueprintPlacements)
                {
                    if (bp == null) continue;
                    rows.Add(new StagingPlacementRow
                    {
                        id = bp.Id,
                        furniture_asset_id = bp.FurnitureAssetId,
                        catalog_item_id = bp.CatalogItemId,
                        plan_x = bp.PlanPosition.x,
                        plan_y = bp.PlanPosition.y,
                        plan_yaw_deg = bp.PlanYawDegrees,
                        scale = bp.Scale,
                        hasPlan = true,  // disambiguates a placement legitimately at plan origin
                        has_plan = true, // DB column spelling (migration 0014)
                    });
                }
            }

            if (anchorPlacements != null)
            {
                foreach (var p in anchorPlacements)
                {
                    if (p == null) continue;
                    rows.Add(new StagingPlacementRow
                    {
                        id = p.Id,
                        furniture_asset_id = p.FurnitureAssetId,
                        catalog_item_id = p.CatalogItemId,
                        pos_x = p.Position.x,
                        pos_y = p.Position.y,
                        pos_z = p.Position.z,
                        rot_y_deg = p.YawDegrees,
                        scale = p.Scale,
                        anchor_id = p.AnchorId,
                    });
                }
            }

            return Unwrap(JsonUtility.ToJson(new StagingPlacementRowList { items = rows.ToArray() }));
        }

        // Inverse of ProjectPayloadParser.Wrap: {"items":X} -> X.
        private static string Unwrap(string wrapped)
        {
            const string prefix = "{\"items\":";
            if (!string.IsNullOrEmpty(wrapped) && wrapped.StartsWith(prefix) && wrapped.EndsWith("}"))
                return wrapped.Substring(prefix.Length, wrapped.Length - prefix.Length - 1);
            return wrapped;
        }
    }
}
