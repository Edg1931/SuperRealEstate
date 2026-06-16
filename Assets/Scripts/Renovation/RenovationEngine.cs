using System;
using System.Collections.Generic;
using System.Linq;
using SuperRealEstate.MaterialCost;

namespace SuperRealEstate.Renovation
{
    /// <summary>
    /// Applies a <see cref="RenovationPlan"/>'s edits to a base
    /// <see cref="BuildingModel"/> to produce the renovated model, and costs a
    /// whole plan. This is the core the desktop 3D editor drives: edits are the
    /// undo/redo history and the wire format; the base model is never mutated
    /// (before/after toggle = render base vs. Apply(base, plan)). Pure + tested.
    /// </summary>
    public static class RenovationEngine
    {
        /// <summary>Return a new model with all edits applied in order. Base is untouched.</summary>
        public static BuildingModel Apply(BuildingModel baseModel, RenovationPlan plan)
        {
            if (baseModel == null) throw new ArgumentNullException(nameof(baseModel));
            var model = Clone(baseModel);
            if (plan?.Edits == null) return model;

            foreach (var edit in plan.Edits) ApplyEdit(model, edit);
            return model;
        }

        private static void ApplyEdit(BuildingModel model, RenovationEdit e)
        {
            switch (e.Kind)
            {
                case EditKind.RemoveWall:
                    model.Walls.RemoveAll(w => w.Id == e.TargetId);
                    break;

                case EditKind.AddWall:
                    model.Walls.Add(new Wall
                    {
                        Id = e.TargetId ?? Guid.NewGuid().ToString(),
                        Start = e.Start,
                        End = e.End,
                        HeightM = e.HeightM > 0 ? e.HeightM : 2.5f,
                        ThicknessM = e.ThicknessM > 0 ? e.ThicknessM : 0.1f,
                    });
                    break;

                case EditKind.MoveWall:
                    foreach (var w in model.Walls.Where(w => w.Id == e.TargetId))
                    {
                        w.Start = e.Start;
                        w.End = e.End;
                    }
                    break;

                case EditKind.RemoveOpening:
                    foreach (var w in model.Walls) w.Openings.RemoveAll(o => o.Id == e.TargetId);
                    break;

                case EditKind.AddOpening:
                    foreach (var w in model.Walls.Where(w => w.Id == e.TargetId))
                        w.Openings.Add(new Opening
                        {
                            Id = Guid.NewGuid().ToString(),
                            Kind = OpeningKind.Passage,
                            OffsetM = e.Start.x,
                            WidthM = e.Value,
                            HeightM = e.HeightM > 0 ? e.HeightM : 2.0f,
                        });
                    break;

                case EditKind.ChangeCeilingHeight:
                    foreach (var r in model.Rooms.Where(r => r.Id == e.TargetId)) r.CeilingHeightM = e.Value;
                    break;

                case EditKind.ChangeFloorFinish:
                    foreach (var r in model.Rooms.Where(r => r.Id == e.TargetId)) r.FloorMaterialId = e.MaterialId;
                    break;

                case EditKind.ChangeCeilingFinish:
                    foreach (var r in model.Rooms.Where(r => r.Id == e.TargetId)) r.CeilingMaterialId = e.MaterialId;
                    break;

                case EditKind.ChangeWallFinish:
                    foreach (var w in model.Walls.Where(w => w.Id == e.TargetId))
                    {
                        w.LeftFinishMaterialId = e.MaterialId;
                        w.RightFinishMaterialId = e.MaterialId;
                    }
                    break;
            }
        }

        /// <summary>
        /// Cost a whole plan against the base model: wall demolition (with a
        /// load-bearing beam line for exterior walls) + finish-change costs.
        /// <paramref name="resolveMaterial"/> looks up a material by id.
        /// </summary>
        public static List<RenovationLineItem> EstimatePlanCost(
            BuildingModel baseModel,
            RenovationPlan plan,
            Func<string, Material> resolveMaterial,
            DemolitionRates rates = null)
        {
            if (baseModel == null) throw new ArgumentNullException(nameof(baseModel));
            if (resolveMaterial == null) throw new ArgumentNullException(nameof(resolveMaterial));
            rates ??= new DemolitionRates();

            var items = new List<RenovationLineItem>();
            if (plan?.Edits == null) return items;

            foreach (var e in plan.Edits)
            {
                switch (e.Kind)
                {
                    case EditKind.RemoveWall:
                        var wall = baseModel.Walls.FirstOrDefault(w => w.Id == e.TargetId);
                        if (wall != null)
                            items.AddRange(RenovationCostEstimator.WallRemoval(
                                wall.SideAreaM2, wall.LengthM, wall.IsExterior, rates));
                        break;

                    case EditKind.ChangeWallFinish:
                        AddFinish(items, "Refinish wall", WallArea(baseModel, e.TargetId), resolveMaterial(e.MaterialId));
                        break;

                    case EditKind.ChangeFloorFinish:
                        AddFinish(items, "New flooring", RoomFloorArea(baseModel, e.TargetId), resolveMaterial(e.MaterialId));
                        break;

                    case EditKind.ChangeCeilingFinish:
                        AddFinish(items, "Refinish ceiling", RoomFloorArea(baseModel, e.TargetId), resolveMaterial(e.MaterialId));
                        break;
                }
            }
            return items;
        }

        private static void AddFinish(List<RenovationLineItem> items, string desc, float areaSqM, Material material)
        {
            if (material == null || areaSqM <= 0f) return;
            items.Add(RenovationCostEstimator.FinishChange(desc, areaSqM, material));
        }

        private static float WallArea(BuildingModel m, string wallId)
            => m.Walls.FirstOrDefault(w => w.Id == wallId)?.SideAreaM2 ?? 0f;

        private static float RoomFloorArea(BuildingModel m, string roomId)
            => m.Rooms.FirstOrDefault(r => r.Id == roomId)?.FloorAreaM2 ?? 0f;

        // --- deep clone so edits never mutate the base model ---

        private static BuildingModel Clone(BuildingModel src) => new BuildingModel
        {
            Id = src.Id,
            SourceType = src.SourceType,
            Walls = src.Walls.Select(CloneWall).ToList(),
            Rooms = src.Rooms.Select(CloneRoom).ToList(),
        };

        private static Wall CloneWall(Wall w) => new Wall
        {
            Id = w.Id, Start = w.Start, End = w.End,
            ThicknessM = w.ThicknessM, HeightM = w.HeightM, IsExterior = w.IsExterior,
            LeftFinishMaterialId = w.LeftFinishMaterialId,
            RightFinishMaterialId = w.RightFinishMaterialId,
            Openings = w.Openings.Select(o => new Opening
            {
                Id = o.Id, Kind = o.Kind, OffsetM = o.OffsetM,
                WidthM = o.WidthM, HeightM = o.HeightM, SillHeightM = o.SillHeightM,
            }).ToList(),
        };

        private static RoomDef CloneRoom(RoomDef r) => new RoomDef
        {
            Id = r.Id, Name = r.Name, CeilingHeightM = r.CeilingHeightM,
            FloorMaterialId = r.FloorMaterialId, CeilingMaterialId = r.CeilingMaterialId,
            FloorOutline = new List<UnityEngine.Vector2>(r.FloorOutline),
        };
    }
}
