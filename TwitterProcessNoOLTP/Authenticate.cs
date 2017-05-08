using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Web.Script.Serialization;
using Serilog;

namespace TwitterProcessNoOLTP
{
    class Utility
    {
        public string RequstJson(string apiUrl, string tokenType, string accessToken)
        {
            var json = string.Empty;
            try
            {
                HttpWebRequest apiRequest = (HttpWebRequest)WebRequest.Create(apiUrl);
                var timelineHeaderFormat = "{0} {1}";
                apiRequest.Headers.Add("Authorization",
                    string.Format(timelineHeaderFormat, tokenType,
                        accessToken));
                apiRequest.Method = "Get";
                WebResponse timeLineResponse = apiRequest.GetResponse();

                using (timeLineResponse)
                {
                    using (var reader = new StreamReader(timeLineResponse.GetResponseStream()))
                    {
                        json = reader.ReadToEnd();
                        // The below can be used to deserialize into a c# object
                        //var result = JsonConvert.DeserializeObject<List<TimeLine>>(json);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("RequestJson {@ex}", ex);
                throw;
            }
            return json;
        }
    }

    public class AuthenticateSettings : IAuthenticateSettings
    {
        public string OAuthConsumerKey { get; set; }
        public string OAuthConsumerSecret { get; set; }
        public string OAuthUrl { get; set; }
    }

    // json type
    public class AuthResponse
    {
        [JsonProperty("token_type")]
        public string TokenType { get; set; }

        [JsonProperty("access_token")]
        public string AccessToken { get; set; }
    }

    public interface IAuthenticateSettings
    {
        string OAuthConsumerKey { get; set; }
        string OAuthConsumerSecret { get; set; }
        string OAuthUrl { get; set; }
    }

    public interface IAuthenticate
    {
        AuthResponse AuthenticateMe(IAuthenticateSettings authenticateSettings);
    }

    public class Authenticate : IAuthenticate
    {
        public AuthResponse AuthenticateMe(IAuthenticateSettings authenticateSettings)
        {
            AuthResponse twitAuthResponse = null;
            // Do the Authenticate
            var authHeaderFormat = "Basic {0}";

            var authHeader = string.Format(authHeaderFormat,
                                           Convert.ToBase64String(
                                               Encoding.UTF8.GetBytes(Uri.EscapeDataString(authenticateSettings.OAuthConsumerKey) + ":" +

                                                                      Uri.EscapeDataString((authenticateSettings.OAuthConsumerSecret)))

                                               ));
            var postBody = "grant_type=client_credentials";
            HttpWebRequest authRequest = (HttpWebRequest)WebRequest.Create(authenticateSettings.OAuthUrl);

            authRequest.Headers.Add("Authorization", authHeader);
            authRequest.Method = "POST";
            authRequest.ContentType = "application/x-www-form-urlencoded;charset=UTF-8";
            authRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            using (Stream stream = authRequest.GetRequestStream())
            {
                byte[] content = ASCIIEncoding.ASCII.GetBytes(postBody);
                stream.Write(content, 0, content.Length);
            }
            authRequest.Headers.Add("Accept-Encoding", "gzip");
            WebResponse authResponse = null;
            try
            {
                authResponse = authRequest.GetResponse();
            }
            catch (WebException ex)
            {
                // possible 503 service unavailable.
                throw;
            }
            // deserialize into an object
            using (authResponse)
            {
                using (var reader = new StreamReader(authResponse.GetResponseStream()))
                {
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    var objectText = reader.ReadToEnd();
                    twitAuthResponse = JsonConvert.DeserializeObject<AuthResponse>(objectText);
                }
            }

            return twitAuthResponse;
        }
    }
}
