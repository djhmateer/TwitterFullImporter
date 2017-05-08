CREATE TABLE [dbo].[Tweets] (
    [TweetID]              INT               IDENTITY (1, 1) NOT NULL,
    [CreatedAtFromTwitter] DATETIME2 (7)     NULL,
    [TweetIDFromTwitter]   BIGINT            NOT NULL,
    [Text]                 NVARCHAR (1024)   NOT NULL,
    [UserID]               INT               NOT NULL,
    [Coordinates]          [sys].[geography] NULL,
    [PlaceID]              INT               NULL,
    [RetweetCount]         INT               NULL,
    [FavouriteCount]       INT               NULL,
    [LanguageID]           INT               NULL,
    [TimeInserted]         DATETIME2 (7)     NULL,
    CONSTRAINT [PK_Tweets] PRIMARY KEY CLUSTERED ([TweetID] ASC),
    CONSTRAINT [FK_Tweets_Users] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);




















GO
CREATE NONCLUSTERED INDEX [IX_Tweets_1]
    ON [dbo].[Tweets]([TweetID] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Tweets]
    ON [dbo].[Tweets]([CreatedAtFromTwitter] ASC);

