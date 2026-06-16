using System;
using UnityEngine;

namespace SuperRealEstate.Staging
{
    /// <summary>A furniture/fixture vendor whose catalog can be imported. Mirrors `vendors`.</summary>
    [Serializable]
    public sealed class Vendor
    {
        public string Id;
        public string Name;
        public string Website;
        public string LogoUrl;
    }

    /// <summary>
    /// A purchasable 3D asset from a vendor's catalog — droppable into a space
    /// just like the user's own furniture, but carrying a SKU and price so a
    /// staged item can flow into a real order. Mirrors `vendor_catalog_items`.
    /// </summary>
    [Serializable]
    public sealed class CatalogItem
    {
        public string Id;
        public string VendorId;
        public string Sku;
        public string Name;
        public string Category;

        /// <summary>Real-world bounding box in meters: x = width, y = height, z = depth.</summary>
        public Vector3 Size;

        public string ModelUrl;
        public string ThumbnailUrl;
        public float Price;
        public string Currency = "USD";

        public CatalogItem() { }

        public CatalogItem(string id, string name, Vector3 size, float price)
        {
            Id = id;
            Name = name;
            Size = size;
            Price = price;
        }
    }
}
