using System;
using UnityEngine;

namespace SuperRealEstate.Voice
{
    /// <summary>
    /// Parses the voice-agent Edge Function's JSON into a <see cref="VoiceResponse"/>.
    /// Pure + unit-tested (the network call is separate), so intent mapping is
    /// verified without a server.
    /// </summary>
    public static class VoiceResponseParser
    {
        public static VoiceResponse Parse(string json)
        {
            var response = new VoiceResponse();
            if (string.IsNullOrEmpty(json)) return response;

            var dto = JsonUtility.FromJson<Dto>(json);
            if (dto == null) return response;

            response.Reply = dto.reply ?? "";
            response.Action = new VoiceAction
            {
                Type = ParseType(dto.action?.type),
                Target = dto.action?.target,
                Material = dto.action?.material,
                Params = dto.action?.@params,
            };
            return response;
        }

        public static VoiceActionType ParseType(string type) => (type ?? "").ToLowerInvariant() switch
        {
            "measure_room" => VoiceActionType.MeasureRoom,
            "identify_plant" => VoiceActionType.IdentifyPlant,
            "estimate_material" => VoiceActionType.EstimateMaterial,
            "recognize_finish" => VoiceActionType.RecognizeFinish,
            "remove_wall" => VoiceActionType.RemoveWall,
            "stage_furniture" => VoiceActionType.StageFurniture,
            "show_comps" => VoiceActionType.ShowComps,
            "none" or "" => VoiceActionType.None,
            _ => VoiceActionType.Unknown,
        };

        [Serializable] private sealed class Dto { public string reply; public ActionDto action; }
        [Serializable] private sealed class ActionDto { public string type; public string target; public string material; public string @params; }
    }
}
