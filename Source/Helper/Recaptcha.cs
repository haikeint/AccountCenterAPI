using DotNetEnv;
using S84Account.Service;
using System.Net;
using System.Text.Json.Serialization;

namespace S84Account.Helper {
    public static class Recaptcha {
        private static readonly float ACCEPT_SCORE = float.Parse(Env.GetString("RECATPCHA_V3_ACCEPT_SCORE"));
        private static readonly string RECATPCHA_V2_SECRET_KEY = Env.GetString("RECATPCHA_V2_SECRET_KEY");
        private static readonly string RECATPCHA_V3_SECRET_KEY = Env.GetString("RECATPCHA_V3_SECRET_KEY");

        public static async Task<bool> Verify(string recToken, int version = 2) {
            if(Util.IsDevelopment()) return true;
            string verifyURL = "https://www.google.com/recaptcha/api/siteverify";
            Dictionary<string, string> formData = new()
            {
                { "secret", version == 2 ? RECATPCHA_V2_SECRET_KEY : RECATPCHA_V3_SECRET_KEY },
                { "response", recToken }
            };
            FormUrlEncodedContent content = new(formData);
            try {
                using HttpClient client = new();
                HttpResponseMessage response = await client.PostAsync(verifyURL, content);

                if (response.EnsureSuccessStatusCode().StatusCode != HttpStatusCode.OK) return false;
                ReCaptchaResponse? responseBody = await response.Content.ReadFromJsonAsync<ReCaptchaResponse>();
                return version == 2 ? responseBody?.Success ?? false : (responseBody?.Score ?? 0.0)*100 >= ACCEPT_SCORE *100;
            } catch (HttpRequestException) {
                //Console.WriteLine($"Request error: {_.Message}");
            }
            return false;
        }

        private class ReCaptchaResponse {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("challenge_ts")]
            public DateTime ChallengeTimestamp { get; set; }

            [JsonPropertyName("apk_package_name")]
            public string? ApkPackageName { get; set; }

            [JsonPropertyName("error-codes")]
            public List<string>? ErrorCodes { get; set; }

            [JsonPropertyName("action")]
            public string? Action { get; set; }

            [JsonPropertyName("score")]
            public float? Score { get; set; }
        }
    }
}
