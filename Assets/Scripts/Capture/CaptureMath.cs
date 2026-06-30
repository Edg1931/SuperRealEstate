using System;

namespace SuperRealEstate.Capture
{
    /// <summary>
    /// Pure geometry for capture — turning a camera position relative to the object
    /// center into the <see cref="CaptureView"/> (azimuth + elevation) the coverage
    /// coach needs. Floats in, no Unity dependency, so it's unit-tested. The AR
    /// layer passes camera + object-center positions as plain coordinates.
    /// Convention: +Z = north (azimuth 0), +X = east (azimuth 90), +Y = up.
    /// </summary>
    public static class CaptureMath
    {
        /// <summary>The view angle of a camera at (px,py,pz) looking at an object centered at (cx,cy,cz).</summary>
        public static CaptureView ViewFrom(
            float cx, float cy, float cz,
            float px, float py, float pz)
        {
            float dx = px - cx;
            float dy = py - cy;
            float dz = pz - cz;

            float horiz = (float)Math.Sqrt(dx * dx + dz * dz);
            float azimuth = (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI); // bearing from +Z, clockwise
            float elevation = horiz > 1e-5f
                ? (float)(Math.Atan2(dy, horiz) * 180.0 / Math.PI)
                : (dy >= 0f ? 90f : -90f);

            return new CaptureView(azimuth, elevation);
        }
    }
}
