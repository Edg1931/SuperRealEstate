using NUnit.Framework;
using SuperRealEstate.Onboarding;

namespace SuperRealEstate.Tests
{
    public class ConsentTests
    {
        [Test]
        public void Ungranted_Consent_IsNotAllowed()
        {
            var ledger = new ConsentLedger();
            Assert.IsFalse(ledger.IsGranted(ConsentType.Camera));
            Assert.IsFalse(ledger.HasDecided(ConsentType.Camera));
            Assert.IsFalse(ConsentGate.IsAllowed(CaptureAction.IdentifyPlant, ledger));
        }

        [Test]
        public void Grant_ThenAllowed_AndDecided()
        {
            var ledger = new ConsentLedger();
            ledger.Grant(ConsentType.Camera);
            Assert.IsTrue(ledger.IsGranted(ConsentType.Camera));
            Assert.IsTrue(ledger.HasDecided(ConsentType.Camera));
            Assert.IsTrue(ConsentGate.IsAllowed(CaptureAction.RecognizeFinish, ledger));
        }

        [Test]
        public void Deny_IsDecidedButNotAllowed()
        {
            var ledger = new ConsentLedger();
            ledger.Deny(ConsentType.SceneScan);
            Assert.IsTrue(ledger.HasDecided(ConsentType.SceneScan));
            Assert.IsFalse(ledger.IsGranted(ConsentType.SceneScan));
            Assert.IsFalse(ConsentGate.IsAllowed(CaptureAction.ScanRoom, ledger));
        }

        [Test]
        public void Gate_RequiresAllConsents_AndReportsFirstMissing()
        {
            var ledger = new ConsentLedger();
            Assert.AreEqual(ConsentType.SceneScan, ConsentGate.FirstMissing(CaptureAction.MeasureRoom, ledger));
            ledger.Grant(ConsentType.SceneScan);
            Assert.IsNull(ConsentGate.FirstMissing(CaptureAction.MeasureRoom, ledger));
            Assert.IsTrue(ConsentGate.IsAllowed(CaptureAction.MeasureRoom, ledger));
        }

        [Test]
        public void NullLedger_IsNeverAllowed()
        {
            Assert.IsFalse(ConsentGate.IsAllowed(CaptureAction.IdentifyPlant, null));
        }

        [Test]
        public void Serialize_RoundTrips_Deterministically()
        {
            var ledger = new ConsentLedger();
            ledger.Grant(ConsentType.Camera);
            ledger.Deny(ConsentType.Location);
            ledger.Grant(ConsentType.SceneScan);

            string s = ledger.Serialize();
            ConsentLedger back = ConsentLedger.Deserialize(s);

            Assert.IsTrue(back.IsGranted(ConsentType.Camera));
            Assert.IsTrue(back.IsGranted(ConsentType.SceneScan));
            Assert.IsFalse(back.IsGranted(ConsentType.Location));
            Assert.IsTrue(back.HasDecided(ConsentType.Location));
            // Deterministic (sorted by name) → stable storage key.
            Assert.AreEqual(s, back.Serialize());
        }

        [Test]
        public void Deserialize_ToleratesJunk()
        {
            ConsentLedger ledger = ConsentLedger.Deserialize("garbage;Camera=1;=;Nope=1;Location");
            Assert.IsTrue(ledger.IsGranted(ConsentType.Camera));
            Assert.IsFalse(ledger.HasDecided(ConsentType.Location)); // "Location" had no =value
        }
    }
}
