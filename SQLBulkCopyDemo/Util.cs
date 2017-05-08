using Dapper;
using System.Data.SqlClient;

namespace SQLBulkCopyDemo
{
    public static class Util
    {
        public static SqlConnection GetOpenConnection()
        {
            var connection = new SqlConnection(GetConnectionString());
            connection.Open();
            return connection;
        }

        public static string GetConnectionString()
        {
            return @"Data Source=.\;initial catalog=SimpleTwitter;integrated security=True;MultipleActiveResultSets=True;";
        }

        public static void ClearAllTempTablesInDatabase()
        {
            using (var db = GetOpenConnection())
            {
                var sql = @"
                            truncate table userstmp
                            truncate table tweetstmp
                            ";
                db.Execute(sql);
            }
        }

        public static void ClearAllTablesInDatabase()
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
                            truncate table hashtagstmp
                            ALTER TABLE [dbo].[Tweets]  WITH CHECK ADD  CONSTRAINT [FK_Tweets_Users] FOREIGN KEY([UserID]) REFERENCES [dbo].[Users] ([UserID])
                            ALTER TABLE [dbo].tweethashtag  WITH CHECK ADD  CONSTRAINT FK_TweetHashTag_Tweets FOREIGN KEY([TweetID]) REFERENCES [dbo].Tweets (TweetID)
                            ALTER TABLE [dbo].tweethashtag  WITH CHECK ADD  CONSTRAINT FK_TweetHashTag_HashTags FOREIGN KEY([HashTagID]) REFERENCES [dbo].HashTags (HashTagID)

                            truncate table langtmp
                            truncate table userstmp
                            truncate table tweetstmp
                            truncate table tweetHashTagTmp
  
                            DBCC CHECKIDENT ('[Users]', RESEED, 1);
                            DBCC CHECKIDENT ('[Tweets]', RESEED, 1);
                            DBCC CHECKIDENT ('[tweethashtag]', RESEED, 1);

                            ";
                db.Execute(sql);
            }
        }
    }
}
