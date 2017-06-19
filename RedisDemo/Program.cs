using System;
using StackExchange.Redis;

namespace RedisDemo
{
    class Program
    {
        static ConnectionMultiplexer redisConnection = RedisConnectionFactory.GetConnection();
        static IDatabase redis = redisConnection.GetDatabase();

        static void Main()
        {
            FlushAllDatabasesFromRedis();

            SetValue();
            GetValue();

            //Set a list of Users
            SetListOfUserIDs();
            LookupIDOfUser("Roy");
            LookupIDOfUser("Bob");
            Console.WriteLine("done");
        }

        static void SetValue()
        {
            redis.StringSet("AnswerToLife", 42);
        }

        static void GetValue()
        {
            var answerToLife = redis.StringGet("AnswerToLife");
            Console.WriteLine(answerToLife);
        }

        private static void SetListOfUserIDs()
        {
            // Lookup the Name and get the ID back
            redis.StringSet("User:John", 1);
            redis.StringSet("User:Roy", 2);
            redis.StringSet("User:Duncan", 3);

            redis.StringSet("LargestUserID", 3);
        }

        private static void LookupIDOfUser(string userName)
        {
            var userIDRedisKey = $"User:{userName}";
            // Does it exist already in redis?
            var userID = redis.StringGet(userIDRedisKey);

            if (userID.IsNullOrEmpty)
            {
                userID = redis.StringIncrement("LargestUserID");
                redis.StringSet(userIDRedisKey, userID);
                Console.WriteLine($"Set a new user in redis {userID}");
            }
            else
                Console.WriteLine($"Redis hit: UserID {userID}");
        }



        public static void FlushAllDatabasesFromRedis()
        {
            var endpoints = redisConnection.GetEndPoints(true);
            foreach (var endpoint in endpoints)
            {
                var server = redisConnection.GetServer(endpoint);
                server.FlushAllDatabases();
            }
        }
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
}
