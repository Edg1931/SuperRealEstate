using System;
using System.Collections.Generic;

namespace SuperRealEstate.Capture
{
    /// <summary>One photo's viewing direction: where the camera sat relative to the
    /// object center, as a compass azimuth (0–360, clockwise) + elevation above
    /// the object (0 = level, 90 = straight down from above).</summary>
    public readonly struct CaptureView
    {
        public readonly float AzimuthDeg;
        public readonly float ElevationDeg;

        public CaptureView(float azimuthDeg, float elevationDeg)
        {
            AzimuthDeg = Normalize360(azimuthDeg);
            ElevationDeg = elevationDeg;
        }

        internal static float Normalize360(float deg)
        {
            deg %= 360f;
            return deg < 0f ? deg + 360f : deg;
        }
    }

    /// <summary>
    /// The guided-capture "brain": tracks which views around an object have been
    /// photographed and tells the user where to move next — the single biggest
    /// driver of reconstruction quality, and where the best scanning apps (Polycam,
    /// RealityScan, Kiri) win or lose. We bin the viewing sphere into azimuth
    /// sectors × elevation bands; a bin is covered once any photo lands in it.
    /// Coverage % + the list of missing sectors drive a live "move left / capture
    /// the back / tilt up" coach. Pure + unit-tested (no camera, no Unity).
    /// </summary>
    public sealed class CaptureCoverage
    {
        private readonly int _azimuthSectors;
        private readonly int _elevationBands;
        private readonly float _bandMaxElevation; // bands span [0, bandMax]
        private readonly bool[,] _covered;        // [azimuthSector, elevationBand]
        private readonly int[,] _counts;

        public int PhotoCount { get; private set; }

        public CaptureCoverage(int azimuthSectors = 12, int elevationBands = 2, float bandMaxElevationDeg = 70f)
        {
            _azimuthSectors = Math.Max(1, azimuthSectors);
            _elevationBands = Math.Max(1, elevationBands);
            _bandMaxElevation = bandMaxElevationDeg <= 0f ? 70f : bandMaxElevationDeg;
            _covered = new bool[_azimuthSectors, _elevationBands];
            _counts = new int[_azimuthSectors, _elevationBands];
        }

        /// <summary>Total bins (azimuth sectors × elevation bands).</summary>
        public int TotalBins => _azimuthSectors * _elevationBands;

        /// <summary>Record a captured view.</summary>
        public void Add(CaptureView view)
        {
            int a = AzimuthSector(view.AzimuthDeg);
            int e = ElevationBand(view.ElevationDeg);
            _covered[a, e] = true;
            _counts[a, e]++;
            PhotoCount++;
        }

        /// <summary>Fraction of bins covered, 0..1.</summary>
        public float Coverage01
        {
            get
            {
                int covered = 0;
                foreach (bool b in _covered) if (b) covered++;
                return TotalBins == 0 ? 0f : (float)covered / TotalBins;
            }
        }

        /// <summary>Ready to reconstruct when coverage + photo count clear the thresholds.</summary>
        public bool IsReady(int minPhotos = 20, float minCoverage = 0.8f)
            => PhotoCount >= minPhotos && Coverage01 >= minCoverage;

        /// <summary>
        /// Azimuth centers (deg) of sectors that still have an uncovered elevation
        /// band — i.e. "go shoot here". Ordered by azimuth.
        /// </summary>
        public IReadOnlyList<float> MissingAzimuthCenters()
        {
            var list = new List<float>();
            float sectorWidth = 360f / _azimuthSectors;
            for (int a = 0; a < _azimuthSectors; a++)
            {
                bool anyMissing = false;
                for (int e = 0; e < _elevationBands; e++)
                    if (!_covered[a, e]) { anyMissing = true; break; }
                if (anyMissing) list.Add(a * sectorWidth + sectorWidth * 0.5f);
            }
            return list;
        }

        /// <summary>
        /// A short, human coaching line for the next move (e.g. "Move around to ~210°"
        /// / "Capture a higher angle"), or "" when ready. UI maps the azimuth to a
        /// left/right/behind arrow relative to the user's current heading.
        /// </summary>
        public string NextGuidance(float userHeadingDeg = 0f)
        {
            if (IsReady()) return "";
            IReadOnlyList<float> missing = MissingAzimuthCenters();
            if (missing.Count == 0) return "Capture a few more angles";

            // Nearest missing sector to where the user is looking → least walking.
            float best = missing[0];
            float bestDelta = 360f;
            foreach (float az in missing)
            {
                float d = Math.Abs(SignedDelta(az, userHeadingDeg));
                if (d < bestDelta) { bestDelta = d; best = az; }
            }
            return $"Move around to about {best:0}° and keep the object centered";
        }

        private int AzimuthSector(float azimuthDeg)
        {
            float w = 360f / _azimuthSectors;
            int s = (int)(azimuthDeg / w);
            return s < 0 ? 0 : (s >= _azimuthSectors ? _azimuthSectors - 1 : s);
        }

        private int ElevationBand(float elevationDeg)
        {
            float e = elevationDeg < 0f ? 0f : (elevationDeg > _bandMaxElevation ? _bandMaxElevation : elevationDeg);
            float w = _bandMaxElevation / _elevationBands;
            int b = (int)(e / w);
            return b < 0 ? 0 : (b >= _elevationBands ? _elevationBands - 1 : b);
        }

        /// <summary>Signed smallest angle from b to a, in [-180,180].</summary>
        private static float SignedDelta(float a, float b)
        {
            float d = CaptureView.Normalize360(a - b);
            return d > 180f ? d - 360f : d;
        }
    }
}
