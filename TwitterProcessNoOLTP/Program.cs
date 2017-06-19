using Dapper;
using FastMember;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using Serilog.Formatting.Json;
using StackExchange.Profiling;
using StackExchange.Profiling.Storage;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Transactions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TwitterProcessNoOLTP
{
    public class Program
    {
        static ConnectionFactory rabbitFactory;
        static MiniProfiler mp;

        static void Main(string[] args)
        {
            SetupLogging();
            ConsoleAndLog("Starting TwitterProcessNoOLTP");

            if (args.Length == 0) // running this program directly (not from bootstrapper)
            {
                Util.ClearMSSQLDatabaseAndInsertSeedLanguages();
                Util.FlushAllDatabasesFromRedis();

                Util.DeleteAllFromRabbitWorkerQueue();
                Util.PopulateRabbitWorkerQueueWithFilenames();

                Util.UpdateRedisLanguageCacheAndLargestLanguageIDFromMSSQL();
                Util.UpdateRedisHashTagCacheAndLargestHashTagIDFromMSSQL();
            }

            // 1.Main function (usually call from bootstrapper so does multiple times)
            GetFileNameFromWorkerQueueAndKeepListening();

            // 2.Load from a file (testing)
            //LoadTweetsAndProcess(@"C:\Dev\simpleTwitterClientC\TwitterProcessNoOLTP\Tests\NewLanguage.json");

            // Setup of Files
            //var rabbit = new RabbitConsumer();
            //rabbit.ReadAllFromQueueDumpingToTextFile();
            //SplitFileIntoSmallerOnes();

            // Sanity check testing of live data
            //Util.CheckHashTagsInDBAreActuallyInTweetText();
            ConsoleAndLog("Ending TwitterProcessNoOLTP Queue Processor");
        }

        // 1. Main function - has side effects (gets filenames from queue)
        private static void GetFileNameFromWorkerQueueAndKeepListening()
        {
            var sw = new Stopwatch();

            using (var connection = rabbitFactory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                var consumer = new EventingBasicConsumer(channel);
                //https://www.rabbitmq.com/consumer-prefetch.html
                // limiting to 1 at a time
                channel.BasicQos(0, 1, false);
                channel.BasicConsume("WorkerQueue", false, consumer);

                // every time a message is received, this lambda expression (anon method) is called
                consumer.Received += (model, ea) =>
                {
                    if (!sw.IsRunning) sw.Start();

                    var fileName = Encoding.UTF8.GetString(ea.Body);

                    Console.WriteLine();
                    Console.WriteLine($"Processing: {fileName}");
                    var redisConnection = RedisConnectionFactory.GetConnection();
                    var redis = redisConnection.GetDatabase();
                    var redisKeyNumberOfJobsCurrentlyRunning = "NumberOfJobsCurrentlyRunning";
                    long numJobsCurrentlyRunningRedisCounter;
                    try
                    {
                        // Add 1 to redis
                        var exists = redis.KeyExists(redisKeyNumberOfJobsCurrentlyRunning);
                        if (!exists)
                            redis.StringSet(redisKeyNumberOfJobsCurrentlyRunning, 1);
                        else
                            redis.StringIncrement(redisKeyNumberOfJobsCurrentlyRunning);

                        // Setup lock for redis
                        var lockKey = "LockToRunFinalSQL";
                        exists = redis.KeyExists(lockKey);
                        if (!exists)
                            redis.StringSet(lockKey, 100);

                        LoadTweetsAndProcess(fileName);

                        // job has succeeded so Ack on RabbitMQ
                        // and decrement on redis the number of jobs currently running
                        channel.BasicAck(ea.DeliveryTag, false);
                        numJobsCurrentlyRunningRedisCounter = redis.StringDecrement(redisKeyNumberOfJobsCurrentlyRunning);
                        Console.WriteLine($"NumberOfJobsCurrentlyRunning {numJobsCurrentlyRunningRedisCounter}");
                    }
                    catch (Exception ex)
                    {
                        // Have to handle exceptions here as we are in an event
                        // so caller doesn't know how to handle
                        Console.WriteLine($"Exception {ex}");
                        Log.Error("LoadTweetsAndProcess {@ex}", ex);
                        Console.WriteLine("Rejecting message");
                        channel.BasicReject(ea.DeliveryTag, true);
                    }

                    // are there more on the queue to be processed (need to check this if we're in a single threaded scenario
                    // or just a running on a few and condition where all have just finished)
                    var result = channel.QueueDeclarePassive("WorkerQueue");
                    var messagesLeftOnRabbitWorkerQueue = result?.MessageCount ?? 0;
                    if (messagesLeftOnRabbitWorkerQueue == 0)
                    {
                        // Have seen situation when the same thread displays this twice
                        Console.WriteLine($"Parallel thread work done! Waiting. Finished in {sw.ElapsedMilliseconds}");
                        Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff"));

                        numJobsCurrentlyRunningRedisCounter = (long)redis.StringGet(redisKeyNumberOfJobsCurrentlyRunning);
                        if (numJobsCurrentlyRunningRedisCounter == 0)
                        {
                            // it is possible for 2 jobs to see numJobs==0 .. so need one of them to get a lock
                            // decrement and get the value in 1 atomic operation
                            // starting value is 100. Have only ever seen 2 threads contend (out of 7)
                            var redisLockKey = "LockToRunFinalSQL";
                            var lockKeyValue = redis.StringDecrement(redisLockKey);
                            if (lockKeyValue == 99)
                            {
                                Console.WriteLine($"Got Lock! {lockKeyValue}");
                                sw.Reset();
                                sw.Start();
                                RunSQLToImportToMainTables();
                                sw.Stop();
                                Console.WriteLine($"Finished RunSQLToImportToMainTables in {sw.ElapsedMilliseconds}");

                                // Remove lock from redis as no longer need it
                                redis.KeyDelete(redisLockKey);
                            }
                            else
                                Console.WriteLine($"No Lock {lockKeyValue}");
                        }
                    }
                };
                Console.ReadLine();
            }
        }

        // The main processor 
        // this has side effects as writes and queries Redis, then writes to the database
        // try to abstract out functions which have no side effects so can test easily
        private static void LoadTweetsAndProcess(string fileNameAndPath)
        {
            var sw = new Stopwatch();
            sw.Start();

            var jsonLines = File.ReadAllLines(fileNameAndPath).ToList();

            // A dictionary to hold data for each Temp table
            var langDictionary = new Dictionary<int, string>(); // languageID (from redis), tweet.lang
            var usersDictionary = new Dictionary<long, string>(); // tweet.user.id, tweet.user.name
            var tweetsDictionary = new Dictionary<long, TweetTmp>(); // tweet.id, interesting twitter fields
            var tweetHashTagDictionary = new Dictionary<long, List<int>>(); // tweet.id, list<HashTagID>
            var hashTagDictionary = new Dictionary<int, string>(); // hashTagID (from redis), hashTagName

            var redisConnection = RedisConnectionFactory.GetConnection();
            var redis = redisConnection.GetDatabase();

            using (mp.Step("Deserialise and Redis lookups"))
                foreach (var json in jsonLines)
                {
                    Tweet tweet;
                    try
                    {
                        tweet = DeserialiseJsonToTweet(json);
                    }
                    catch (ArgumentException)
                    {
                        //Log.Error("DeserialiseJsonToTweet failed: {@ex} on json {@json}", ex, json);
                        continue;
                    }
                    //Log.Information("Tweet Received {@tweet}", tweet);
                    //var jsonWithNewLines = JToken.Parse(json);

                    var tweetIDFromTwitter = tweet.id;
                    var tt = new TweetTmp
                    {
                        CreatedAtFromTwitter = tweet.created_at,
                        TweetIDFromTwitter = tweetIDFromTwitter,
                        Text = tweet.text,
                        UserIDFromTwitter = tweet.user.id, // need this for matching up to UserTmp
                        TimeInserted = DateTime.Now,
                    };

                    // 1. Get the LanguageID by looking up what Lang is eg fr
                    tt.LanguageID = GetLanguageIDFromLanguageShortCode(tweet.lang, redis);
                    tt.Lang = tweet.lang; // so we can do insert into mssql if needed (a new language)

                    // 2. HashTags - lookup in redis and if new get redis to give a new HashTagID
                    var listOfHashTagIDs = new List<int>();
                    var countOfHashTags = 0;
                    if (tweet.entities?.hashtags != null)
                        countOfHashTags = tweet.entities.hashtags.Count;
                    if (countOfHashTags > 0)
                    {
                        // loop over all hashtags from Tweet
                        for (var i = 0; i < countOfHashTags; i++)
                        {
                            var hashTagTextOrig = tweet.entities.hashtags[i].text.Trim();

                            // make it case insensitive
                            var hashTagText = hashTagTextOrig.ToLower();

                            var hashTagRedisKey = $"HashTag:{hashTagText}";
                            var hashTagID = redis.StringGet(hashTagRedisKey);

                            // if not in redis get a new HashTagID
                            if (hashTagID.IsNullOrEmpty)
                            {
                                hashTagID = redis.StringIncrement("LargestHashTagID");
                                redis.StringSet(hashTagRedisKey, hashTagID);
                            }

                            var hashTagIDInt = (int)hashTagID;
                            listOfHashTagIDs.Add(hashTagIDInt);

                            // v.unusual to have a tweet with dupe hashtags
                            if (!hashTagDictionary.ContainsKey(hashTagIDInt))
                                hashTagDictionary.Add(hashTagIDInt, hashTagText);
                        }
                    }

                    // Stop duplicate languages being added to the LangTmp
                    if (!langDictionary.ContainsKey(tt.LanguageID))
                        langDictionary.Add(tt.LanguageID, tt.Lang);

                    // Stop duplicate users being added to UserTmp (if a user had tweeted more than once in this batch)
                    if (!usersDictionary.ContainsKey(tweet.user.id))
                        usersDictionary.Add(tweet.user.id, tweet.user.name);

                    // Stop duplicate tweets being added to TweetTmp (sometimes Twitter sends duplicate tweets)
                    if (!tweetsDictionary.ContainsKey(tweetIDFromTwitter))
                        tweetsDictionary.Add(tweetIDFromTwitter, tt);

                    // Stop duplicate tweets being added to TweetHashTag.. note that 1 tweetID has a list of HashTagID's 
                    // (sometimes Twitter sends dupe tweets)
                    if (!tweetHashTagDictionary.ContainsKey(tweetIDFromTwitter))
                        tweetHashTagDictionary.Add(tweetIDFromTwitter, listOfHashTagIDs);
                }

            // Create lists for BCP
            var langData = new List<LangTmp>();
            var usersData = new List<UserTmp>();
            var tweetsData = new List<TweetTmp>();
            var tweetHashTagData = new List<TweetHashTagTmp>();
            var hashTagsData = new List<HashTagTmp>();

            foreach (var item in langDictionary)
                langData.Add(new LangTmp { LanguageID = item.Key, ShortCode = item.Value }); // languageID (from redis), tweet.lang

            foreach (var item in usersDictionary)
                usersData.Add(new UserTmp { UserIDFromTwitter = item.Key, Name = item.Value });// tweet.user.id, tweet.user.name

            foreach (var item in tweetsDictionary)
                tweetsData.Add(item.Value); // interesting twitter fields (includes the tweet.id)

            foreach (var listOfHashTagIDs in tweetHashTagDictionary)
            foreach (var hashTagID in listOfHashTagIDs.Value)
                tweetHashTagData.Add(new TweetHashTagTmp { TweetIDFromTwitter = listOfHashTagIDs.Key, HashTagID = hashTagID }); // tweet.id, list<HashTagID>

            foreach (var item in hashTagDictionary)
                hashTagsData.Add(new HashTagTmp { HashTagID = item.Key, HashTagName = item.Value }); // hashTagID (from redis), hashTagName

            using (mp.Step("MSSQL TransactionScope"))
            using (var tran = new TransactionScope())
            using (var db = Util.GetOpenConnection())
            {
                //var sql = @"TRUNCATE TABLE LangTmp; TRUNCATE TABLE UsersTmp; TRUNCATE TABLE TweetsTmp;  TRUNCATE TABLE TweetHashTagTmp; TRUNCATE TABLE HashTagsTmp";
                var sql = @"CREATE TABLE #LangTmp (
	                        [LanguageID] [int] NOT NULL,
	                        [ShortCode] [nvarchar](50) NOT NULL
	                        ); 

                            CREATE TABLE #UsersTmp(
	                            [Name] [nvarchar](255) NOT NULL,
	                            [UserIDFromTwitter] [bigint] NOT NULL
                            );

                            CREATE TABLE #TweetsTmp(
	                            [Text] [nvarchar](1024) NOT NULL,
	                            [UserIDFromTwitter] [bigint] NOT NULL,
	                            [TweetIDFromTwitter] [bigint] NOT NULL,
	                            [Lang] [nvarchar](50) NULL,
	                            [LanguageID] [int] NULL,
	                            [TimeInserted] [datetime2](7) NULL,
	                            [CreatedAtFromTwitter] [datetime2](7) NULL
                            );

                            CREATE TABLE #TweetHashTagTmp(
	                            [TweetIDFromTwitter] [bigint] NOT NULL,
	                            [HashTagID] [int] NOT NULL
                            );

                            CREATE TABLE #HashTagsTmp(
	                            [HashTagID] [int] NOT NULL,
	                            [Name] [nvarchar](255) NOT NULL
                            );
                            ";

                using (mp.Step("Build Temp tables"))
                    db.Execute(sql);

                using (mp.Step("BCP"))
                using (var bcp = new SqlBulkCopy(db))
                {
                    // LangTmp
                    using (var reader = ObjectReader.Create(langData, "LanguageID", "ShortCode"))
                    {
                        bcp.DestinationTableName = "#LangTmp";
                        bcp.WriteToServer(reader);
                    }

                    // UsersTmp
                    using (var reader = ObjectReader.Create(usersData, "Name", "UserIDFromTwitter"))
                    {
                        bcp.DestinationTableName = "#UsersTmp";
                        bcp.WriteToServer(reader);
                    }

                    // TweetsTmp
                    using (var reader = ObjectReader.Create(tweetsData, "Text", "UserIDFromTwitter", "TweetIDFromTwitter",
                        "Lang", "LanguageID", "TimeInserted", "CreatedAtFromTwitter"))
                    {
                        bcp.DestinationTableName = "#TweetsTmp";
                        bcp.WriteToServer(reader);
                    }

                    // TweetHashTagTmp
                    using (var reader = ObjectReader.Create(tweetHashTagData, "TweetIDFromTwitter", "HashTagID"))
                    {
                        bcp.DestinationTableName = "#TweetHashTagTmp";
                        bcp.WriteToServer(reader);
                    }

                    // HashTagsTmp
                    using (var reader = ObjectReader.Create(hashTagsData, "HashTagID", "HashTagName"))
                    {
                        bcp.DestinationTableName = "#HashTagsTmp";
                        bcp.WriteToServer(reader);
                    }
                }

                // copy all into all stage tables
                // doesn't matter if we have duplicates in here
                // just get the data in really fast (and no deadlocks)
                var sql2 = @"
                    INSERT INTO LangTmp
                        SELECT *
                        FROM #LangTmp lt

                    INSERT INTO UsersTmp 
                        SELECT *
                        FROM #UsersTmp

                    INSERT INTO TweetsTmp 
                        SELECT *
                        FROM #TweetsTmp

                    INSERT INTO TweetHashTagTmp 
                        SELECT *
                        FROM #TweetHashTagTmp

                    INSERT INTO HashTagsTmp 
                        SELECT *
                        FROM #HashTagsTmp
                        ";

                using (mp.Step("Inserting from #xTmp to xTmp"))
                    db.Execute(sql2);

                tran.Complete();
            }

            sw.Stop();
            var elapsedMilliseconds = sw.ElapsedMilliseconds;
            Console.WriteLine($"time: {elapsedMilliseconds}");
            Console.WriteLine($"users and tweets: {usersData.Count} {tweetsData.Count}");

            if (elapsedMilliseconds <= 1000) elapsedMilliseconds = 1000;
            var ts = tweetsData.Count / (elapsedMilliseconds / 1000);

            Console.WriteLine($"ts {ts}");

            MiniProfiler.Settings.ProfilerProvider.Stop(false);
            // have seen deadlocks here on 7 different processes running!
            Console.WriteLine(MiniProfiler.Current.RenderPlainText());
            mp = MiniProfiler.Start();
        }

        private static void RunSQLToImportToMainTables()
        {
            using (var tran = new TransactionScope())
            using (var db = Util.GetOpenConnection())
            {
                var sql = @"-- Insert any new Languages
                        INSERT INTO Languages 
                            (LanguageID, Shortcode, Name) 
                            SELECT lt.LanguageID, lt.ShortCode, '' 
                         FROM   LangTmp lt 
                         WHERE  NOT EXISTS (SELECT LanguageID 
                              FROM   Languages l 
                              WHERE  l.LanguageID = lt.LanguageID) ";
                db.Execute(sql);
                sql = @"
                        -- Insert any new Users
                           INSERT INTO Users 
                            (Name, UserIDFromTwitter) 
  
	                        -- take any name
                            -- a user can be in multiple times in the UsersTmp
                            -- want only 1 unique user
	                        SELECT ut.Name, ut.UserIDFromTwitter
	                        FROM (SELECT ut.*,
				                         row_number() OVER (partition by UserIDFromTwitter order by [Name]) as seqnum
		                          from UsersTmp ut
		                         ) ut
	                        WHERE seqnum = 1
                            AND  NOT EXISTS (SELECT UserIDFromTwitter 
                                                FROM   Users u 
                                                WHERE  u.UserIDFromTwitter = ut.UserIDFromTwitter)";
                db.Execute(sql);
                sql = @"
                        -- Insert Tweets (all users are in now) 
                        INSERT INTO Tweets 
                            (CreatedAtFromTwitter,TweetIDFromTwitter,Text,UserID,LanguageID, TimeInserted) 

	                        -- take any tweet (if multiple)
                            SELECT tt.CreatedAtFromTwitter, tt.TweetIDFromTwitter, tt.Text, u.UserID, tt.LanguageID, tt.TimeInserted 
                            FROM (select tt.*,
				                         row_number() over (partition by TweetIDFromTwitter order by TimeInserted desc) as seqnum
		                          from TweetsTmp tt
		                         ) tt
                            JOIN Users u ON u.UserIDFromTwitter = tt.UserIDFromTwitter
	                        where seqnum = 1
                            -- check not a duplicate tweet insert (this would happen if twitter sent a duplicate tweet which spanned import files)
	                        -- this does happen..approx 1 in 200k

	                        -- check Tweet not in there already..would be very strange.
                            AND NOT EXISTS (SELECT TweetIDFromTwitter 
                                                FROM Tweets t 
                                                WHERE  t.TweetIDFromTwitter = tt.TweetIDFromTwitter) ";

                db.Execute(sql);
                sql = @"
                        -- Insert any new HashTags
                        INSERT INTO HashTags 
                        (HashTagID,Name) 
	                    -- take any HashTag (if multiple).. explore why there are so many multiples..maybe a way to reduce
	                    -- still need the safety here of dupe checker

	                    -- this is case sensitive, which is okay as twitter works this way eg #hastag is same as #Hashtag
                        SELECT distinct htt.HashTagID, htt.Name 
                        FROM HashTagsTmp htt 
		
                        WHERE  NOT EXISTS (SELECT ht.HashTagID
                            FROM   HashTags ht 
                            WHERE  ht.HashTagID = htt.HashTagID)";

                db.Execute(sql);
                sql = @"
                        -- Insert TweetHashTag (all Tweets and all HashTags are in now)
                        INSERT INTO TweetHashTag 
                            (TweetIDFromTwitter, TweetID, HashTagID)
                            SELECT thtt.TweetIDFromTwitter, t.tweetID, thtt.HashTagID
                            FROM TweetHashTagTmp thtt
                            JOIN Tweets t on t.TweetIDFromTwitter = thtt.TweetIDFromTwitter
                        ";

                db.Execute(sql);
                tran.Complete();
            }
        }

        // integration tests clear down db and redis before the run each test
        private static int GetLanguageIDFromLanguageShortCode(string languageShortCode, IDatabase redis)
        {
            int languageID;
            var langRedisKey = $"Language:{languageShortCode}";

            var memoryCache = MemoryCache.Default;
            var result = memoryCache[langRedisKey];

            if (result != null) // exists in memoryCache
                languageID = (int)result;
            else // lookup Redis
            {
                var languageIDRedisType = redis.StringGet(langRedisKey);

                if (languageIDRedisType.IsNullOrEmpty) // not in Redis
                {
                    // get a new LanguageID by incrementing counter from redis
                    languageIDRedisType = redis.StringIncrement("LargestLanguageID");
                    redis.StringSet(langRedisKey, languageIDRedisType);
                }
                int.TryParse(languageIDRedisType, out languageID);
                memoryCache.Add(langRedisKey, languageID, null);
            }
            return languageID;
        }

        [Fact]
        public void GivenNewShortCode_ShouldReturnANewLanguageIDGenereatedFromRedis()
        {
            Util.ClearMSSQLDatabaseAndInsertSeedLanguages();
            Util.FlushAllDatabasesFromRedis();

            var redisConnection = RedisConnectionFactory.GetConnection();
            var redis = redisConnection.GetDatabase();

            int expectedLanguageID = Util.GetLargestLanguageIDFromRedis() + 1;
            int result = GetLanguageIDFromLanguageShortCode("xxx", redis);

            // Did the function return the correct result?
            Assert.Equal(expectedLanguageID, result);
            // Did the counter get updated
            Assert.Equal(expectedLanguageID, Util.GetLargestLanguageIDFromRedis());
        }

        [Fact]
        // Integration tests
        public void GivenShortCodeEn_ShouldReturnLanguageID37()
        {
            ResetRedisAndMssql();

            var redisConnection = RedisConnectionFactory.GetConnection();
            var redis = redisConnection.GetDatabase();

            var result = GetLanguageIDFromLanguageShortCode("en", redis);
            Assert.Equal(37, result);
        }

        [Fact]
        public void GivenShortCodeEnAndEs_ShouldReturnLanguageIDs()
        {
            ResetRedisAndMssql();

            var redisConnection = RedisConnectionFactory.GetConnection();
            var redis = redisConnection.GetDatabase();

            var result = GetLanguageIDFromLanguageShortCode("en", redis);
            Assert.Equal(37, result);
            result = GetLanguageIDFromLanguageShortCode("es", redis);
            Assert.Equal(39, result);
        }

        [Fact]
        public void GivenShortCodeEnAndEsTwice_ShouldReturnLanguageIDs()
        {
            ResetRedisAndMssql();

            var redisConnection = RedisConnectionFactory.GetConnection();
            var redis = redisConnection.GetDatabase();

            var result = GetLanguageIDFromLanguageShortCode("en", redis);
            Assert.Equal(37, result);
            result = GetLanguageIDFromLanguageShortCode("es", redis);
            Assert.Equal(39, result);
            result = GetLanguageIDFromLanguageShortCode("en", redis);
            Assert.Equal(37, result);
        }

        private static void ResetRedisAndMssql()
        {
            Util.FlushAllDatabasesFromRedis();
            Util.ClearMSSQLDatabaseAndInsertSeedLanguages();
            Util.UpdateRedisLanguageCacheAndLargestLanguageIDFromMSSQL();
        }

        // function has no side effects - so easy to unit test
        public static Tweet DeserialiseJsonToTweet(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentException("Cannot deserialise a null or empty string");

            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new IsoDateTimeConverter
            {
                DateTimeFormat = "ddd MMM dd HH:mm:ss +ffff yyyy",
                DateTimeStyles = DateTimeStyles.AdjustToUniversal
            });
            settings.MissingMemberHandling = MissingMemberHandling.Ignore;
            var tweet = JsonConvert.DeserializeObject<Tweet>(json, settings);

            // eg {[limit, {"track": 6,"timestamp_ms": "1488384664896"}]}
            if (tweet.id_str == null)
                throw new ArgumentException("Valid json but not a valid tweet, as no ID found");

            return tweet;
        }

        static string testFilePath = @"C:\Dev\simpletwitterclientc\TwitterProcessNoOLTP\Tests\";

        [Fact]
        public void GivenApostrophInHashTagDontInText_ButActuallyDonInEntities_DeserialiseCorrectly()
        {
            var path = testFilePath + "TweetWithApos.json";
            var line = File.ReadAllLines(path)[0];
            var tweet = DeserialiseJsonToTweet(line);
            var firstHashTag = tweet.entities.hashtags[0];
            Assert.Equal("Don", firstHashTag.text);
        }

        [Fact]
        public void GivenEmptyString_ShouldThrow()
        {
            var exception = Record.Exception(() => DeserialiseJsonToTweet(""));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
        }

        [Fact]
        public void GivenGoodJson_ShouldReturnTweet()
        {
            var path = testFilePath + "GoodTweet.json";
            var json = File.ReadAllLines(path)[0];
            var tweet = DeserialiseJsonToTweet(json);
            Assert.Equal("@ImP_Pracstyle No body wanted but everybody need it !", tweet.text);
        }

        [Fact]
        public void GivenMalformedJson_ShouldThrow()
        {
            var path = testFilePath + "MalformedJsonTweet.json";
            var line = File.ReadAllLines(path)[0];
            var exception = Record.Exception(() => DeserialiseJsonToTweet(line));
            Assert.NotNull(exception);
            Assert.IsType<JsonReaderException>(exception);
        }

        [Fact]
        // "created_at":"Thu Feb 33 13:32:22 +0000 2017"
        public void GivenJsonDateThatDoesntConformToType_ShouldThrow()
        {
            var path = testFilePath + "TweetWithDateTypeError.json";
            var line = File.ReadAllLines(path)[0];
            var exception = Record.Exception(() =>
            {
                var x = DeserialiseJsonToTweet(line);
                return x;
            });
            Assert.NotNull(exception);
            // As 33rd Feb is not in Gregorian calendar, json.net uses DateTime.TryParse, so will throw a system exception
            Assert.IsType<FormatException>(exception);
        }

        [Fact]
        // "created_at":"Thu Feb 33 13:32:22 +0000 2017"
        public void GivenJsonWithNonEnglishLang_ShouldReturnNonEn()
        {
            var path = testFilePath + "FrenchTweet.json";
            var line = File.ReadAllLines(path)[0];
            var tweet = DeserialiseJsonToTweet(line);
            Assert.Equal("fr", tweet.lang);
        }

        [Fact]
        //https://dev.twitter.com/overview/api/upcoming-changes-to-tweets
        public void GivenMultipleJson_ShouldNotThrow()
        {
            var path = testFilePath + "ManyTweets.json";
            var lines = File.ReadAllLines(path);
            foreach (var line in lines)
            {
                var x = DeserialiseJsonToTweet(line);
                Assert.NotNull(x);
            }
        }

        [Fact]
        public void GivenMissing_ShouldNotThrow()
        {
            var path = testFilePath + "MissingPartOfJson.json";
            var line = File.ReadAllLines(path)[0];
            var x = DeserialiseJsonToTweet(line);
            Assert.NotNull(x);
        }

        private static void SplitFileIntoSmallerOnes()
        {
            var path = @"d:\Tweets\";
            var bigFileNameNoExtension = "Data1906";

            var readPath = path + $"{bigFileNameNoExtension}.json";
            var jsonLines = File.ReadLines(readPath);
            var numberOfLinesLeft = jsonLines.Count();
            Console.WriteLine(numberOfLinesLeft);
            var list = new List<string>();
            string writePath;
            foreach (var json in jsonLines)
            {
                list.Add(json);
                int chunckSize = 50000;
                if (numberOfLinesLeft > chunckSize - 1)
                {
                    if (numberOfLinesLeft % chunckSize == 0)
                    {
                        writePath = path + $@"split1906\{bigFileNameNoExtension}_{numberOfLinesLeft}.json";
                        Console.WriteLine(numberOfLinesLeft);
                        File.AppendAllLines(writePath, list);
                        list.Clear();
                    }
                }
                numberOfLinesLeft--;
            }
            // write out the final file 0-chunksize
            writePath = path + $@"split1906\{bigFileNameNoExtension}_0.json";
            File.AppendAllLines(writePath, list);
            Console.WriteLine("done splitting");
        }

        private static void ConsoleAndLog(string m)
        {
            Console.WriteLine(m);
            Log.Information(m);
        }

        private static void SetupLogging()
        {
            rabbitFactory = new ConnectionFactory { HostName = "localhost" };

            MiniProfiler.Settings.Storage = new SqlServerStorage(Util.GetConnectionString());
            MiniProfiler.Settings.ProfilerProvider = new SingletonProfilerProvider();
            MiniProfiler.Settings.ProfilerProvider.Start(ProfileLevel.Info);
            MiniProfiler.Start();
            mp = MiniProfiler.Start();

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.RollingFile(new JsonFormatter(), @"C:\ELK-Stack\logs\TwitterProcessNoOLTPLog-{Hour}.txt")
                .Enrich.WithProperty("ApplicationName", "TwitterProcessNoOLTPLog")
                .CreateLogger();
        }
    }

    public class HtThing
    {
        public int HashTagID { get; set; }
        public string HashTagName { get; set; }
    }

    public class LangTmp
    {
        public int LanguageID { get; set; }
        public string ShortCode { get; set; }
    }

    public class RedisConnectionFactory
    {
        private static readonly Lazy<ConnectionMultiplexer> Connection;

        static RedisConnectionFactory()
        {
            string connectionString;

            connectionString = "localhost,allowAdmin=true";

            var options = ConfigurationOptions.Parse(connectionString);
            Connection = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(options));
        }
        public static ConnectionMultiplexer GetConnection() => Connection.Value;
    }

    public class HashTagTmp
    {
        //public long TweetIDFromTwitter { get; set; }
        public int HashTagID { get; set; }
        public string HashTagName { get; set; }
    }

    public class TweetHashTagTmp
    {
        public long TweetIDFromTwitter { get; set; }
        public int HashTagID { get; set; }
        //public string HashTagName { get; set; }
    }

    public class TweetTmp
    {
        public string Text { get; set; }
        public long UserIDFromTwitter { get; set; }
        public long TweetIDFromTwitter { get; set; }
        public string Lang { get; set; }
        public int LanguageID { get; set; }
        public DateTime TimeInserted { get; set; }
        public DateTime CreatedAtFromTwitter { get; set; }
        public List<int> ListOfHashTagIDs { get; set; }
    }

    public class UserTmp
    {
        public string Name { get; set; }
        public long UserIDFromTwitter { get; set; }
    }

    public class RabbitConsumer //: IDisposable
    {
        private const string HostName = "localhost";
        private const string UserName = "guest";
        private const string Password = "guest";
        //private const string QueueName = "TweetRawStreamQueue";
        private const string QueueName = "RawTweets";

        private ConnectionFactory connectionFactory;
        private IConnection connection;
        private IModel model;

        public RabbitConsumer()
        {
            connectionFactory = new ConnectionFactory
            {
                HostName = HostName,
                UserName = UserName,
                Password = Password
            };

            connection = connectionFactory.CreateConnection();
            model = connection.CreateModel();
            model.BasicQos(0, 1, false);
        }

        public void ReadAllFromQueueDumpingToTextFile()
        {
            var consumer = new QueueingBasicConsumer(model);
            model.BasicConsume("RawTweets", false, consumer);

            var sb = new StringBuilder();
            uint numberLeftOnQueue = 1;
            while (numberLeftOnQueue > 0)
            {
                // Get next message from Queue
                var deliveryArgs = consumer.Queue.Dequeue();
                var json = Encoding.Default.GetString(deliveryArgs.Body);

                sb.Append(json + Environment.NewLine);

                // RawTweeets
                var result = model.QueueDeclarePassive("RawTweets");
                numberLeftOnQueue = result?.MessageCount ?? 0;

                var path = @"d:\Tweets\Data1906.json";
                if (numberLeftOnQueue > 999)
                {
                    if (numberLeftOnQueue % 1000 == 0)
                    {
                        File.AppendAllText(path, sb.ToString());
                        Console.WriteLine(numberLeftOnQueue);
                        sb.Clear();
                    }
                }
                else
                {
                    File.AppendAllText(path, sb.ToString());
                    Console.WriteLine(numberLeftOnQueue);
                    sb.Clear();
                }

                model.BasicAck(deliveryArgs.DeliveryTag, false);
            }
        }
    }

    class Point
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
    }
}
