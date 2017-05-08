CREATE TABLE [dbo].[TweetHashTag] (
    [TweetHashTagID]     INT    IDENTITY (1, 1) NOT NULL,
    [TweetIDFromTwitter] BIGINT NOT NULL,
    [TweetID]            INT    NOT NULL,
    [HashTagID]          INT    NOT NULL,
    CONSTRAINT [PK_TweetsHashTags] PRIMARY KEY CLUSTERED ([TweetHashTagID] ASC),
    CONSTRAINT [FK_TweetHashTag_HashTags] FOREIGN KEY ([HashTagID]) REFERENCES [dbo].[HashTags] ([HashTagID]),
    CONSTRAINT [FK_TweetHashTag_Tweets] FOREIGN KEY ([TweetID]) REFERENCES [dbo].[Tweets] ([TweetID])
);



