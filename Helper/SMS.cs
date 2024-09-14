using System.Net.Http.Headers;
using DotNetEnv;

namespace ACAPI.Helper {
    public class SMS {

        private static readonly string SMS_FROM = Env.GetString("SMS_FROM");
        private static readonly string URL = 
            $"https://api.twilio.com/2010-04-01/Accounts/{Env.GetString("SMS_SERVICE")}/Messages.json";

        private static readonly string AUTH_HEADER = BuildAuth();

        public static bool Send(string toPhone, string content) {
            if(Util.IsDevelopment()) return true;

            FormUrlEncodedContent postData = new([
                new KeyValuePair<string, string>("Body", content),
                new KeyValuePair<string, string>("To",  $"+84{toPhone}"),
                new KeyValuePair<string, string>("From", SMS_FROM)
            ]);

            HttpClient client = new();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", AUTH_HEADER);
            HttpRequestMessage request = new(HttpMethod.Post, URL) {
                Content = postData
            };

            HttpResponseMessage response = client.Send(request);
            return response.IsSuccessStatusCode;

            //if (response.IsSuccessStatusCode) {
            //    string content = await response.Content.ReadAsStringAsync();
            //    Console.WriteLine($"Response: {content}");
            //} else {
            //    string content = await response.Content.ReadAsStringAsync();
            //    Console.WriteLine($"Error: {response.StatusCode}");
            //    Console.WriteLine(content);
            //}
        }

        private static string BuildAuth() {
            string sid = Env.GetString("SMS_SID");
            string token = Env.GetString("SMS_TOKEN");

            byte[] byteArray = System.Text.Encoding.ASCII.GetBytes($"{sid}:{token}");
            return Convert.ToBase64String(byteArray);
        }
    }
}
