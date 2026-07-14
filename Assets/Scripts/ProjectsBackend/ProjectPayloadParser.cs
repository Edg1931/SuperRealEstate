using System;
using System.Collections.Generic;
using UnityEngine;
using SuperRealEstate.Acquisition;   // PointDto (reused for {x,y} plan points)
using SuperRealEstate.Renovation;
using SuperRealEstate.Staging;

namespace SuperRealEstate.ProjectsBackend
{
    // ---------------------------------------------------------------------------
    // Wire DTOs for the two JSONB / row payloads a project carries:
    //   * renovation_plans.edits  -> an ordered RenovationEdit[] (JSONB array)
    //   * staging_placements rows -> world (pos_*) and/or blueprint (plan_*) coords
    //
    // The edit JSON mirrors the RenovationEdit fields (camelCase, "kind" as a
    // string, start/end as {x,y} points reusing Acquisition.PointDto), matching
    // the BuildingModelDto convention. Placement DTOs use the snake_case column
    // names from staging_placements (migrations 0002 + 0003) so PostgREST rows
    // deserialize directly.
    //
    // NOTE: JsonUtility cannot deserialize a top-level JSON array, so both
    // parsers wrap the array as `{ "items": [...] }` (the same trick the
    // SupabaseBackendClient uses) before deserializing.
    // ---------------------------------------------------------------------------

    /// <summary>Wire shape for one <see cref="RenovationEdit"/> in the plan's JSONB array.</summary>
    [Serializable]
    public sealed class RenovationEditDto
    {
        public string id;
        public string kind;        // "RemoveWall" | "ChangeFloorFinish" | ...
        public string targetId;    // wall / room / opening id this edit acts on
        public string materialId;  // finish changes
        public float value;        // e.g. new ceiling height (m)
        public PointDto start;     // AddWall / MoveWall geometry
        public PointDto end;
        public float heightM;
        public float thicknessM;
    }

    [Serializable]
    internal sealed class RenovationEditList { public RenovationEditDto[] items; }

    /// <summary>
    /// Wire shape for a <c>staging_placements</c> row (snake_case columns from
    /// migrations 0002 + 0003). A row may carry world coordinates (<c>pos_*</c>),
    /// blueprint-plane coordinates (<c>plan_*</c>), or both; <see cref="ProjectPayloadParser"/>
    /// decides which world it belongs to per row.
    /// </summary>
    [Serializable]
    public sealed class StagingPlacementRow
    {
        public string id;
        public string furniture_asset_id;
        public string catalog_item_id;

        // World / anchor-relative position (always present, defaults to 0).
        public float pos_x;
        public float pos_y;
        public float pos_z;
        public float rot_y_deg;
        public float scale;
        public string anchor_id;

        // Blueprint-plane coordinates (nullable columns -> may be absent / 0).
        public float plan_x;
        public float plan_y;
        public float plan_yaw_deg;

        // Explicit flag disambiguating a placement that legitimately sits at
        // plan origin (0,0). `has_plan` is the DB column (migration 0014);
        // `hasPlan` is the older in-memory/JSON spelling. Either wins over the
        // coordinate heuristic below.
        public bool has_plan;
        public bool hasPlan;
    }

    [Serializable]
    internal sealed class StagingPlacementRowList { public StagingPlacementRow[] items; }

    /// <summary>
    /// Pure, network-free parsers turning a project's stored JSON payloads into
    /// the in-memory shapes the staging pipeline consumes. Kept free of any
    /// <c>UnityWebRequest</c> dependency so the mapping is unit-tested without
    /// hitting Supabase — the network layer (<see cref="SupabaseProjectStore"/>)
    /// only fetches the strings and hands them here.
    ///
    /// Defensive by design: empty / null / malformed JSON yields a safe empty
    /// result rather than throwing, so a bad row never crashes a load.
    /// </summary>
    public static class ProjectPayloadParser
    {
        /// <summary>
        /// Parses the <c>renovation_plans.edits</c> JSONB array into a
        /// <see cref="RenovationPlan"/>. Unknown / missing <c>kind</c> strings map
        /// to <see cref="EditKind.RemoveWall"/>'s safe default via
        /// <see cref="Enum.TryParse{TEnum}(string,bool,out TEnum)"/> fallback.
        /// Null / empty / malformed input yields an empty (but non-null) plan.
        /// </summary>
        public static RenovationPlan ParseEdits(string editsJson)
        {
            var plan = new RenovationPlan { Edits = new List<RenovationEdit>() };
            if (string.IsNullOrWhiteSpace(editsJson)) return plan;

            RenovationEditList list;
            try
            {
                list = JsonUtility.FromJson<RenovationEditList>(Wrap(editsJson));
            }
            catch (Exception)
            {
                // JsonUtility throws on structurally invalid JSON — treat as empty.
                return plan;
            }

            if (list?.items == null) return plan;

            foreach (var dto in list.items)
            {
                if (dto == null) continue;
                plan.Edits.Add(new RenovationEdit
                {
                    Id = dto.id,
                    Kind = ParseEnum(dto.kind, EditKind.RemoveWall),
                    TargetId = dto.targetId,
                    MaterialId = dto.materialId,
                    Value = dto.value,
                    Start = dto.start.ToVector2(),
                    End = dto.end.ToVector2(),
                    HeightM = dto.heightM,
                    ThicknessM = dto.thicknessM,
                });
            }

            return plan;
        }

        /// <summary>
        /// Splits a <c>staging_placements</c> result set into blueprint-authored
        /// placements (plan space) and world / anchor-relative placements.
        ///
        /// Per-row heuristic: a row is treated as a <see cref="BlueprintPlacement"/>
        /// when it carries plan coordinates — either its <c>hasPlan</c> flag is set,
        /// or <c>plan_x</c>/<c>plan_y</c> are non-zero (the nullable plan columns
        /// default to 0 when unset). Otherwise the row's <c>pos_*</c> fields define a
        /// world / anchor <see cref="Placement"/>. The furniture / catalog id is
        /// carried through unchanged so the renderer can resolve the asset either way.
        ///
        /// Null / empty / malformed input yields two empty (but non-null) lists.
        /// </summary>
        public static (List<BlueprintPlacement> blueprint, List<Placement> anchor) ParsePlacements(string placementsJson)
        {
            var blueprint = new List<BlueprintPlacement>();
            var anchor = new List<Placement>();
            if (string.IsNullOrWhiteSpace(placementsJson)) return (blueprint, anchor);

            StagingPlacementRowList list;
            try
            {
                list = JsonUtility.FromJson<StagingPlacementRowList>(Wrap(placementsJson));
            }
            catch (Exception)
            {
                return (blueprint, anchor);
            }

            if (list?.items == null) return (blueprint, anchor);

            foreach (var row in list.items)
            {
                if (row == null) continue;

                if (HasPlanCoords(row))
                {
                    blueprint.Add(new BlueprintPlacement
                    {
                        Id = row.id,
                        FurnitureAssetId = row.furniture_asset_id,
                        CatalogItemId = row.catalog_item_id,
                        PlanPosition = new Vector2(row.plan_x, row.plan_y),
                        PlanYawDegrees = row.plan_yaw_deg,
                        Scale = NormalizeScale(row.scale),
                    });
                }
                else
                {
                    anchor.Add(new Placement
                    {
                        Id = row.id,
                        FurnitureAssetId = row.furniture_asset_id,
                        CatalogItemId = row.catalog_item_id,
                        Position = new Vector3(row.pos_x, row.pos_y, row.pos_z),
                        YawDegrees = row.rot_y_deg,
                        Scale = NormalizeScale(row.scale),
                        AnchorId = row.anchor_id,
                    });
                }
            }

            return (blueprint, anchor);
        }

        private static bool HasPlanCoords(StagingPlacementRow row)
            => row.has_plan || row.hasPlan || row.plan_x != 0f || row.plan_y != 0f;

        // A 0 scale almost always means the column was absent in the JSON; treat
        // it as the default 1 so placements aren't rendered collapsed.
        private static float NormalizeScale(float scale) => scale > 0f ? scale : 1f;

        private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
            => Enum.TryParse(value, ignoreCase: true, out TEnum parsed) ? parsed : fallback;

        // JsonUtility can't parse a top-level array — wrap it as an object.
        private static string Wrap(string jsonArray)
            => "{\"items\":" + (string.IsNullOrEmpty(jsonArray) ? "[]" : jsonArray) + "}";
    }
}
