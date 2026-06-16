using System;
using System.Collections.Generic;
using SuperRealEstate.RoomMeasure;

namespace SuperRealEstate.MaterialCost
{
    /// <summary>A single priced surface within an estimate.</summary>
    [Serializable]
    public readonly struct CostLineItem
    {
        public readonly SurfaceType Surface;
        public readonly Material Material;
        public readonly float Quantity;     // in the material's unit
        public readonly float WasteFactor;  // e.g. 0.10 = 10% overage
        public readonly float Subtotal;     // currency

        public CostLineItem(SurfaceType surface, Material material, float quantity, float wasteFactor, float subtotal)
        {
            Surface = surface;
            Material = material;
            Quantity = quantity;
            WasteFactor = wasteFactor;
            Subtotal = subtotal;
        }
    }

    /// <summary>
    /// Turns room measurements + a chosen <see cref="Material"/> into cost.
    /// Pure math: <c>subtotal = quantity × (1 + waste) × pricePerUnit</c>.
    /// </summary>
    public static class CostEstimator
    {
        /// <summary>Sensible default overage per surface (cuts, breakage, trimming).</summary>
        public static float DefaultWasteFactor(SurfaceType surface) => surface switch
        {
            SurfaceType.Floor => 0.10f,
            SurfaceType.Walls => 0.05f,
            SurfaceType.Ceiling => 0.05f,
            SurfaceType.Trim => 0.10f,
            _ => 0.10f
        };

        /// <summary>
        /// Quantity of a material needed for a surface, expressed in that
        /// material's quoting unit, before waste.
        /// </summary>
        public static float QuantityFor(SurfaceType surface, MaterialUnit unit, RoomMeasurements m)
        {
            switch (unit)
            {
                case MaterialUnit.PerSquareFoot:
                    return AreaSqFt(surface, m);
                case MaterialUnit.PerSquareMeter:
                    return AreaSqM(surface, m);
                case MaterialUnit.PerLinearFoot:
                    return m.PerimeterFt; // trim / baseboard
                case MaterialUnit.Each:
                    return 1f;
                default:
                    throw new ArgumentOutOfRangeException(nameof(unit), unit, null);
            }
        }

        /// <summary>Estimate one surface. Pass a negative waste to use the default.</summary>
        public static CostLineItem Estimate(SurfaceType surface, Material material, RoomMeasurements m, float wasteFactor = -1f)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (wasteFactor < 0f) wasteFactor = DefaultWasteFactor(surface);

            float quantity = QuantityFor(surface, material.Unit, m);
            float subtotal = quantity * (1f + wasteFactor) * material.PricePerUnit;
            return new CostLineItem(surface, material, quantity, wasteFactor, subtotal);
        }

        /// <summary>Sum the subtotals of several line items.</summary>
        public static float Total(IEnumerable<CostLineItem> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            float total = 0f;
            foreach (var item in items) total += item.Subtotal;
            return total;
        }

        private static float AreaSqFt(SurfaceType surface, RoomMeasurements m) => surface switch
        {
            SurfaceType.Floor => m.FloorAreaSqFt,
            SurfaceType.Ceiling => m.CeilingAreaSqFt,
            SurfaceType.Walls => m.WallAreaSqFt,
            SurfaceType.Trim => m.PerimeterFt,
            _ => 0f
        };

        private static float AreaSqM(SurfaceType surface, RoomMeasurements m) => surface switch
        {
            SurfaceType.Floor => m.FloorAreaSqM,
            SurfaceType.Ceiling => m.CeilingAreaSqM,
            SurfaceType.Walls => m.WallAreaSqM,
            SurfaceType.Trim => m.PerimeterM,
            _ => 0f
        };
    }
}
