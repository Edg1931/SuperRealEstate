using System;
using System.Collections.Generic;
using UnityEngine;
using SuperRealEstate.Renovation;

namespace SuperRealEstate.Acquisition
{
    // Wire DTOs mirroring the BuildingModel JSON the edge functions emit
    // (supabase/functions/cubicasa-import + matterport-import). The JSON shape is:
    //
    //   {
    //     "id": "...",
    //     "sourceType": "cubicasa" | "matterport" | ...,
    //     "walls": [
    //       { "id": "w0", "start": {"x":0,"y":0}, "end": {"x":4,"y":0},
    //         "thicknessM": 0.1, "heightM": 2.5, "isExterior": true,
    //         "openings": [ { "id":"o0", "kind":"Door", "offsetM":1.0,
    //                          "widthM":0.9, "heightM":2.0, "sillHeightM":0 } ] }
    //     ],
    //     "rooms": [
    //       { "id":"r0", "name":"Living", "ceilingHeightM": 2.5,
    //         "floorOutline": [ {"x":0,"y":0}, {"x":4,"y":0}, ... ] }
    //     ]
    //   }
    //
    // NOTE: JsonUtility cannot deserialize a top-level JSON array, so the root is
    // always an object with "walls"/"rooms" fields. Coordinates are plan-view
    // meters on the floor plane (x = east, y = north), matching RoomMeasure.

    /// <summary>A plan-view {x,y} point in meters. Wire shape for <see cref="Vector2"/>.</summary>
    [Serializable]
    public struct PointDto
    {
        public float x;
        public float y;

        public Vector2 ToVector2() => new Vector2(x, y);
    }

    /// <summary>Wire shape for <see cref="Opening"/>.</summary>
    [Serializable]
    public sealed class OpeningDto
    {
        public string id;
        public string kind;        // "Door" | "Window" | "Passage"
        public float offsetM;
        public float widthM;
        public float heightM;
        public float sillHeightM;
    }

    /// <summary>Wire shape for <see cref="Wall"/>.</summary>
    [Serializable]
    public sealed class WallDto
    {
        public string id;
        public PointDto start;
        public PointDto end;
        public float thicknessM;
        public float heightM;
        public bool isExterior;
        public OpeningDto[] openings;
        public string leftFinishMaterialId;
        public string rightFinishMaterialId;
    }

    /// <summary>Wire shape for <see cref="RoomDef"/>.</summary>
    [Serializable]
    public sealed class RoomDto
    {
        public string id;
        public string name;
        public PointDto[] floorOutline;
        public float ceilingHeightM;
        public string floorMaterialId;
        public string ceilingMaterialId;
    }

    /// <summary>Root wire shape mapping to <see cref="BuildingModel"/>.</summary>
    [Serializable]
    public sealed class BuildingModelDto
    {
        public string id;
        public string sourceType;
        public WallDto[] walls;
        public RoomDto[] rooms;
    }

    /// <summary>
    /// Pure, network-free conversion from the edge-function JSON to an editable
    /// <see cref="BuildingModel"/>. Both <see cref="CubiCasaImporter"/> and
    /// <see cref="MatterportImporter"/> share this so the parsing logic is
    /// exercised by unit tests without hitting Supabase.
    ///
    /// Defensive by design: empty or malformed JSON yields a safe empty model
    /// rather than throwing, so a bad capture never crashes the import flow.
    /// </summary>
    public static class BuildingModelParser
    {
        /// <summary>
        /// Parses edge-function BuildingModel JSON into a <see cref="BuildingModel"/>.
        /// Returns an empty (but non-null) model for null/empty/malformed input.
        /// </summary>
        public static BuildingModel Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return EmptyModel();

            BuildingModelDto dto;
            try
            {
                dto = JsonUtility.FromJson<BuildingModelDto>(json);
            }
            catch (Exception)
            {
                // JsonUtility throws on structurally invalid JSON — treat as empty.
                return EmptyModel();
            }

            if (dto == null) return EmptyModel();
            return FromDto(dto);
        }

        /// <summary>Maps an already-deserialized DTO to a <see cref="BuildingModel"/>.</summary>
        public static BuildingModel FromDto(BuildingModelDto dto)
        {
            var model = EmptyModel();
            if (dto == null) return model;

            if (!string.IsNullOrEmpty(dto.id)) model.Id = dto.id;
            if (!string.IsNullOrEmpty(dto.sourceType)) model.SourceType = dto.sourceType;

            if (dto.walls != null)
            {
                foreach (var w in dto.walls)
                {
                    if (w == null) continue;
                    model.Walls.Add(ToWall(w));
                }
            }

            if (dto.rooms != null)
            {
                foreach (var r in dto.rooms)
                {
                    if (r == null) continue;
                    model.Rooms.Add(ToRoom(r));
                }
            }

            return model;
        }

        private static Wall ToWall(WallDto dto)
        {
            var wall = new Wall
            {
                Id = dto.id,
                Start = dto.start.ToVector2(),
                End = dto.end.ToVector2(),
                ThicknessM = dto.thicknessM > 0f ? dto.thicknessM : 0.1f,
                HeightM = dto.heightM > 0f ? dto.heightM : 2.5f,
                IsExterior = dto.isExterior,
                LeftFinishMaterialId = dto.leftFinishMaterialId,
                RightFinishMaterialId = dto.rightFinishMaterialId,
                Openings = new List<Opening>(),
            };

            if (dto.openings != null)
            {
                foreach (var o in dto.openings)
                {
                    if (o == null) continue;
                    wall.Openings.Add(new Opening
                    {
                        Id = o.id,
                        Kind = ParseEnum(o.kind, OpeningKind.Door),
                        OffsetM = o.offsetM,
                        WidthM = o.widthM,
                        HeightM = o.heightM,
                        SillHeightM = o.sillHeightM,
                    });
                }
            }

            return wall;
        }

        private static RoomDef ToRoom(RoomDto dto)
        {
            var room = new RoomDef
            {
                Id = dto.id,
                Name = string.IsNullOrEmpty(dto.name) ? "Room" : dto.name,
                CeilingHeightM = dto.ceilingHeightM > 0f ? dto.ceilingHeightM : 2.5f,
                FloorMaterialId = dto.floorMaterialId,
                CeilingMaterialId = dto.ceilingMaterialId,
                FloorOutline = new List<Vector2>(),
            };

            if (dto.floorOutline != null)
            {
                foreach (var p in dto.floorOutline)
                    room.FloorOutline.Add(p.ToVector2());
            }

            return room;
        }

        private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
        {
            return Enum.TryParse(value, ignoreCase: true, out TEnum parsed) ? parsed : fallback;
        }

        private static BuildingModel EmptyModel() => new BuildingModel
        {
            Walls = new List<Wall>(),
            Rooms = new List<RoomDef>(),
        };
    }
}
