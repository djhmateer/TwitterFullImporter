using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using RabbitMQ.Client;
using Serilog;
using Serilog.Formatting.Json;

namespace TwitterStreamLoader
{
    class Program
    {
        static void Main()
        {
            Log.Logger = new LoggerConfiguration()
               .MinimumLevel.Debug()
                .WriteTo.RollingFile(new JsonFormatter(), @"C:\logs\TwitterStreamLoaderLog-{Date}.txt")
               .Enrich.WithProperty("ApplicationName", "TwitterStreamLoader")
               .CreateLogger();
            Console.WriteLine("Starting TwitterStreamLoader");
            Log.Information("Starting TwitterStreamLoader");
            var t = new TwitterStreamClient();
            t.CallTwitterStreamingAPI();
        }
    }

    // http://www.adamjamesbull.co.uk/words/rolling-your-own-connecting-to-the-twitter-streaming-api-using-c/
    public class TwitterStreamClient : OAuthBase
    {
        string filteredUrl = @"https://stream.twitter.com/1.1/statuses/filter.json";
        string consumerKey = "n7Uxxxxxxxxxxx";
        string consumerSecret = "JMY9rxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
        string accessToken = "11309782-lxCxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
        string accessSecret = "Hg8VN89HVfxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";

        public void CallTwitterStreamingAPI()
        {
            // Fallback wait time in case of Twitter rate limiting
            var wait = 250;
            while (true)
            {
                // docs: https://dev.twitter.com/streaming/overview/request-parameters
                var keywords = new List<string>();
                var follows = new List<string>();

                keywords.AddRange(new List<string> { "Entomology", "Bumble Bee", "bumblebees", "Hover fly", "hoverfly", "Hammerschmidtia", "Blera fallax" });
                //keywords.AddRange(new List<string> { "dotnet", "shanselman", "troyhunt", "rockylhotka", "codinghorror", "aspnet", "stackoverflow" });
                //keywords.Add("Euclidea");
                //keywords.AddRange(new List<string> { "jokes", "joke" });
                //keywords.Add("scotland");
                //keywords.AddRange(new List<string> { "humor", "humour", "funny" });
                //keywords.AddRange(new List<string> { "humour" });
                //keywords.AddRange(new List<string> { "scotland", "england", "uk"});
                keywords.AddRange(new List<string> { "scotland" });
                //keywords.AddRange(new List<string> { "@realdonaldtrump"});

                // try looking for people rather than keywords
                // http://gettwitterid.com
                follows.AddRange(new List<string> { "140118545" }); //Queen_UK
                follows.AddRange(new List<string> { "5402612" }); //BBCBreaking
                follows.AddRange(new List<string> { "3367735517" }); //HoverflyLagoons

                follows.AddRange(new List<string> { "14454642" }); //ayende
                follows.AddRange(new List<string> { "6108292" }); //samnewman
                follows.AddRange(new List<string> { "14414286" }); //troyhunt
                follows.AddRange(new List<string> { "5676102" }); //shanselman
                follows.AddRange(new List<string> { "14429713" }); //venkat_s
                follows.AddRange(new List<string> { "21576088" }); //markrendle
                follows.AddRange(new List<string> { "5637652" }); //codinghorror
                follows.AddRange(new List<string> { "19231320" }); //mat_mcloughlin
                follows.AddRange(new List<string> { "4085561" }); //seesharp
                follows.AddRange(new List<string> { "22696598" }); //mauroservienti

                //follows.AddRange(new List<string> { "50393960" }); //billgates
                follows.AddRange(new List<string> { "44196397" }); //elonmusk
                follows.AddRange(new List<string> { "20779255" }); //slsingh

                follows.AddRange(new List<string> { "833003030089957380" }); //pjhemingway
                follows.AddRange(new List<string> { "1364930179" }); //WarrenBuffett
                follows.AddRange(new List<string> { "1243851643" }); //LondonMindful

                var keywordsEncoded = UrlEncode(string.Join(",", keywords.ToArray()));
                var followEncoded = UrlEncode(string.Join(",", follows.ToArray()));

                var postParameters =
                                //("&language=en") +
                                //("&filter_level=low") +
                                //"&locations=" + UrlEncode("-7,50,3,60") + //UK  3/s
                                "&locations=" + UrlEncode("-180,-90,180,90") + // All world
                                ("&track=" + keywordsEncoded) +
                                ("&follow=" + followEncoded)
                                // only tweets the user does?
                                // + ("&with=user")
                                ;

                // get rid of first & from Post
                if (string.IsNullOrEmpty(postParameters)) { }
                else if (postParameters.IndexOf('&') == 0)
                    postParameters = postParameters.Remove(0, 1).Replace("#", "%23");

                try
                {
                    // TODO use HttpClient and simplify
                    var webRequest = (HttpWebRequest)WebRequest.Create(filteredUrl);
                    webRequest.Timeout = -1;
                    webRequest.Headers.Add("Authorization", GetAuthHeader(filteredUrl + "?" + postParameters));

                    var encode = Encoding.GetEncoding("utf-8");
                    webRequest.Method = "POST";
                    webRequest.ContentType = "application/x-www-form-urlencoded";
                    var twitterTrack = encode.GetBytes(postParameters);
                    webRequest.ContentLength = twitterTrack.Length;
                    var twitterPost = webRequest.GetRequestStream();
                    twitterPost.Write(twitterTrack, 0, twitterTrack.Length);
                    twitterPost.Close();

                    webRequest.BeginGetResponse(ar =>
                    {
                        var req = (WebRequest)ar.AsyncState;
                        using (var response = req.EndGetResponse(ar))
                        {
                            using (var reader = new StreamReader(response.GetResponseStream()))
                            {
                                while (!reader.EndOfStream)
                                {
                                    var json = reader.ReadLine();
                                    // there are no escape characters when written to the console
                                    // when inspected in debugger - looks like "{\"created_at etc..  but in console it is {"created_at

                                    if (!string.IsNullOrEmpty(json))
                                    {
                                        Console.WriteLine(json.Substring(0, Math.Min(json.Length, 250)));

                                        //var jsonWithNewLines = JToken.Parse(json);
                                        // json on 1 line going to the queue

                                        SendSingleJsonLineToQueue(json);
                                        //Console.WriteLine(json);
                                    }
                                }
                            }
                        }

                    }, webRequest);
                }
                catch (WebException ex)
                {
                    Log.Error("Webexception {@ex}", ex);
                    Console.WriteLine($"Web exception {ex}");
                }
                catch (IOException ex)
                {
                    Log.Error("SystemIO Exception {@ex}", ex);
                    Console.WriteLine("SYSTEM IO Exception");
                }
                catch (Exception ex)
                {
                    Log.Error("Exception {@ex}", ex);
                    Console.WriteLine("Exception");
                }
                finally
                {
                    Console.WriteLine($"wait {wait}");
                    Thread.Sleep(wait);
                }
            }
        }

        private static void SendSingleJsonLineToQueue(string json)
        {
            ConnectionFactory factory = new ConnectionFactory { HostName = "localhost" };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                // declaring a queue is idempotent - will only be created if doesn't exist already
                //channel.QueueDeclare(queue: "hello2", durable: true, exclusive: false, autoDelete: false, arguments: null);
                channel.QueueDeclare(queue: "RawTweets", durable: true, exclusive: false, autoDelete: false,
                    arguments: null);
                var body = Encoding.UTF8.GetBytes(json);
                // notice I have to make the message persistent too to make it durable through a restart
                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;
                channel.BasicPublish(exchange: "", routingKey: "RawTweets", basicProperties: properties, body: body);
            }
        }

        // Uses OAuthBase
        private string GetAuthHeader(string url)
        {
            var timeStamp = GenerateTimeStamp();
            var nonce = GenerateNonce();

            // using c#7 feature - haven't declared my out param
            // https://blog.jetbrains.com/dotnet/2017/02/17/state-union-resharper-c-7-vb-net-15-support/
            string oauthSignature = GenerateSignature(new Uri(url), consumerKey, consumerSecret, accessToken, accessSecret,
                "POST", timeStamp, nonce, out string normalizeUrl, out string normalizedString);

            const string headerFormat = "OAuth oauth_nonce=\"{0}\", oauth_signature_method=\"{1}\", " +
                                        "oauth_timestamp=\"{2}\", oauth_consumer_key=\"{3}\", " +
                                        "oauth_token=\"{4}\", oauth_signature=\"{5}\", " +
                                        "oauth_version=\"{6}\"";

            return string.Format(headerFormat,
                Uri.EscapeDataString(nonce),
                Uri.EscapeDataString(Hmacsha1SignatureType),
                Uri.EscapeDataString(timeStamp),
                Uri.EscapeDataString(consumerKey),
                Uri.EscapeDataString(accessToken),
                Uri.EscapeDataString(oauthSignature),
                Uri.EscapeDataString(OAuthVersion));
        }
    }
}
