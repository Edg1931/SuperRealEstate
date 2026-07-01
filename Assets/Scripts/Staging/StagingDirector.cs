using System;
using System.Collections.Generic;
using UnityEngine;

namespace SuperRealEstate.Staging
{
    /// <summary>
    /// The pure core of the AI Staging Director: "stage this room, warm modern
    /// style" → a validated, to-scale furniture layout. The AI (Claude, via the
    /// `stage-director` Edge Function) proposes placements from the vendor
    /// catalog plus the client's own captured furniture; everything here is the
    /// deterministic half — building the request, parsing the plan, and
    /// validating every placement against the real room geometry with
    /// <see cref="FitChecker"/>. AI output is advisory; geometry is not.
    /// All coordinates are floor-plane meters (x east, z north), yaw in degrees.
    /// </summary>
    public static class StagingDirector
    {
        /// <summary>Default walkway clearance the validator enforces around each piece.</summary>
        public const float DefaultClearanceM = 0.05f;

        /// <summary>How far a misplaced item may be nudged toward the room center to rescue it.</summary>
        public const float MaxRescueNudgeM = 0.75f;

        // ---------- items offered to the director ----------

        /// <summary>A stageable item (catalog or the user's own capture), flattened for the AI.</summary>
        [Serializable]
        public sealed class DirectorItem
        {
            public string id;
            public string name;
            public string category;
            public float widthM;
            public float depthM;
            public float heightM;
            public float priceUsd;      // 0 for the user's own furniture
            public bool userFurniture;  // true = client already owns it (prefer using it!)

            public Vector3 Size => new Vector3(widthM, heightM, depthM);

            public static DirectorItem From(CatalogItem c) => c == null ? null : new DirectorItem
            {
                id = c.Id, name = c.Name, category = c.Category,
                widthM = c.Size.x, depthM = c.Size.z, heightM = c.Size.y,
                priceUsd = c.Price, userFurniture = false,
            };

            public static DirectorItem From(FurnitureAsset f) => f == null ? null : new DirectorItem
            {
                id = f.Id, name = f.Name, category = f.Category,
                widthM = f.Size.x, depthM = f.Size.z, heightM = f.Size.y,
                priceUsd = 0f, userFurniture = true,
            };
        }

        // ---------- request ----------

        /// <summary>
        /// Build the JSON body for the `stage-director` Edge Function. Room
        /// outline is the floor polygon in meters.
        /// </summary>
        public static string BuildRequestJson(string style, IReadOnlyList<Vector3> roomOutline, IReadOnlyList<DirectorItem> items)
        {
            if (roomOutline == null || roomOutline.Count < 3) throw new ArgumentException("room outline required", nameof(roomOutline));
            if (items == null || items.Count == 0) throw new ArgumentException("at least one stageable item required", nameof(items));

            var dto = new RequestDto { style = string.IsNullOrEmpty(style) ? "comfortable and neutral" : style };
            foreach (Vector3 p in roomOutline) dto.outline.Add(new PointDto { x = p.x, z = p.z });
            foreach (DirectorItem i in items) if (i != null) dto.items.Add(i);
            return JsonUtility.ToJson(dto);
        }

        // ---------- response ----------

        /// <summary>One AI-proposed placement, before validation.</summary>
        [Serializable]
        public sealed class PlannedPlacement
        {
            public string itemId;
            public float x;
            public float z;
            public float yawDegrees;
            public string note;
        }

        /// <summary>The AI's staging plan, parsed but not yet trusted.</summary>
        public sealed class DirectorPlan
        {
            public string Summary = "";
            public List<PlannedPlacement> Placements = new List<PlannedPlacement>();
        }

        /// <summary>Parse the Edge Function response. Never throws on bad JSON — returns an empty plan.</summary>
        public static DirectorPlan ParsePlan(string json)
        {
            var plan = new DirectorPlan();
            if (string.IsNullOrEmpty(json)) return plan;

            ResponseDto dto;
            try { dto = JsonUtility.FromJson<ResponseDto>(json); }
            catch { return plan; }
            if (dto == null) return plan;

            plan.Summary = dto.summary ?? "";
            if (dto.placements != null)
                foreach (PlannedPlacement p in dto.placements)
                    if (p != null && !string.IsNullOrEmpty(p.itemId)) plan.Placements.Add(p);
            return plan;
        }

        // ---------- validation ----------

        /// <summary>A plan after geometry validation: what to place, what was dropped and why.</summary>
        public sealed class ValidatedStaging
        {
            public string Summary = "";
            public List<Placement> Accepted = new List<Placement>();
            public List<string> Rejected = new List<string>();
            /// <summary>Catalog spend of the accepted placements (user furniture is free).</summary>
            public float CatalogCostUsd;
        }

        /// <summary>
        /// Validate every proposed placement against the room. Items that fit are
        /// accepted as real <see cref="Placement"/>s (catalog vs user-furniture id
        /// set from the item source). Near-misses are nudged up to
        /// <see cref="MaxRescueNudgeM"/> toward the room centroid; placements that
        /// still don't fit, or reference unknown items, are rejected with a reason.
        /// </summary>
        public static ValidatedStaging Validate(
            IReadOnlyList<Vector3> roomOutline,
            DirectorPlan plan,
            IReadOnlyList<DirectorItem> items,
            float clearanceM = DefaultClearanceM)
        {
            var result = new ValidatedStaging { Summary = plan?.Summary ?? "" };
            if (plan == null || roomOutline == null || roomOutline.Count < 3) return result;

            var byId = new Dictionary<string, DirectorItem>();
            if (items != null)
                foreach (DirectorItem i in items)
                    if (i != null && !string.IsNullOrEmpty(i.id)) byId[i.id] = i;

            Vector3 centroid = CentroidXZ(roomOutline);

            foreach (PlannedPlacement pp in plan.Placements)
            {
                if (!byId.TryGetValue(pp.itemId, out DirectorItem item))
                {
                    result.Rejected.Add($"{pp.itemId}: not an offered item");
                    continue;
                }

                var placement = new Placement
                {
                    Position = new Vector3(pp.x, 0f, pp.z),
                    YawDegrees = pp.yawDegrees,
                    Scale = 1f,
                };
                if (item.userFurniture) placement.FurnitureAssetId = item.id;
                else placement.CatalogItemId = item.id;

                FitResult fit = FitChecker.FootprintFitsInRoom(roomOutline, placement, item.Size, clearanceM);
                if (!fit.Fits)
                {
                    // Rescue: nudge toward the room center in small steps.
                    Vector3 toCenter = centroid - placement.Position;
                    toCenter.y = 0f;
                    float dist = toCenter.magnitude;
                    if (dist > 1e-4f)
                    {
                        Vector3 dir = toCenter / dist;
                        for (float step = 0.15f; step <= MaxRescueNudgeM + 1e-4f; step += 0.15f)
                        {
                            var nudged = new Placement
                            {
                                CatalogItemId = placement.CatalogItemId,
                                FurnitureAssetId = placement.FurnitureAssetId,
                                Position = placement.Position + dir * Mathf.Min(step, dist),
                                YawDegrees = placement.YawDegrees,
                                Scale = 1f,
                            };
                            fit = FitChecker.FootprintFitsInRoom(roomOutline, nudged, item.Size, clearanceM);
                            if (fit.Fits) { placement = nudged; break; }
                        }
                    }
                }

                if (fit.Fits)
                {
                    result.Accepted.Add(placement);
                    if (!item.userFurniture) result.CatalogCostUsd += item.priceUsd;
                }
                else
                {
                    result.Rejected.Add($"{item.name ?? item.id}: {fit.Reason}");
                }
            }

            return result;
        }

        /// <summary>Polygon centroid on the floor plane (average of vertices — fine for placement rescue).</summary>
        public static Vector3 CentroidXZ(IReadOnlyList<Vector3> outline)
        {
            float x = 0f, z = 0f;
            for (int i = 0; i < outline.Count; i++) { x += outline[i].x; z += outline[i].z; }
            return new Vector3(x / outline.Count, 0f, z / outline.Count);
        }

        // ---------- DTOs ----------

        [Serializable] private sealed class PointDto { public float x; public float z; }
        [Serializable] private sealed class RequestDto
        {
            public string style;
            public List<PointDto> outline = new List<PointDto>();
            public List<DirectorItem> items = new List<DirectorItem>();
        }
        [Serializable] private sealed class ResponseDto
        {
            public string summary;
            public List<PlannedPlacement> placements;
        }
    }
}
