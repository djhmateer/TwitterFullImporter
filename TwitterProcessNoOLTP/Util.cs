using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace TwitterProcessNoOLTP
{
    public static class Util
    {
        static ConnectionFactory rabbitFactory;

        //public static IDbConnection GetOpenConnection()
        public static SqlConnection GetOpenConnection()
        {
            var connection = new SqlConnection(GetConnectionString());
            connection.Open();
            //MiniProfiler.Settings.SqlFormatter = new StackExchange.Profiling.SqlFormatters.SqlServerFormatter();
            //return new ProfiledDbConnection(connection, MiniProfiler.Current);
            return connection;
        }

        public static string GetConnectionString()
        {
            if (Environment.MachineName == "DAVIDMADESK5")
                return @"Data Source=.\sql2016;initial catalog=SimpleTwitter;integrated security=True;MultipleActiveResultSets=True;";

            return @"Data Source=.\;initial catalog=SimpleTwitter;integrated security=True;MultipleActiveResultSets=True;";
        }

        // used from bootstrapper too
        public static void DeleteAllFromRabbitWorkerQueue()
        {
            SetupRabbit();

            Console.WriteLine("Delete all from Rabbit Worker Queue");
            using (var connection = rabbitFactory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDelete(queue: "WorkerQueue");
                // declaring a queue is idempotent - will only be created if doesn't exist already
                channel.QueueDeclare(queue: "WorkerQueue", durable: true, exclusive: false, autoDelete: false, arguments: null);

                //var result = channel.QueueDeclarePassive("WorkerQueue");
                //var messagesLeftOnWorkerQueue = result?.MessageCount ?? 0;

                //var consumer = new QueueingBasicConsumer(channel);
                //channel.BasicConsume("WorkerQueue", false, consumer);
                //for (int i = 0; i < messagesLeftOnWorkerQueue; i++)
                //{
                //    var deliveryArgs = consumer.Queue.Dequeue();
                //    channel.BasicAck(deliveryArgs.DeliveryTag, false);
                //}
            }
        }

        public static void PopulateRabbitWorkerQueueWithFilenames()
        {
            SetupRabbit();

            using (var connection = rabbitFactory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                // declaring a queue is idempotent - will only be created if doesn't exist already
                channel.QueueDeclare(queue: "WorkerQueue", durable: true, exclusive: false, autoDelete: false, arguments: null);

                string[] files;
                //if (Environment.MachineName == "DAVIDMA4" || Environment.MachineName == "XPS")
                files = Directory.GetFiles(@"c:\Tweets\split\", "*.json");
                // else // Azure server
                // files = Directory.GetFiles(@"f:\Tweets\", "*.json");

                foreach (var file in files)
                {
                    var body = Encoding.UTF8.GetBytes(file);
                    channel.BasicPublish(exchange: "", routingKey: "WorkerQueue", basicProperties: null, body: body);
                }
            }
        }

        private static void SetupRabbit()
        {
            rabbitFactory = new ConnectionFactory { HostName = "localhost" };
            if (Environment.MachineName == "XPS")
                rabbitFactory = new ConnectionFactory { HostName = "dom", UserName = "dave", Password = "zp2737AA" };
        }

        // Helpers for testing
        public static void ClearMSSQLDatabaseAndInsertSeedLanguages()
        {
            using (var db = GetOpenConnection())
            {
                var sql = @"alter table [dbo].[Tweets] drop constraint fk_tweets_users
                            alter table [dbo].tweethashtag drop constraint FK_TweetHashTag_Tweets
                            alter table [dbo].tweethashtag drop constraint FK_TweetHashTag_HashTags

                            truncate table users
                            truncate table tweets
                            truncate table languages
                            truncate table hashtags
                            truncate table tweethashtag

                            ALTER TABLE [dbo].[Tweets]  WITH CHECK ADD  CONSTRAINT [FK_Tweets_Users] FOREIGN KEY([UserID]) REFERENCES [dbo].[Users] ([UserID])
                            ALTER TABLE [dbo].tweethashtag  WITH CHECK ADD  CONSTRAINT FK_TweetHashTag_Tweets FOREIGN KEY([TweetID]) REFERENCES [dbo].Tweets (TweetID)
                            ALTER TABLE [dbo].tweethashtag  WITH CHECK ADD  CONSTRAINT FK_TweetHashTag_HashTags FOREIGN KEY([HashTagID]) REFERENCES [dbo].HashTags (HashTagID)

                            truncate table langtmp
                            truncate table userstmp
                            truncate table tweetstmp
                            truncate table tweetHashTagTmp
                            truncate table hashtagstmp
  
                            DBCC CHECKIDENT ('[Users]', RESEED, 1);
                            DBCC CHECKIDENT ('[Tweets]', RESEED, 1);
                            -- Identities are handled by Redis (so Identity is not on in these SQLServer tables)
                            --DBCC CHECKIDENT ('[languages]', RESEED, 1);
                            --DBCC CHECKIDENT ('[hashtags]', RESEED, 1);
                            DBCC CHECKIDENT ('[tweethashtag]', RESEED, 1);

                            truncate table [dbo].[MiniProfilerTimings]
                            truncate table [dbo].[MiniProfilers]
                            ";
                db.Execute(sql);

                sql = @"
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (1, N'aa', N'Afar')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (2, N'ab', N'Abkhazian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (3, N'ae', N'Avestan')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (4, N'af', N'Afrikaans')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (5, N'ak', N'Akan')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (6, N'am', N'Amharic')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (7, N'an', N'Aranese')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (8, N'ar', N'Arabic')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (9, N'as', N'Assamese')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (10, N'av', N'Avaric')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (11, N'ay', N'Aymara')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (12, N'az', N'Azerbaijani')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (13, N'ba', N'Bashkir')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (14, N'be', N'Belarusian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (15, N'bg', N'Bulgarian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (16, N'bi', N'Bislama')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (17, N'bm', N'Bambara')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (18, N'bn', N'Bengali')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (19, N'bo', N'Tibetan')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (20, N'br', N'Breton')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (21, N'bs', N'Bosnian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (22, N'ca', N'Catalan')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (23, N'ce', N'Chechen')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (24, N'ch', N'Chamorro')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (25, N'co', N'Corsican')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (26, N'cr', N'Cree')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (27, N'cs', N'Czech')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (28, N'cu', N'Church Slavic')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (29, N'cv', N'Chuvash')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (30, N'cy', N'Welsh')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (31, N'da', N'Danish')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (32, N'de', N'German')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (33, N'dv', N'Dhivehi')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (34, N'dz', N'Dzongkha')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (35, N'ee', N'Ewe')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (36, N'el', N'Modern Greek (1453-)')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (37, N'en', N'English')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (38, N'eo', N'Esperanto')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (39, N'es', N'Spanish')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (40, N'et', N'Estonian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (41, N'eu', N'Basque')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (42, N'fa', N'Persian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (43, N'ff', N'Fulah')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (44, N'fi', N'Finnish')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (45, N'fj', N'Fijian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (46, N'fo', N'Faroese')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (47, N'fr', N'French')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (48, N'fy', N'Western Frisian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (49, N'ga', N'Irish')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (50, N'gd', N'Scottish Gaelic')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (51, N'gl', N'Galician')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (52, N'gn', N'Guarani')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (53, N'gu', N'Gujarati')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (54, N'gv', N'Manx')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (55, N'ha', N'Hausa')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (56, N'he', N'Hebrew')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (57, N'hi', N'Hindi')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (58, N'ho', N'Hiri Motu')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (59, N'hr', N'Croatian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (60, N'ht', N'Haitian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (61, N'hu', N'Hungarian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (62, N'hy', N'Armenian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (63, N'hz', N'Herero')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (64, N'ia', N'Interlingua (International Auxiliary Language Association)')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (65, N'id', N'Indonesian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (66, N'ie', N'Interlingue')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (67, N'ig', N'Igbo')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (68, N'ii', N'Sichuan Yi')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (69, N'ik', N'Inupiaq')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (70, N'io', N'Ido')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (71, N'is', N'Icelandic')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (72, N'it', N'Italian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (73, N'iu', N'Inuktitut')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (74, N'ja', N'Japanese')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (75, N'jv', N'Javanese')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (76, N'ka', N'Georgian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (77, N'kg', N'Kon')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (78, N'ki', N'Kikuyu')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (79, N'kj', N'Kuanyama')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (80, N'kk', N'Kazakh')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (81, N'kl', N'Kalaallisut')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (82, N'km', N'Central Khmer')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (83, N'kn', N'Kannada')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (84, N'ko', N'Korean')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (85, N'kr', N'Kanuri')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (86, N'ks', N'Kashmiri')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (87, N'ku', N'Kurdish')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (88, N'kv', N'Komi')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (89, N'kw', N'Cornish')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (90, N'ky', N'Kirghiz')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (91, N'la', N'Latin')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (92, N'lb', N'Luxembourgish')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (93, N'lg', N'Ganda')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (94, N'li', N'Limburgan')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (95, N'ln', N'Lingala')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (96, N'lo', N'Lao')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (97, N'lt', N'Lithuanian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (98, N'lu', N'Luba-Katanga')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (99, N'lv', N'Latvian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (100, N'mg', N'Malagasy')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (101, N'mh', N'Marshallese')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (102, N'mi', N'Maori')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (103, N'mk', N'Macedonian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (104, N'ml', N'Malayalam')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (105, N'mn', N'Monlian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (106, N'mr', N'Marathi')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (107, N'ms', N'Malay (macrolanguage)')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (108, N'mt', N'Maltese')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (109, N'my', N'Burmese')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (110, N'na', N'Nauru')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (111, N'nb', N'Norwegian Bokmål')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (112, N'nd', N'North Ndebele')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (113, N'ne', N'Nepali (macrolanguage)')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (114, N'ng', N'Ndonga')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (115, N'nl', N'Dutch')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (116, N'nn', N'Norwegian Nynorsk')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (117, N'no', N'Norwegian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (118, N'nr', N'South Ndebele')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (119, N'nv', N'Navajo')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (120, N'ny', N'Nyanja')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (121, N'oc', N'Occitan (post 1500)')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (122, N'oj', N'Ojibwa')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (123, N'om', N'Oromo')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (124, N'or', N'Oriya (macrolanguage)')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (125, N'os', N'Ossetian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (126, N'pa', N'Panjabi')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (127, N'pi', N'Pali')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (128, N'pl', N'Polish')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (129, N'ps', N'Pushto')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (130, N'pt', N'Portuguese')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (131, N'qu', N'Quechua')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (132, N'rm', N'Romansh')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (133, N'rn', N'Rundi')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (134, N'ro', N'Romanian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (135, N'ru', N'Russian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (136, N'rw', N'Kinyarwanda')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (137, N'sa', N'Sanskrit')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (138, N'sc', N'Sardinian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (139, N'sd', N'Sindhi')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (140, N'se', N'Northern Sami')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (141, N'sg', N'San')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (142, N'sh', N'Serbo-CroatianCode element for 639-1 has been deprecated')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (143, N'si', N'Sinhala')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (144, N'sk', N'Slovak')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (145, N'sl', N'Slovenian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (146, N'sm', N'Samoan')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (147, N'sn', N'Shona')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (148, N'so', N'Somali')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (149, N'sq', N'Albanian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (150, N'sr', N'Serbian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (151, N'ss', N'Swati')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (152, N'st', N'Southern Sotho')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (153, N'su', N'Sundanese')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (154, N'sv', N'Swedish')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (155, N'sw', N'Swahili (macrolanguage)')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (156, N'ta', N'Tamil')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (157, N'te', N'Telugu')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (158, N'tg', N'Tajik')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (159, N'th', N'Thai')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (160, N'ti', N'Tigrinya')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (161, N'tk', N'Turkmen')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (162, N'tl', N'Tagalog')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (163, N'tn', N'Tswana')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (164, N'to', N'Tonga (Tonga Islands)')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (165, N'tr', N'Turkish')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (166, N'ts', N'Tsonga')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (167, N'tt', N'Tatar')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (168, N'tw', N'Twi')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (169, N'ty', N'Tahitian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (170, N'ug', N'Uighur')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (171, N'uk', N'Ukrainian')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (172, N'ur', N'Urdu')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (173, N'uz', N'Uzbek')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (174, N've', N'Venda')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (175, N'vi', N'Vietnamese')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (176, N'vo', N'Volapük')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (177, N'wa', N'Walloon')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (178, N'wo', N'Wolof')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (179, N'xh', N'Xhosa')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (180, N'yi', N'Yiddish')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (181, N'yo', N'Yoruba')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (182, N'za', N'Zhuang')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (183, N'zh', N'Chinese')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (184, N'zu', N'Zulu')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (185, N'und', N'Undisclosed')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (186, N'in', N'Indonesian (new)')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (187, N'ckb', N'Iraq')
                        INSERT [dbo].[Languages] ([LanguageID], [ShortCode], [Name]) VALUES (188, N'iw', N'Hebrew')
                        
                        --INSERT dbo.HashTags(HashTagID, Name) VALUES (1, 'Scotland')
                        ";
                db.Execute(sql);
            }
        }

        //http://stackoverflow.com/questions/25074788/how-to-excecute-flush-commands-on-stackexchange-redis-client-using-c-sharp
        public static void FlushAllDatabasesFromRedis()
        {
            var redisConnection = RedisConnectionFactory.GetConnection();
            var endpoints = redisConnection.GetEndPoints(true);
            foreach (var endpoint in endpoints)
            {
                var server = redisConnection.GetServer(endpoint);
                server.FlushAllDatabases();
            }
        }

        public static void UpdateRedisLanguageCacheAndLargestLanguageIDFromMSSQL()
        {
            var redisConnection = RedisConnectionFactory.GetConnection();
            var redis = redisConnection.GetDatabase();
            using (var db = GetOpenConnection())
            {
                var sql = "SELECT LanguageID, ShortCode FROM Languages ORDER BY LanguageID";
                IEnumerable<dynamic> languages = db.Query(sql);

                var largestLanguageID = 0;
                foreach (var language in languages)
                {
                    var langRedisKey = $"Language:{language.ShortCode}";
                    // Does it exist already in redis?
                    var exists = redis.KeyExists(langRedisKey);
                    if (!exists)
                        redis.StringSet(langRedisKey, language.LanguageID);

                    largestLanguageID = language.LanguageID;
                }
                redis.StringSet("LargestLanguageID", largestLanguageID);
            }
        }

        public static int GetLargestLanguageIDFromRedis()
        {
            var redisConnection = RedisConnectionFactory.GetConnection();
            var redis = redisConnection.GetDatabase();

            return (int)redis.StringGet("LargestLanguageID");
        }

        public static void UpdateRedisHashTagCacheAndLargestHashTagIDFromMSSQL()
        {
            var redisConnection = RedisConnectionFactory.GetConnection();
            var redis = redisConnection.GetDatabase();

            using (var db = GetOpenConnection())
            {
                var sql = "SELECT HashTagID, Name FROM HashTags ORDER BY HashTagID";
                IEnumerable<dynamic> hashTags = db.Query(sql);

                var largestHashTagID = 0;
                foreach (var hashTag in hashTags)
                {
                    var hashTagRedisKey = $"HashTag:{hashTag.Name}";
                    // exists already?
                    var exists = redis.KeyExists(hashTagRedisKey);
                    if (!exists)
                        redis.StringSet(hashTagRedisKey, hashTag.HashTagID);

                    largestHashTagID = hashTag.HashTagID;
                }
                redis.StringSet("LargestHashTagID", largestHashTagID);
            }
        }

        public static void CheckHashTagsInDBAreActuallyInTweetText()
        {
            using (var db = GetOpenConnection())
            {
                var sql = "SELECT t.TweetID, t.Text FROM Tweets t";
                var tweets = db.Query<Tweet2>(sql);

                foreach (var tweet in tweets)
                {
                    var text = tweet.Text;
                    var tweetID = tweet.TweetID;
                    var sql2 = $@"SELECT ht.Name FROM HashTags ht
                                join TweetHashTag tht on ht.HashTagID = tht.HashTagID
                                WHERE tht.TweetID = {tweetID}";
                    var htNames = db.Query<string>(sql2);
                    // does the text contain the hastag that we have linked to in our db?
                    foreach (var htName in htNames)
                    {
                        if (!text.Contains(htName))
                            Console.WriteLine("***text doesn't contain hashtag");
                    }

                    // are all the hastags in the text in our db
                    IEnumerable<string> tags = Regex.Split(text, @"\s+").Where(i => i.StartsWith("#"));
                    foreach (var tag in tags)
                    {
                        // chop off the initial #
                        var x = tag.Substring(1);
                        //
                        var index = x.IndexOf("'");
                        if (index != -1)
                            x = x.Substring(0, index);

                        index = x.IndexOf(".");
                        if (index != -1)
                            x = x.Substring(0, index);

                        index = x.IndexOf("?");
                        if (index != -1)
                            x = x.Substring(0, index);

                        index = x.IndexOf(",");
                        if (index != -1)
                            x = x.Substring(0, index);

                        index = x.IndexOf(":");
                        if (index != -1)
                            x = x.Substring(0, index);

                        index = x.IndexOf("!");
                        if (index != -1)
                            x = x.Substring(0, index);

                        // if a person has used # in the text with a space afterwards
                        if (tag == "#") continue;

                        // this is not 3 dots!
                        index = x.IndexOf("…");
                        if (index != -1)
                            x = x.Substring(0, index);

                        //"#LoMejorDeMi💕"
                        index = x.IndexOf("💕");
                        if (index != -1)
                            x = x.Substring(0, index);

                        if (!htNames.Contains(x))
                            Console.WriteLine($"could not find {tag} in our list");
                    }
                }
            }
        }
    }


    public class Tweet2
    {
        public int TweetID { get; set; }
        public string Text { get; set; }
    }
}
