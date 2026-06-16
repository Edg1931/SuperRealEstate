using System;

namespace SuperRealEstate.MaterialCost
{
    /// <summary>How a material's price is quoted.</summary>
    public enum MaterialUnit
    {
        PerSquareFoot,
        PerSquareMeter,
        PerLinearFoot,
        PerGallon,   // paint (priced by the gallon; quantity via FinishQuantity)
        Each
    }

    /// <summary>A surface in the room a material can be applied to.</summary>
    public enum SurfaceType
    {
        Floor,
        Walls,
        Ceiling,
        Trim
    }

    /// <summary>
    /// A purchasable building material with an editable quoted price. Prices
    /// come from the Supabase catalog (seeded manually — there is no official
    /// Lowe's/Home Depot pricing API), so they are expected to be updated.
    /// </summary>
    [Serializable]
    public sealed class Material
    {
        public string Id;
        public string Name;
        public string Category;
        public MaterialUnit Unit;
        public float PricePerUnit;

        // Finish-catalog fields (let AI-recognized finishes resolve to a real
        // product with a buy link). E.g. brand "Sherwin-Williams",
        // ProductCode "SW 7029", ColorHex "#D1CBC1".
        public string Brand;
        public string ProductCode;
        public string ColorHex;
        public string BuyUrl;

        public Material() { }

        public Material(string id, string name, string category, MaterialUnit unit, float pricePerUnit)
        {
            Id = id;
            Name = name;
            Category = category;
            Unit = unit;
            PricePerUnit = pricePerUnit;
        }
    }
}
