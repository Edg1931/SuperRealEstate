using System;
using SuperRealEstate.MaterialCost;
using SuperRealEstate.Staging;

namespace SuperRealEstate.Catalog
{
    public enum ProductKind { Material, VendorCatalogItem }

    /// <summary>
    /// A normalized, source-agnostic product the matcher scores against — built
    /// from a finish <see cref="Material"/> or a vendor <see cref="CatalogItem"/>.
    /// Keeping a flat candidate type lets the matching logic stay pure/testable.
    /// </summary>
    [Serializable]
    public sealed class ProductCandidate
    {
        public string Id;
        public ProductKind Kind;
        public string Brand;
        public string Product;      // display name / color name
        public string Sku;          // product code
        public string Category;     // paint / flooring / tile ...
        public string ColorHex;
        public float UnitCost;
        public string Unit;         // per_gallon / per_sqft / per_sqm ...
        public string BuyUrl;

        public static ProductCandidate FromMaterial(Material m) => new ProductCandidate
        {
            Id = m.Id,
            Kind = ProductKind.Material,
            Brand = m.Brand,
            Product = m.Name,
            Sku = m.ProductCode,
            Category = m.Category,
            ColorHex = m.ColorHex,
            UnitCost = m.PricePerUnit,
            Unit = m.Unit.ToString(),
            BuyUrl = m.BuyUrl,
        };

        // CatalogItem carries no brand/color; those stay null.
        public static ProductCandidate FromCatalogItem(CatalogItem c) => new ProductCandidate
        {
            Id = c.Id,
            Kind = ProductKind.VendorCatalogItem,
            Product = c.Name,
            Sku = c.Sku,
            Category = c.Category,
            UnitCost = c.Price,
        };
    }
}
