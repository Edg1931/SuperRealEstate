using System;
using UnityEngine;

namespace SuperRealEstate.Staging
{
    /// <summary>
    /// A floor plan authored at a desktop, with a real-world scale. Placements
    /// are designed in blueprint (plan) coordinates, then registered to the
    /// real space on-site via <see cref="BlueprintTransform"/>. Mirrors
    /// `blueprints`.
    /// </summary>
    [Serializable]
    public sealed class Blueprint
    {
        public string Id;
        public string Name = "Blueprint";
        public string ImageUrl;
        public float PlanWidth;
        public float PlanHeight;

        /// <summary>Plan units → meters (informational; on-site 2-point solve overrides).</summary>
        public float MetersPerUnit = 1f;
    }

    /// <summary>
    /// An item placed in blueprint (plan) space. X/Y are plan coordinates; yaw
    /// is in the plan plane. Converted to a world-space <see cref="Placement"/>
    /// by <see cref="BlueprintTransform"/> once the blueprint is registered.
    /// </summary>
    [Serializable]
    public sealed class BlueprintPlacement
    {
        public string Id;
        public string FurnitureAssetId;
        public string CatalogItemId;
        public Vector2 PlanPosition;
        public float PlanYawDegrees;
        public float Scale = 1f;
    }

    /// <summary>
    /// Maps blueprint (plan) coordinates to world space. Solved from two
    /// corresponding point pairs picked on-site — two known ground points (e.g.
    /// survey stakes or corner marks) matched to two blueprint points — which
    /// fixes position, rotation, and scale **without** relying on wall
    /// detection. This is what makes new-construction pre-visualization work on
    /// a bare slab.
    ///
    /// Plan coordinates (x, y) map to the world floor plane: plan-x → world-X,
    /// plan-y → world-Z, at a fixed floor elevation. Pure math, unit-tested.
    /// </summary>
    public readonly struct BlueprintTransform
    {
        public readonly float Scale;
        public readonly float RotationDegrees;   // yaw about world up axis
        public readonly Vector3 Translation;     // includes floor elevation

        private BlueprintTransform(float scale, float rotationDegrees, Vector3 translation)
        {
            Scale = scale;
            RotationDegrees = rotationDegrees;
            Translation = translation;
        }

        /// <summary>
        /// Solve from two plan points and the two world points (world X/Z) they
        /// correspond to, at floor elevation <paramref name="floorY"/>.
        /// </summary>
        public static BlueprintTransform Solve(Vector2 planA, Vector2 planB, Vector2 worldA, Vector2 worldB, float floorY = 0f)
        {
            Vector3 planDelta = PlanToFlat(planB) - PlanToFlat(planA);
            Vector3 worldDelta = new Vector3(worldB.x - worldA.x, 0f, worldB.y - worldA.y);

            float planLen = planDelta.magnitude;
            if (planLen < 1e-6f)
                throw new ArgumentException("Blueprint reference points must be distinct.");

            float scale = worldDelta.magnitude / planLen;
            float yaw = Vector3.SignedAngle(planDelta, worldDelta, Vector3.up);

            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 worldA3 = new Vector3(worldA.x, floorY, worldA.y);
            Vector3 translation = worldA3 - (scale * (rot * PlanToFlat(planA)));

            return new BlueprintTransform(scale, yaw, translation);
        }

        /// <summary>Map a blueprint point to a world-space position.</summary>
        public Vector3 ToWorld(Vector2 planPoint)
        {
            Quaternion rot = Quaternion.Euler(0f, RotationDegrees, 0f);
            return Translation + (Scale * (rot * PlanToFlat(planPoint)));
        }

        /// <summary>
        /// Convert a blueprint-authored placement into a world-space placement.
        /// Position is mapped through the transform; item yaw composes with the
        /// blueprint's rotation; the item keeps its real-world size (Scale is
        /// the item's own scale, not multiplied by the blueprint scale).
        /// </summary>
        public Placement ToWorldPlacement(BlueprintPlacement source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new Placement
            {
                Id = source.Id,
                FurnitureAssetId = source.FurnitureAssetId,
                CatalogItemId = source.CatalogItemId,
                Position = ToWorld(source.PlanPosition),
                YawDegrees = source.PlanYawDegrees + RotationDegrees,
                Scale = source.Scale,
            };
        }

        private static Vector3 PlanToFlat(Vector2 plan) => new Vector3(plan.x, 0f, plan.y);
    }
}
