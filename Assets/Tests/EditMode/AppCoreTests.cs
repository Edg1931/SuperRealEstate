using NUnit.Framework;
using SuperRealEstate.AppCore;

namespace SuperRealEstate.Tests
{
    public class AppSettingsTests
    {
        [Test]
        public void Defaults_AreImperial_VoiceOn_AnalyticsOff()
        {
            var s = new AppSettings();
            Assert.AreEqual(UnitSystem.Imperial, s.Units);
            Assert.AreEqual(QualityLevel.Balanced, s.Quality);
            Assert.IsTrue(s.VoiceEnabled);
            Assert.IsFalse(s.AnalyticsOptIn);
        }

        [Test]
        public void Serialize_RoundTrips()
        {
            var s = new AppSettings
            {
                Units = UnitSystem.Metric,
                Quality = QualityLevel.High,
                VoiceEnabled = false,
                AnalyticsOptIn = true,
                UiScale = 1.25f,
                ReduceMotion = true,
            };
            AppSettings back = AppSettings.Deserialize(s.Serialize());

            Assert.AreEqual(UnitSystem.Metric, back.Units);
            Assert.AreEqual(QualityLevel.High, back.Quality);
            Assert.IsFalse(back.VoiceEnabled);
            Assert.IsTrue(back.AnalyticsOptIn);
            Assert.That(back.UiScale, Is.EqualTo(1.25f).Within(0.001f));
            Assert.IsTrue(back.ReduceMotion);
        }

        [Test]
        public void Deserialize_EmptyOrJunk_KeepsDefaults()
        {
            AppSettings a = AppSettings.Deserialize("");
            Assert.AreEqual(UnitSystem.Imperial, a.Units);

            AppSettings b = AppSettings.Deserialize("nonsense;units=Metric;bogus=zzz");
            Assert.AreEqual(UnitSystem.Metric, b.Units);
            Assert.IsTrue(b.VoiceEnabled); // untouched default
        }

        [Test]
        public void ClampedUiScale_StaysInRange()
        {
            Assert.That(new AppSettings { UiScale = 5f }.ClampedUiScale, Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(new AppSettings { UiScale = 0.1f }.ClampedUiScale, Is.EqualTo(0.75f).Within(0.001f));
        }
    }

    public class TelemetryTests
    {
        [Test]
        public void Disabled_DropsEvents()
        {
            var sink = new BufferTelemetrySink();
            var t = new Telemetry(sink, enabled: false);
            t.Track("room_measured");
            Assert.AreEqual(0, sink.Events.Count);
        }

        [Test]
        public void Enabled_ForwardsEvents()
        {
            var sink = new BufferTelemetrySink();
            var t = new Telemetry(sink, enabled: true);
            t.Track("plant_identified", "oak", 120);
            Assert.AreEqual(1, sink.Events.Count);
            Assert.AreEqual("plant_identified", sink.Events[0].Name);
            Assert.AreEqual("oak", sink.Events[0].Detail);
            Assert.AreEqual(120, sink.Events[0].ValueMillis);
        }

        [Test]
        public void RevokingConsent_StopsForwarding()
        {
            var sink = new BufferTelemetrySink();
            var t = new Telemetry(sink, enabled: true);
            t.Track("a");
            t.SetEnabled(false);   // e.g. user revoked analytics consent
            t.Track("b");
            Assert.AreEqual(1, sink.Events.Count);
            Assert.AreEqual("a", sink.Events[0].Name);
        }

        [Test]
        public void NullSink_IsSafe()
        {
            var t = new Telemetry(null, enabled: true);
            Assert.DoesNotThrow(() => t.Track("x"));
        }
    }
}
