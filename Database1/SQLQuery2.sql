select count(*) from users
select count(*) from tweets

select * from users

select * from tweets
order by tweetid desc

select * from tweets
where TimeInserted is not null
order by TweetID desc

select distinct lang from tweets

-- who has the most tweets
select t.userid, count(t.userid), u.Name 
from tweets t
join users u on t.UserID = u.UserID
group by t.userid, u.Name
order by count(t.userid) desc


--delete from Users
--truncate table tweets


DROP TABLE TweetsTmp

CREATE TABLE [dbo].[TweetsTmp](
	[Text] [nvarchar](1024) NOT NULL,
	[UserIDFromTwitter] [bigint] NOT NULL,
	[TweetIDFromTwitter] [bigint] NOT NULL,
	[Lang] [nvarchar](50) NULL,
	[TimeInserted] [datetime2](7) NULL
)

CREATE TABLE [dbo].[UsersTmp](
	[Name] [nvarchar](255) NOT NULL,
	[UserIDFromTwitter] [bigint] NOT NULL
)

truncate table TweetsTmp
truncate table UsersTmp

select * from TweetsTmp
order by userid desc
delete from users where userid=16762



-- so have data in Tmp tables and want to copy to real tables
-- UsersTmp has duplicates (which is valid)
--http://stackoverflow.com/questions/18390574/how-to-delete-duplicate-rows-in-sql-server
WITH CTE AS(
   SELECT [Name], [UserIDFromTwitter],
       RN = ROW_NUMBER()OVER(PARTITION BY [UserIDFromTwitter] ORDER BY [UserIDFromTwitter])
   FROM UsersTmp
)
DELETE FROM CTE WHERE RN > 1



truncate table TweetsTmp
truncate table UsersTmp

select * from users

select * from TweetsTmp
where text like '%feynman%'

select * from Tweets
where text like '%feynman%'
order by TweetIDFromTwitter

delete from UsersTmp --where UserIDFromTwitter not in (22649409, 159446900, 30332845)
delete from users
delete from tweets

alter table TweetURL drop constraint FK_TweetURL_Tweets
alter table TweetUserMention drop constraint FK_TweetUserMention_Tweets

alter table [dbo].[Tweets] drop constraint fk_tweets_users

truncate table users
truncate table tweets
truncate table languages
truncate table hashtags
truncate table tweethashtag
--ALTER TABLE [dbo].[Tweets]  WITH NOCHECK ADD  CONSTRAINT [FK_Tweets_Users] FOREIGN KEY([UserID]) REFERENCES [dbo].[Users] ([UserID])
truncate table userstmp
truncate table tweetstmp
truncate table tweetHashTagTmp
DBCC CHECKIDENT ('[Users]', RESEED, 1);
DBCC CHECKIDENT ('[Tweets]', RESEED, 1);
DBCC CHECKIDENT ('[languages]', RESEED, 1);
DBCC CHECKIDENT ('[hashtags]', RESEED, 1);
DBCC CHECKIDENT ('[tweethashtag]', RESEED, 1);

sp_help 'Users'

select top 5 * from Tweets
where lang != 'en'

insert into Languages(ShortCode)
select distinct Lang from Tweets

-- 25,000 t/sec (4)
-- 29,000 (6)

-- 11,459,168
select count(*) from Tweets
--  2,275,517
select count(*) from Users
-- 64
select count(*) from languages
-- 859,477 (478 is an error - still have it saved in HashTagDave)
select count(*) from hashtags

-- compare hashtags
-- with hashtagsdave

-- 859,478
select count(*) from hashtagsdave
-- 859,477
select count(*) from hashtags

-- all okay
select top 10 name from HashTagsdave d
where not exists (select name from hashtags t where t.Name = d.name)

select top 10 * from hashtagsdave order by name asc
select top 10 * from hashtags order by name asc

--select top 10 * from hashtagsdave order by name COLLATE Latin1_General_CS_AS

-- binary collation (this gives same sort order data)
select count(*) from hashtagsdave order by name COLLATE Latin1_General_bin
select count(*) from hashtags order by name COLLATE Latin1_General_bin

-- 747,074
select distinct name from hashtagsdave
-- 747,074 (there are 859,477 in redis)
select distinct name from hashtags

--problem - 
-- 859,478
select count(*) from hashtagsdave
-- 859,477
select count(*) from hashtags

-- loeuvreaunoir is in twice in hashtagsdave
-- at position 580,156
select name from hashtagsdave order by name COLLATE Latin1_General_bin 
select name from hashtags order by name COLLATE Latin1_General_bin 

select


select name COLLATE Latin1_General_bin from HashTagsdave d
--where not exists (select name from hashtags)
where name COLLATE Latin1_General_bin not in (select name COLLATE Latin1_General_bin from hashtags)

-- 859,109
select distinct name COLLATE Latin1_General_CS_AS from HashTagsdave
-- 859,109
select distinct name COLLATE Latin1_General_CS_AS from HashTags

-- ahh so there are duplicates in there!!!
-- so according to this there should only be 859,109 in 
--**HERE**
-- loeuvreaunoir
--**THIS GOT THE Actual duplicate and not just case**
select Name COLLATE Latin1_General_bin, count(Name COLLATE Latin1_General_bin)
from HashTags
--where Name='loeuvreaunoir'
group by Name COLLATE Latin1_General_bin
order by count(Name COLLATE Latin1_General_bin) desc

-- have got a dupe in pt
select distinct shortcode from languages
select * from Languages

select top 10 * from TweetsTmp

--select * from tweetHashTagTmp
select * from HashTags 
select * from tweethashtag

-- which hashtag is used the most and a count
select  ht.Name, count(tht.hashtagid), ht.HashTagID
from TweetHashTag tht
join hashtags ht on ht.HashTagID = tht.HashTagID
group by tht.hashtagid, ht.Name, ht.HashTagID
order by count(tht.hashtagid) desc

-- looks like 1776 retweets of brexit bill
select t.TweetID, t.text, ht.name from tweets t
join TweetHashTag tht on t.TweetID = tht.TweetID
join HashTags ht on ht.HashTagID = tht.HashTagID
--where ht.HashTagID = 224 --brexit
--where ht.HashTagID = 28 -- entomology
--where ht.Name = 'syrphid' 





select * from Tweets
where Text like '%entomology%' 

-- get tweets by language
select t.languageid, count(t.languageid), l.Name
from tweets t
join languages l on t.LanguageID = l.LanguageID
group by t.languageID, l.Name
order by count(t.languageid) desc

-- find duplicates in HashTags
select Name COLLATE Latin1_General_CS_AS, count(Name COLLATE Latin1_General_CS_AS)
from hashtags
group by Name COLLATE Latin1_General_CS_AS
order by count(Name COLLATE Latin1_General_CS_AS) desc

-- take of the duplicate hashtags
select * from hashtags
where name = 'thoughts'

-- undefeated
select * from hashtags where name = 'undefeated'
select * from hashtags where name = 'Mieli'
select * from hashtags where name = 'classicpicsart'

-- 859,478
select * 
into hashtagsdave
from hashtags









-- russian tweets
select * from tweets where LanguageID = 8

select * from tweets where LanguageID = 15

-- 7992 tweets in total????
select * from tweets
order by CreatedAtFromTwitter desc




insert into tweethashtag(TweetIDFromTwitter, TweetID, HashTagID)
select htt.TweetIDFromTwitter, t.tweetID, htt.HashTagID
from tweetHashTagTmp htt
join Tweets t on t.TweetIDFromTwitter = htt.TweetIDFromTwitter

select * from languages
select top 10 * from Tweets 
where text not like 'rt%'
and text not like '@%'

select top 10 * from Users

-- diagram stuff
-- List all database diagrams
-- diagram_id = 1
-- name = Diagram_0
--SELECT * FROM SimpleTwitter.[dbo].sysdiagrams

--http://stackoverflow.com/a/21916076/26086

SELECT
    'DECLARE @def AS VARBINARY(MAX) ; ' +
    'SELECT @def = CONVERT(VARBINARY(MAX), 0x' + CONVERT(NVARCHAR(MAX), [definition], 2) + ', 2) for xml auto;' +
    ' EXEC dbo.sp_creatediagram' +
        ' @diagramname=''' + [name] + ''',' +
        ' @version=' + CAST([version] AS NVARCHAR(MAX)) + ',' +
        ' @definition=@def'
    AS ExportQuery
	
FROM
    [dbo].[sysdiagrams]
WHERE
    [name] = 'Diagram_0' -- Diagram Name


select * from tweets
where userid>5
select * from users



 SELECT t.*, u.Name FROM Tweets t
 JOIN Users u ON u.UserID = t.UserID

select * from users

select * from Tweets
 where Lang != 'en'

select * from UsersTmp

select * from TweetsTmp

select * from TweetsTmp where UserIDFromTwitter in (22649409, 159446900, 30332845)
delete from TweetsTmp where UserIDFromTwitter not in (22649409, 159446900)
delete from UsersTmp

-- 3 statement trick - more performant than merge?
--http://stackoverflow.com/questions/39521423/sql-server-stored-procedure-update-table-insert-new-rows-update-existing?noredirect=1&lq=1
-- Users
--1. update statement with inner join 
--2. insert any new users 
--http://stackoverflow.com/questions/9230878/how-can-i-perform-a-sql-not-in-query-faster
insert into users(Name,UserIDFromTwitter) 
select ut.Name,ut.UserIDFromTwitter
from UsersTmp ut
where not exists (select UserIDFromTwitter from users u where u.UserIDFromTwitter = ut.UserIDFromTwitter)
--3. then a delete statement constrained to non joined records

-- Tweets (all users should be in the users table now)
insert into tweets(text,userid, TweetIDFromTwitter, lang,TimeInserted)
select text,u.UserID,TweetIDFromTwitter,lang,TimeInserted
from TweetsTmp tt
join users u on u.UserIDFromTwitter = tt.UserIDFromTwitter
-- check not a duplicate tweet insert with whatever is in Tweets now
-- have already checked for duplicates in TweetTmp
where not exists (select TweetIDFromTwitter from tweets t where t.TweetIDFromTwitter = tt.TweetIDFromTwitter)


-- ahh there are duplicate TweetIDFromTwitter
--
select TweetIDFromTwitter, count(TweetIDFromTwitter)
from Tweets
group by TweetIDFromTwitter
order by count(TweetIDFromTwitter) desc

-- 477,008
select distinct TweetIDFromTwitter
from Tweets











-- this in Yonz
delete from UsersTmp where UserIDFromTwitter=30332845


--http://stackoverflow.com/questions/14806768/sql-merge-statement-to-update-data
declare @recno table(UserIDFromTwitter bigint, UserID int);
-- Update o
MERGE INTO Users WITH (HOLDLOCK) AS target
USING UsersTmp source
  ON target.UserIDFromTwitter = source.UserIDFromTwitter
-- User is in both Source and Target
WHEN MATCHED THEN
  -- hack so can get the UserID into the OUTPUT
  UPDATE SET target.Name = source.Name
-- User is in Source but not Target
WHEN NOT MATCHED BY TARGET THEN
  INSERT ([Name], UserIDFromTwitter)
  VALUES (source.[Name], source.UserIDFromTwitter)
OUTPUT source.UserIDFromTwitter, inserted.UserID into @recno;

--**HERE** need to get the UserID when MATCHED
-- works well when NOT MATCHED (ie user not there already)
-- we want the UserID if it is there already

INSERT INTO Tweets([Text], UserID, TweetIDFromTwitter, Lang, TimeInserted)
SELECT TT.Text, R.UserID, TT.UserIDFromTwitter, TT.Lang, TT.TimeInserted
FROM TweetsTmp TT
JOIN @recno as R ON TT.UserIDFromTwitter = R.UserIDFromTwitter



