alter table [dbo].[Tweets] drop constraint fk_tweets_users
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
DBCC CHECKIDENT ('[Users]', RESEED, 1);
DBCC CHECKIDENT ('[Tweets]', RESEED, 1);
--DBCC CHECKIDENT ('[languages]', RESEED, 1);
--DBCC CHECKIDENT ('[hashtags]', RESEED, 1);
DBCC CHECKIDENT ('[tweethashtag]', RESEED, 1);


-- 592s with new lookup stuff
-- 11,459,168
select count(*) from Tweets
--  2,275,517
select count(*) from Users
-- 64
select count(*) from languages
-- 859,477 (478 is an error - still have it saved in HashTagDave)
select count(*) from hashtags

-- 137 locally xps and dom
-- 93s with 4 cores
-- 90s with 6 cores
-- 501,390
select count(*) from Tweets
--  281,455  (281,458??)
select count(*) from Users
-- 188
select count(*) from languages
-- 63,530
select count(*) from hashtags

-- 198 with 7 on e560 and 2 on xps (redis on xps)
-- waiting for sql a lot of the time


-- 530 with 5 on xps (reids on dom)
-- 200 with 7 on e560 and 4 on dom
-- move rabbit to dom
-- 184s with 7 on e560 and 4 on dom (but big differences)
-- then make files smaller!
-- 217 with 7 and 4 (10k file size)

-- 186 with 7 and 4 (25k files)

-- 1,504,458  (giving 7,500t/s)
select count(*) from Tweets
--  734,155
select count(*) from Users
-- 188
select count(*) from languages
-- 114,922
select count(*) from hashtags

-- 177s with 12 procs (but haven't done final insert)


-- Fri 24th work files
-- 104s with 7 procs going (no tablock)
-- 99s with tablock

-- 1,957,387
select count(*) from Tweets
--  703,510
select count(*) from Users
-- 188 (62 actually in this run)
select count(*) from languages
-- 179,533
select count(*) from hashtags

select count(*) from TweetsTmp

--XPS
-- 82s at 8
-- 75 at 12

-- WORK
-- 83s, 84 with only going into tweetsTmp
-- 80s going into all
-- 51s Tuesday (with MemoryCache on languages)
TRUNCATE TABLE LangTmp;
TRUNCATE TABLE UsersTmp; 
TRUNCATE TABLE TweetsTmp; 
TRUNCATE TABLE TweetHashTagTmp;
TRUNCATE TABLE HashTagsTmp

-- 2029 (dupes!)
 INSERT INTO Languages --WITH (TABLOCK)
    (LanguageID, Shortcode, Name) 
    SELECT distinct lt.LanguageID, lt.ShortCode , ''
	FROM   LangTmp lt 
	WHERE  NOT EXISTS (SELECT LanguageID 
						FROM   Languages l 
						WHERE  l.LanguageID = lt.LanguageID) 

-- 1,377,627 (716,713 distinct, should be 703,510 just on userIDFromTwitter)..hmmm have users changed their names?
select count(*) from userstmp
-- Insert any new Users
INSERT INTO Users --WITH (TABLOCK)
    (Name, UserIDFromTwitter) 
    SELECT distinct ut.Name,ut.UserIDFromTwitter 
	--SELECT distinct ut.UserIDFromTwitter 
    FROM   UsersTmp ut 
    WHERE  NOT EXISTS (SELECT UserIDFromTwitter 
                        FROM   Users u 
                        WHERE  u.UserIDFromTwitter = ut.UserIDFromTwitter) 

-- 1,957,403 (dupes)  1,957,387
select count(*) from TweetsTmp
select count(*) from tweets
-- Insert Tweets (all users are in now)
-- slow!!! 2:49
INSERT INTO Tweets --WITH (TABLOCK) 
    (CreatedAtFromTwitter,TweetIDFromTwitter,Text,UserID,LanguageID, TimeInserted) 
    SELECT CreatedAtFromTwitter, TweetIDFromTwitter, Text, u.UserID, tt.LanguageID, TimeInserted 
    FROM   TweetsTmp tt 
    JOIN Users u ON u.UserIDFromTwitter = tt.UserIDFromTwitter
    -- check not a duplicate tweet insert (this would happen if twitter sent a duplicate tweet which spanned import files)
    WHERE  NOT EXISTS (SELECT TweetIDFromTwitter 
                        FROM Tweets t 
                        WHERE  t.TweetIDFromTwitter = tt.TweetIDFromTwitter) 
                           
-- Insert any new HashTags

INSERT INTO HashTags --WITH (TABLOCK) 
    (HashTagID,Name) 
    SELECT htt.HashTagID, htt.Name 
	FROM   HashTagsTmp htt 
	WHERE  NOT EXISTS (SELECT ht.HashTagID
						FROM   HashTags ht 
						WHERE  ht.HashTagID = htt.HashTagID) 

-- Insert TweetHashTag (all Tweets nd all HashTags are in now)
INSERT INTO TweetHashTag --WITH (TABLOCK)
    (TweetIDFromTwitter, TweetID, HashTagID)
    SELECT thtt.TweetIDFromTwitter, t.tweetID, thtt.HashTagID
    FROM #TweetHashTagTmp thtt
    JOIN Tweets t on t.TweetIDFromTwitter = thtt.TweetIDFromTwitter










-- parallel?
truncate table langtmp

select * from Languages

select * from users
select * from Tweets
select * from TweetHashTag
select * from HashTags

-- create a table lock for 30s
-- so can test db timeouts
BEGIN TRAN  
SELECT 1 FROM TweetsTmp WITH (TABLOCKX)
WAITFOR DELAY '00:01:30' 
ROLLBACK TRAN   
GO 

EXEC sp_who2

SELECT
db.name DBName,
tl.request_session_id,
wt.blocking_session_id,
OBJECT_NAME(p.OBJECT_ID) BlockedObjectName,
tl.resource_type,
h1.TEXT AS RequestingText,
h2.TEXT AS BlockingTest,
tl.request_mode
FROM sys.dm_tran_locks AS tl
INNER JOIN sys.databases db ON db.database_id = tl.resource_database_id
INNER JOIN sys.dm_os_waiting_tasks AS wt ON tl.lock_owner_address = wt.resource_address
INNER JOIN sys.partitions AS p ON p.hobt_id = tl.resource_associated_entity_id
INNER JOIN sys.dm_exec_connections ec1 ON ec1.session_id = tl.request_session_id
INNER JOIN sys.dm_exec_connections ec2 ON ec2.session_id = wt.blocking_session_id
CROSS APPLY sys.dm_exec_sql_text(ec1.most_recent_sql_handle) AS h1
CROSS APPLY sys.dm_exec_sql_text(ec2.most_recent_sql_handle) AS h2
GO

 
-- check that the hashtags are correct
-- took mindful out of the 2nd tweet
select t.TweetID, t.Text, ht.Name from hashtags ht
join TweetHashTag tht on tht.HashTagID = ht.HashTagID
join Tweets t on t.TweetID = tht.TweetID
where ht.Name not in (select t.Text from Tweets t2 where t2.TweetID = t.TweetID)