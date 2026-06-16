using System;
using UnityEngine;

namespace SuperRealEstate.Staging
{
    /// <summary>How a furniture asset's 3D model was produced.</summary>
    public enum CaptureMethod
    {
        ObjectCapture,
        Photogrammetry,
        Lidar,
        GaussianSplat,
        Catalog,
        Manual
    }

    /// <summary>
    /// A piece of furniture in a user's library — typically scanned from their
    /// phone — with a real-world bounding box used for fit checks. Mirrors the
    /// `furniture_assets` table.
    /// </summary>
    [Serializable]
    public sealed class FurnitureAsset
    {
        public string Id;
        public string OwnerId;
        public string Name;
        public string Category;

        /// <summary>Real-world bounding box in meters: x = width, y = height, z = depth.</summary>
        public Vector3 Size;

        public string ModelUrl;
        public string ThumbnailUrl;
        public CaptureMethod CaptureMethod;

        public FurnitureAsset() { }

        public FurnitureAsset(string id, string name, Vector3 size, string category = null)
        {
            Id = id;
            Name = name;
            Size = size;
            Category = category;
        }
    }
}
