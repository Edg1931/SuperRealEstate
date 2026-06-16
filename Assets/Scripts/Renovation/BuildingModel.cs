using System;
using System.Collections.Generic;
using UnityEngine;
using SuperRealEstate.RoomMeasure;

namespace SuperRealEstate.Renovation
{
    public enum OpeningKind { Door, Window, Passage }

    /// <summary>A door / window / passage cut into a wall.</summary>
    [Serializable]
    public sealed class Opening
    {
        public string Id;
        public OpeningKind Kind;
        public float OffsetM;     // along the wall from Start
        public float WidthM;
        public float HeightM;
        public float SillHeightM; // windows

        public float AreaM2 => Mathf.Max(0f, WidthM) * Mathf.Max(0f, HeightM);
    }

    /// <summary>
    /// A wall as a first-class, editable object (not just mesh). Plan
    /// coordinates are in meters on the floor plane. Each side can carry a
    /// finish material id (paint, tile, etc.).
    /// </summary>
    [Serializable]
    public sealed class Wall
    {
        public string Id;
        public Vector2 Start;
        public Vector2 End;
        public float ThicknessM = 0.1f;
        public float HeightM = 2.5f;
        public bool IsExterior;
        public List<Opening> Openings = new List<Opening>();
        public string LeftFinishMaterialId;
        public string RightFinishMaterialId;

        public float LengthM => Vector2.Distance(Start, End);

        /// <summary>Paintable/finishable area of one face, minus openings (m²).</summary>
        public float SideAreaM2
        {
            get
            {
                float gross = LengthM * Mathf.Max(0f, HeightM);
                float openings = 0f;
                foreach (var o in Openings) openings += o.AreaM2;
                return Mathf.Max(0f, gross - openings);
            }
        }
    }

    /// <summary>A bounded room with editable finishes. Outline in plan meters.</summary>
    [Serializable]
    public sealed class RoomDef
    {
        public string Id;
        public string Name = "Room";
        public List<Vector2> FloorOutline = new List<Vector2>();
        public float CeilingHeightM = 2.5f;
        public string FloorMaterialId;
        public string CeilingMaterialId;

        public float FloorAreaM2
        {
            get
            {
                if (FloorOutline == null || FloorOutline.Count < 3) return 0f;
                var pts = new List<Vector3>(FloorOutline.Count);
                foreach (var p in FloorOutline) pts.Add(new Vector3(p.x, 0f, p.y));
                return MeasurementService.FloorArea(pts); // reuse the tested shoelace
            }
        }
    }

    /// <summary>
    /// An editable structural model of a home, imported from a capture
    /// (Matterport / CubiCasa / RoomPlan) or built from an AR scan. The
    /// substrate renovation edits operate on.
    /// </summary>
    [Serializable]
    public sealed class BuildingModel
    {
        public string Id;
        public string SourceType; // matterport | cubicasa | roomplan | ar_scan | manual
        public List<Wall> Walls = new List<Wall>();
        public List<RoomDef> Rooms = new List<RoomDef>();
    }
}
