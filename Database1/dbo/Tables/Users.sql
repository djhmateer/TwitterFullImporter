CREATE TABLE [dbo].[Users] (
    [UserID]            INT             IDENTITY (1, 1) NOT NULL,
    [UserIDFromTwitter] BIGINT          NOT NULL,
    [Name]              NVARCHAR (255)  NOT NULL,
    [ScreenName]        NVARCHAR (255)  NULL,
    [Location]          NVARCHAR (255)  NULL,
    [URL]               NVARCHAR (255)  NULL,
    [Description]       NVARCHAR (1024) NULL,
    [FollowersCount]    INT             NULL,
    [FriendsCount]      INT             NULL,
    [FavouritesCount]   INT             NULL,
    [StatusesCount]     INT             NULL,
    [TimeZone]          NCHAR (10)      NULL,
    [TwitterCreatedAt]  DATETIME2 (7)   NULL,
    [LanguageID]        INT             NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([UserID] ASC)
);







