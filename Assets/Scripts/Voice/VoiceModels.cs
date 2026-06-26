using System;
using System.Threading;
using System.Threading.Tasks;
using SuperRealEstate.RoomMeasure;

namespace SuperRealEstate.Voice
{
    /// <summary>An app action the voice agent can request the client perform.</summary>
    public enum VoiceActionType
    {
        None,
        MeasureRoom,
        IdentifyPlant,
        EstimateMaterial,   // Material = "mulch" / "paint" / "flooring"...
        RecognizeFinish,
        RemoveWall,         // Target = wall id
        StageFurniture,     // Target = catalog item / "sofa"
        ShowComps,
        Unknown
    }

    [Serializable]
    public sealed class VoiceAction
    {
        public VoiceActionType Type = VoiceActionType.None;
        public string Target;    // wall id / item / room
        public string Material;  // for EstimateMaterial
        public string Params;    // free-form extras (e.g. "depth=3in")
    }

    /// <summary>What the agent says back (to speak) + an optional action to run.</summary>
    [Serializable]
    public sealed class VoiceResponse
    {
        public string Reply = "";
        public VoiceAction Action = new VoiceAction();
    }

    /// <summary>Inputs for a voice turn: the transcript + whatever context the device has.</summary>
    public sealed class VoiceContext
    {
        public string Transcript;
        public RoomMeasurements? Measurements;
        public double? Latitude;
        public double? Longitude;
        public byte[] FrameImage; // optional snapshot ("what's this?")
    }

    /// <summary>
    /// Conversational agent for the AR app. Speech-to-text + text-to-speech are
    /// on-device (Android XR / Gemini, or iOS speech); this turns the recognized
    /// transcript into a spoken reply + an app action via a Supabase Edge
    /// Function (Claude). "How much mulch for this bed?", "what's this tree?",
    /// "remove this wall".
    /// </summary>
    public interface IVoiceAgent
    {
        Task<VoiceResponse> AskAsync(VoiceContext context, CancellationToken ct = default);
    }
}
