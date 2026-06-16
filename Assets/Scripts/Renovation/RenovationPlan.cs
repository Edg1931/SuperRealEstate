using System;
using System.Collections.Generic;
using UnityEngine;

namespace SuperRealEstate.Renovation
{
    public enum EditKind
    {
        RemoveWall,
        AddWall,
        MoveWall,
        AddOpening,
        RemoveOpening,
        ChangeFloorFinish,
        ChangeWallFinish,
        ChangeCeilingFinish,
        ChangeCeilingHeight
    }

    /// <summary>
    /// One non-destructive renovation operation. Storing edits as a list (rather
    /// than mutating the base model) is what makes before/after toggling clean,
    /// edits shareable across a session, and the whole plan undoable.
    /// </summary>
    [Serializable]
    public sealed class RenovationEdit
    {
        public string Id;
        public EditKind Kind;
        public string TargetId;     // wall / room / opening id this edit acts on
        public string MaterialId;   // finish changes
        public float Value;         // e.g. new ceiling height (m)

        // AddWall / MoveWall geometry
        public Vector2 Start;
        public Vector2 End;
        public float HeightM;
        public float ThicknessM;
    }

    /// <summary>
    /// A renovation = a base <see cref="BuildingModel"/> + an ordered edit list.
    /// Mirrors `renovation_plans`. Apply the edits to derive the renovated model
    /// for rendering / costing; keep the base untouched for before/after.
    /// </summary>
    [Serializable]
    public sealed class RenovationPlan
    {
        public string Id;
        public string BuildingModelId;
        public string Name = "Renovation";
        public List<RenovationEdit> Edits = new List<RenovationEdit>();
    }
}
