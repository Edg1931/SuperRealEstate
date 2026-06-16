using System;

namespace SuperRealEstate.RoomMeasure
{
    /// <summary>
    /// Derived measurements for a room, all in metric units (meters / m²).
    /// Use <see cref="ImperialView"/> helpers when presenting in ft / ft².
    /// </summary>
    [Serializable]
    public readonly struct RoomMeasurements
    {
        public readonly float FloorAreaSqM;
        public readonly float CeilingAreaSqM;
        public readonly float WallAreaSqM;
        public readonly float PerimeterM;
        public readonly float CeilingHeightM;
        public readonly float VolumeCubicM;

        public RoomMeasurements(
            float floorAreaSqM,
            float wallAreaSqM,
            float perimeterM,
            float ceilingHeightM,
            float volumeCubicM)
        {
            FloorAreaSqM = floorAreaSqM;
            CeilingAreaSqM = floorAreaSqM; // flat ceiling assumption
            WallAreaSqM = wallAreaSqM;
            PerimeterM = perimeterM;
            CeilingHeightM = ceilingHeightM;
            VolumeCubicM = volumeCubicM;
        }

        private const float SqMetersPerSqFoot = 0.092903f;
        private const float MetersPerFoot = 0.3048f;

        public float FloorAreaSqFt => FloorAreaSqM / SqMetersPerSqFoot;
        public float CeilingAreaSqFt => CeilingAreaSqM / SqMetersPerSqFoot;
        public float WallAreaSqFt => WallAreaSqM / SqMetersPerSqFoot;
        public float PerimeterFt => PerimeterM / MetersPerFoot;
        public float CeilingHeightFt => CeilingHeightM / MetersPerFoot;
    }
}
