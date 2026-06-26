using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using SuperRealEstate.Acquisition;   // BuildingModelParser
using SuperRealEstate.Projects;      // IProjectStore / LoadedProject / RenovationProject
using SuperRealEstate.Renovation;
using SuperRealEstate.Staging;

namespace SuperRealEstate.ProjectsBackend
{
    /// <summary>
    /// Supabase-backed <see cref="IProjectStore"/> over PostgREST
    /// (<c>{url}/rest/v1/...</c>). Loads a <see cref="RenovationProject"/> and its
    /// referenced pieces — the base <see cref="BuildingModel"/>
    /// (<c>building_models.geometry</c>), the <see cref="RenovationPlan"/>
    /// (<c>renovation_plans.edits</c>), and the design placements
    /// (<c>staging_placements</c>) — and assembles a <see cref="LoadedProject"/> the
    /// on-device auto-stager renders.
    ///
    /// Pass a user access token once signed in so row-level security applies; the
    /// anon key alone covers public reads. Call from the main thread
    /// (UnityWebRequest). The HTTP helper mirrors
    /// <c>SuperRealEstate.Services.SupabaseBackendClient</c>; the JSON mapping is
    /// delegated to the pure, unit-tested <see cref="ProjectPayloadParser"/> and
    /// <see cref="BuildingModelParser"/>.
    /// </summary>
    public sealed class SupabaseProjectStore : IProjectStore
    {
        private readonly string _restUrl;
        private readonly string _anonKey;
        private string _accessToken;

        public SupabaseProjectStore(string supabaseUrl, string anonKey, string accessToken = null)
        {
            if (string.IsNullOrEmpty(supabaseUrl)) throw new ArgumentNullException(nameof(supabaseUrl));
            _restUrl = $"{supabaseUrl.TrimEnd('/')}/rest/v1";
            _anonKey = anonKey;
            _accessToken = accessToken;
        }

        /// <summary>Set after the user signs in so reads pass RLS.</summary>
        public void SetAccessToken(string token) => _accessToken = token;

        public async Task<LoadedProject> LoadAsync(string projectId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));

            // 1) The project record itself.
            string projectJson = await Get(
                $"/renovation_projects?id=eq.{UnityWebRequest.EscapeURL(projectId)}&select=*", ct);
            var projectRows = JsonUtility.FromJson<ProjectRowList>(Wrap(projectJson));
            if (projectRows?.items == null || projectRows.items.Length == 0)
                throw new Exception($"Project '{projectId}' not found.");

            ProjectRow row = projectRows.items[0];
            var project = ToProject(row);

            var loaded = new LoadedProject
            {
                Project = project,
                BaseModel = new BuildingModel(),
                Plan = new RenovationPlan(),
                BlueprintPlacements = new List<BlueprintPlacement>(),
                AnchorPlacements = new List<Placement>(),
            };

            // 2) The base building model geometry (JSONB column).
            if (!string.IsNullOrEmpty(project.BuildingModelId))
            {
                string geometryJson = await GetSingleColumn(
                    "building_models", project.BuildingModelId, "geometry", ct);
                loaded.BaseModel = BuildingModelParser.Parse(geometryJson);
                if (string.IsNullOrEmpty(loaded.BaseModel.Id))
                    loaded.BaseModel.Id = project.BuildingModelId;
            }

            // 3) The renovation plan's edit list (JSONB column).
            if (!string.IsNullOrEmpty(project.RenovationPlanId))
            {
                string editsJson = await GetSingleColumn(
                    "renovation_plans", project.RenovationPlanId, "edits", ct);
                loaded.Plan = ProjectPayloadParser.ParseEdits(editsJson);
                loaded.Plan.Id = project.RenovationPlanId;
                loaded.Plan.BuildingModelId = project.BuildingModelId;
            }

            // 4) The design placements for the project's staging layout.
            if (!string.IsNullOrEmpty(project.StagingLayoutId))
            {
                string placementsJson = await Get(
                    $"/staging_placements?layout_id=eq.{UnityWebRequest.EscapeURL(project.StagingLayoutId)}" +
                    "&select=id,furniture_asset_id,catalog_item_id,pos_x,pos_y,pos_z," +
                    "rot_y_deg,scale,anchor_id,plan_x,plan_y,plan_yaw_deg", ct);
                var (blueprint, anchorPlacements) = ProjectPayloadParser.ParsePlacements(placementsJson);
                loaded.BlueprintPlacements = blueprint;
                loaded.AnchorPlacements = anchorPlacements;
            }

            return loaded;
        }

        // --- mapping ---

        private static RenovationProject ToProject(ProjectRow row) => new RenovationProject
        {
            Id = row.id,
            Name = string.IsNullOrEmpty(row.name) ? "Project" : row.name,
            PropertyId = row.property_id,
            Kind = ParseKind(row.kind),
            Origin = ParseOrigin(row.origin),
            Status = ParseStatus(row.status),
            BlueprintId = row.blueprint_id,
            BuildingModelId = row.building_model_id,
            StagingLayoutId = row.staging_layout_id,
            RenovationPlanId = row.renovation_plan_id,
        };

        // Server stores snake_case enum strings (migration 0008).
        private static ProjectKind ParseKind(string k) => k switch
        {
            "empty_staging" => ProjectKind.EmptyStaging,
            "extension"     => ProjectKind.Extension,
            "new_build"     => ProjectKind.NewBuild,
            _               => ProjectKind.Renovation,
        };

        private static BlueprintOrigin ParseOrigin(string o) => o switch
        {
            "cubicasa_upload" => BlueprintOrigin.CubiCasaUpload,
            "cubicasa_api"    => BlueprintOrigin.CubiCasaApi,
            "matterport_api"  => BlueprintOrigin.MatterportApi,
            "roomplan"        => BlueprintOrigin.RoomPlan,
            "manual_desktop"  => BlueprintOrigin.ManualDesktop,
            _                 => BlueprintOrigin.PhoneScan,
        };

        private static ProjectStatus ParseStatus(string s) => s switch
        {
            "designed"     => ProjectStatus.Designed,
            "ready_for_ar" => ProjectStatus.ReadyForAr,
            "archived"     => ProjectStatus.Archived,
            _              => ProjectStatus.Draft,
        };

        // --- HTTP helpers (mirror SupabaseBackendClient) ---

        /// <summary>
        /// Fetch a single JSONB/text column for one row and return its raw JSON.
        /// Uses PostgREST's single-object representation
        /// (<c>Accept: application/vnd.pgrst.object+json</c>) so the body is the
        /// column value, not an array — which is what the JSONB parsers expect.
        /// </summary>
        private async Task<string> GetSingleColumn(string table, string id, string column, CancellationToken ct)
        {
            string json = await Get(
                $"/{table}?id=eq.{UnityWebRequest.EscapeURL(id)}&select={column}", ct);
            // Response is an array of one object: [{ "<column>": <value> }].
            // Unwrap to the column value without a full DTO per column.
            return ExtractColumn(json, column);
        }

        // Pull a single field's raw JSON value out of `[{ "<col>": <value> }]`.
        // Avoids a bespoke DTO per JSONB column while staying JsonUtility-free
        // for nested/array values (which JsonUtility cannot round-trip as text).
        private static string ExtractColumn(string arrayJson, string column)
        {
            if (string.IsNullOrWhiteSpace(arrayJson)) return null;
            string key = "\"" + column + "\"";
            int k = arrayJson.IndexOf(key, StringComparison.Ordinal);
            if (k < 0) return null;
            int colon = arrayJson.IndexOf(':', k + key.Length);
            if (colon < 0) return null;

            int i = colon + 1;
            while (i < arrayJson.Length && char.IsWhiteSpace(arrayJson[i])) i++;
            if (i >= arrayJson.Length) return null;

            char c = arrayJson[i];
            if (c == 'n') return null; // null

            if (c == '[' || c == '{')
            {
                char open = c, close = c == '[' ? ']' : '}';
                int depth = 0;
                bool inStr = false, esc = false;
                int start = i;
                for (; i < arrayJson.Length; i++)
                {
                    char ch = arrayJson[i];
                    if (esc) { esc = false; continue; }
                    if (ch == '\\') { esc = true; continue; }
                    if (ch == '"') { inStr = !inStr; continue; }
                    if (inStr) continue;
                    if (ch == open) depth++;
                    else if (ch == close && --depth == 0) { return arrayJson.Substring(start, i - start + 1); }
                }
                return null;
            }

            if (c == '"')
            {
                int start = i + 1;
                bool esc = false;
                for (i = start; i < arrayJson.Length; i++)
                {
                    char ch = arrayJson[i];
                    if (esc) { esc = false; continue; }
                    if (ch == '\\') { esc = true; continue; }
                    if (ch == '"') return arrayJson.Substring(start, i - start);
                }
                return null;
            }

            // Scalar (number / bool): read up to the next , } or ].
            int s2 = i;
            for (; i < arrayJson.Length; i++)
            {
                char ch = arrayJson[i];
                if (ch == ',' || ch == '}' || ch == ']') break;
            }
            return arrayJson.Substring(s2, i - s2).Trim();
        }

        private Task<string> Get(string pathAndQuery, CancellationToken ct)
            => Send(UnityWebRequest.kHttpVerbGET, pathAndQuery, null, ct);

        private async Task<string> Send(string verb, string pathAndQuery, string body, CancellationToken ct)
        {
            using var www = new UnityWebRequest(_restUrl + pathAndQuery, verb)
            {
                downloadHandler = new DownloadHandlerBuffer(),
            };
            if (body != null)
            {
                www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                www.SetRequestHeader("Content-Type", "application/json");
            }
            www.SetRequestHeader("apikey", _anonKey);
            www.SetRequestHeader("Authorization", $"Bearer {(_accessToken ?? _anonKey)}");

            var tcs = new TaskCompletionSource<bool>();
            using (ct.Register(() => www.Abort()))
            {
                www.SendWebRequest().completed += _ => tcs.TrySetResult(true);
                await tcs.Task;
            }

            ct.ThrowIfCancellationRequested();
            if (www.result != UnityWebRequest.Result.Success)
                throw new Exception($"Supabase {verb} {pathAndQuery} failed: {www.responseCode} {www.error}");
            return www.downloadHandler.text;
        }

        // JsonUtility can't parse a top-level array — wrap it as an object.
        private static string Wrap(string jsonArray)
            => "{\"items\":" + (string.IsNullOrEmpty(jsonArray) ? "[]" : jsonArray) + "}";

        // --- row DTO (snake_case to match PostgREST / migration 0008) ---
        [Serializable] private sealed class ProjectRowList { public ProjectRow[] items; }
        [Serializable] private sealed class ProjectRow
        {
            public string id, name, property_id, kind, origin, status;
            public string blueprint_id, building_model_id, staging_layout_id, renovation_plan_id;
        }
    }
}
