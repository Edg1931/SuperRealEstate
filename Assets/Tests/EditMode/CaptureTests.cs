using NUnit.Framework;
using SuperRealEstate.Capture;

namespace SuperRealEstate.Tests
{
    public class CaptureCoverageTests
    {
        [Test]
        public void Empty_HasZeroCoverage()
        {
            var c = new CaptureCoverage(12, 2);
            Assert.AreEqual(0f, c.Coverage01, 0.001f);
            Assert.AreEqual(24, c.TotalBins);
            Assert.IsFalse(c.IsReady());
        }

        [Test]
        public void Orbiting_AllAround_BothBands_IsReady()
        {
            var c = new CaptureCoverage(12, 2, bandMaxElevationDeg: 70f);
            // One photo per azimuth sector in each of the two elevation bands.
            for (int s = 0; s < 12; s++)
            {
                float az = s * 30f + 15f;
                c.Add(new CaptureView(az, 20f)); // low band
                c.Add(new CaptureView(az, 55f)); // high band
            }
            Assert.AreEqual(1f, c.Coverage01, 0.001f);
            Assert.AreEqual(24, c.PhotoCount);
            Assert.IsTrue(c.IsReady(minPhotos: 20, minCoverage: 0.8f));
            Assert.AreEqual(0, c.MissingAzimuthCenters().Count);
        }

        [Test]
        public void PartialOrbit_ReportsMissingSectors()
        {
            var c = new CaptureCoverage(12, 1, bandMaxElevationDeg: 70f);
            // Only the front half (0..180) captured.
            for (int s = 0; s < 6; s++) c.Add(new CaptureView(s * 30f + 15f, 20f));
            Assert.That(c.Coverage01, Is.EqualTo(0.5f).Within(0.01f));
            var missing = c.MissingAzimuthCenters();
            Assert.AreEqual(6, missing.Count);          // the back half
            Assert.Greater(missing[0], 180f);            // missing sectors are behind
            Assert.IsNotEmpty(c.NextGuidance());         // coaches toward a gap
        }

        [Test]
        public void Azimuth_WrapsAndNormalizes()
        {
            var c = new CaptureCoverage(4, 1);
            c.Add(new CaptureView(-90f, 10f)); // == 270°
            c.Add(new CaptureView(450f, 10f)); // == 90°
            Assert.That(c.Coverage01, Is.EqualTo(0.5f).Within(0.01f));
        }
    }

    public class CaptureBoundsTests
    {
        [Test]
        public void Sofa_FitsThroughStandardDoor_WhenTurned()
        {
            // A 2.1m × 0.9m × 0.85m sofa through a 0.81m × 2.03m (32"×80") door.
            var sofa = new CaptureBounds(2.1f, 0.9f, 0.85f);
            // Smallest two dims (0.85, 0.9) vs door (0.81, 2.03): 0.85 > 0.81 → no.
            Assert.IsFalse(sofa.FitsThroughOpening(0.81f, 2.03f));
            // A slimmer loveseat (1.5 × 0.8 × 0.8) does fit.
            var loveseat = new CaptureBounds(1.5f, 0.8f, 0.8f);
            Assert.IsTrue(loveseat.FitsThroughOpening(0.81f, 2.03f));
        }

        [Test]
        public void Footprint_And_Volume()
        {
            var b = new CaptureBounds(2f, 1f, 0.5f);
            Assert.AreEqual(2f, b.FootprintAreaSqM, 0.001f);
            Assert.AreEqual(1f, b.VolumeCubicM, 0.001f);
            Assert.AreEqual(2f, b.LongestEdgeM, 0.001f);
            Assert.IsTrue(b.IsValid);
        }
    }

    public class CaptureSessionTests
    {
        private static byte[] Frame() => new byte[] { 1, 2, 3 };

        [Test]
        public void Lifecycle_CapturesUntilReady_ThenSubmits()
        {
            var s = new CaptureSession("Client sofa", 12, 2, minPhotos: 24, minCoverage: 0.8f);
            Assert.AreEqual(CaptureStatus.Idle, s.Status);

            s.Begin();
            Assert.AreEqual(CaptureStatus.Capturing, s.Status);

            for (int sector = 0; sector < 12; sector++)
            {
                s.AddPhoto(new CaptureView(sector * 30f + 15f, 20f), Frame());
                s.AddPhoto(new CaptureView(sector * 30f + 15f, 55f), Frame());
            }

            Assert.AreEqual(CaptureStatus.ReadyToProcess, s.Status);
            Assert.IsTrue(s.CanProcess);

            s.SetBounds(new CaptureBounds(2.0f, 0.9f, 0.85f));
            CaptureSubmission sub = s.BuildSubmission("user-1", CaptureMethod.Photogrammetry);
            Assert.AreEqual(CaptureStatus.Processing, s.Status);
            Assert.AreEqual(24, sub.Photos.Count);
            Assert.IsTrue(sub.MetricBounds.HasValue);

            s.MarkReady(new FurnitureCaptureResult { AssetId = "a1", ModelUrl = "u", Bounds = new CaptureBounds(2f, 0.9f, 0.85f) });
            Assert.AreEqual(CaptureStatus.Ready, s.Status);
        }

        [Test]
        public void AddPhoto_RejectedWhenNotCapturing_OrEmpty()
        {
            var s = new CaptureSession();
            Assert.IsFalse(s.AddPhoto(new CaptureView(0f, 10f), Frame())); // Idle
            s.Begin();
            Assert.IsFalse(s.AddPhoto(new CaptureView(0f, 10f), null));    // empty frame
            Assert.IsTrue(s.AddPhoto(new CaptureView(0f, 10f), Frame()));
        }

        [Test]
        public void BuildSubmission_Throws_WhenNotReady()
        {
            var s = new CaptureSession(minPhotos: 20, minCoverage: 0.8f);
            s.Begin();
            s.AddPhoto(new CaptureView(0f, 10f), Frame());
            Assert.IsFalse(s.CanProcess);
            Assert.Throws<System.InvalidOperationException>(() => s.BuildSubmission("u", CaptureMethod.Photogrammetry));
        }

        [Test]
        public void MarkReady_AdoptsReconstructedScale_WhenNoArBounds()
        {
            var s = new CaptureSession(minPhotos: 1, minCoverage: 0f);
            s.Begin();
            s.AddPhoto(new CaptureView(0f, 10f), Frame());
            CaptureSubmission _ = s.BuildSubmission("u", CaptureMethod.ObjectCapture);
            s.MarkReady(new FurnitureCaptureResult { Bounds = new CaptureBounds(1.2f, 0.6f, 0.7f) });
            Assert.IsTrue(s.Bounds.HasValue);
            Assert.AreEqual(1.2f, s.Bounds.Value.WidthM, 0.001f);
        }
    }
}
