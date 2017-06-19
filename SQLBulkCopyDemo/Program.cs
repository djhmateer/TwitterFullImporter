using Dapper;
using FastMember;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Transactions;

namespace SQLBulkCopyDemo
{
    class Program
    {
        static void Main()
        {
            // Show how to get a large amount of data into MSSQL quickly
            Console.WriteLine("Truncating everthing in SQL");
            Util.ClearAllTablesInDatabase();

            Console.WriteLine("Generting user data");
            List<User> usersData = GenerateUsersData();

            BCP(usersData);
            Console.WriteLine("Done");
        }

        class User
        {
            public string Name { get; set; }
            public long UserIDFromTwitter { get; set; }
        }

        private static List<User> GenerateUsersData()
        {
            var x = new List<User>();

            for (int i = 0; i < 200000; i++)
            {
                var u = new User
                {
                    UserIDFromTwitter = i,
                    Name = $"Bob{i}"
                };
                x.Add(u);
            }
            return x;
        }

        static void BCP(List<User> usersData)
        {
            int totalRowsInserting = usersData.Count;
            var sw = new Stopwatch();
            sw.Start();
            using (var tran = new TransactionScope())
            using (var db = Util.GetOpenConnection())
            {
                // 1 create #Temp
                var sql = @"CREATE TABLE #UsersTmp(
	                            [Name] [nvarchar](255) NOT NULL,
	                            [UserIDFromTwitter] [bigint] NOT NULL
                            );";
                db.Execute(sql);


                // 2 BCP into #Temp
                Console.WriteLine($"BCPing {totalRowsInserting} rows into #Temp tables");
                using (var bcp = new SqlBulkCopy(db))
                {
                    // Fastmember by Mark Gravell to give the shape of the data we need to pass to SqlBulkCopy
                    // usersData is a List<User> (Name and UserIDFromTwitter)
                    using (var reader = ObjectReader.Create(usersData, "Name", "UserIDFromTwitter"))
                    {
                        bcp.DestinationTableName = "#UsersTmp";
                        bcp.WriteToServer(reader);
                    }
                }

                // 3 Copy from #Temp to Temp
                Console.WriteLine("Inserting from #Temp to Temp tables");
                var sql2 = @"
                        INSERT INTO UsersTmp 
                        SELECT *
                        FROM #UsersTmp";
                db.Execute(sql2);

                // 4 Main table insert
                Console.WriteLine("Starting Insert into main tables - will cause locks to oltp");
                var sw2 = new Stopwatch();
                sw2.Start();
                var sql3 = @"
                            -- Insert any new Users
                            INSERT INTO Users 
                                (Name, UserIDFromTwitter) 
                                SELECT ut.Name,ut.UserIDFromTwitter 
                                FROM   UsersTmp ut 
                                WHERE  NOT EXISTS (SELECT UserIDFromTwitter 
                                                    FROM   Users u 
                                                    WHERE  u.UserIDFromTwitter = ut.UserIDFromTwitter) 
                            ";

                db.Execute(sql3);
                sw2.Stop();
                Console.WriteLine("Inserting from Temp tables to Main tables {0}", sw2.ElapsedMilliseconds);
                tran.Complete();
            }

            sw.Stop();
            var elapsedMilliseconds = sw.ElapsedMilliseconds;
            Console.WriteLine($"time: {elapsedMilliseconds}");
        }


    }
}
