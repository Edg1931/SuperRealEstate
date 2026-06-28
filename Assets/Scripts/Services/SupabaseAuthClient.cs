using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace SuperRealEstate.Services
{
    /// <summary>A signed-in session: the JWT the app sends so writes pass RLS.</summary>
    [Serializable]
    public sealed class AuthSession
    {
        public string AccessToken;
        public string RefreshToken;
        public string UserId;
        public long ExpiresAtUnix; // 0 if unknown

        public bool IsValid => !string.IsNullOrEmpty(AccessToken);
    }

    /// <summary>
    /// Supabase Auth (GoTrue) over REST (<c>{url}/auth/v1/...</c>) — sign in, sign
    /// up, request a magic link, and refresh. The returned <see cref="AuthSession"/>
    /// carries the access token to hand to <c>RealEstateApp.SetAccessToken</c> so
    /// catalog writes, rooms/projects, and shared sessions pass row-level security.
    ///
    /// Only the public anon key is used here (safe in the client); the user's
    /// password is sent over HTTPS to Supabase exactly as the official SDKs do.
    /// Call from the main thread (UnityWebRequest).
    /// </summary>
    public sealed class SupabaseAuthClient
    {
        private readonly string _authUrl;
        private readonly string _anonKey;

        public SupabaseAuthClient(string supabaseUrl, string anonKey)
        {
            if (string.IsNullOrEmpty(supabaseUrl)) throw new ArgumentNullException(nameof(supabaseUrl));
            _authUrl = $"{supabaseUrl.TrimEnd('/')}/auth/v1";
            _anonKey = anonKey;
        }

        /// <summary>Email + password sign-in. Throws on bad credentials.</summary>
        public Task<AuthSession> SignInWithPasswordAsync(string email, string password, CancellationToken ct = default)
            => TokenGrantAsync("password", new EmailPasswordDto { email = email, password = password }, ct);

        /// <summary>Create an account with email + password (may require confirmation).</summary>
        public async Task<AuthSession> SignUpWithPasswordAsync(string email, string password, CancellationToken ct = default)
        {
            string json = await Post("/signup", JsonUtility.ToJson(new EmailPasswordDto { email = email, password = password }), ct);
            return ToSession(json);
        }

        /// <summary>
        /// Send a passwordless magic link / OTP to the email. Completes the sign-in
        /// out of band (the user clicks the email); returns nothing here.
        /// </summary>
        public async Task SendMagicLinkAsync(string email, CancellationToken ct = default)
        {
            await Post("/otp", JsonUtility.ToJson(new EmailDto { email = email }), ct);
        }

        /// <summary>Exchange a refresh token for a fresh session.</summary>
        public Task<AuthSession> RefreshAsync(string refreshToken, CancellationToken ct = default)
            => TokenGrantAsync("refresh_token", new RefreshDto { refresh_token = refreshToken }, ct);

        private async Task<AuthSession> TokenGrantAsync(string grantType, object body, CancellationToken ct)
        {
            string json = await Post($"/token?grant_type={grantType}", JsonUtility.ToJson(body), ct);
            return ToSession(json);
        }

        private static AuthSession ToSession(string json)
        {
            if (string.IsNullOrEmpty(json)) return new AuthSession();
            AuthResponseDto dto;
            try { dto = JsonUtility.FromJson<AuthResponseDto>(json); }
            catch { return new AuthSession(); }
            if (dto == null) return new AuthSession();

            return new AuthSession
            {
                AccessToken = dto.access_token,
                RefreshToken = dto.refresh_token,
                UserId = dto.user != null ? dto.user.id : null,
                ExpiresAtUnix = dto.expires_at,
            };
        }

        private async Task<string> Post(string pathAndQuery, string body, CancellationToken ct)
        {
            using var www = new UnityWebRequest(_authUrl + pathAndQuery, UnityWebRequest.kHttpVerbPOST)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body ?? "{}")),
            };
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("apikey", _anonKey);
            www.SetRequestHeader("Authorization", $"Bearer {_anonKey}");

            var tcs = new TaskCompletionSource<bool>();
            using (ct.Register(() => www.Abort()))
            {
                www.SendWebRequest().completed += _ => tcs.TrySetResult(true);
                await tcs.Task;
            }

            ct.ThrowIfCancellationRequested();
            if (www.result != UnityWebRequest.Result.Success)
                throw new Exception($"Supabase auth {pathAndQuery} failed: {www.responseCode} {www.error} {www.downloadHandler.text}");
            return www.downloadHandler.text;
        }

        // --- DTOs ---
        [Serializable] private sealed class EmailPasswordDto { public string email; public string password; }
        [Serializable] private sealed class EmailDto { public string email; }
        [Serializable] private sealed class RefreshDto { public string refresh_token; }

        [Serializable] private sealed class AuthResponseDto
        {
            public string access_token;
            public string refresh_token;
            public long expires_at;
            public int expires_in;
            public string token_type;
            public AuthUserDto user;
        }

        [Serializable] private sealed class AuthUserDto { public string id; public string email; }
    }
}
