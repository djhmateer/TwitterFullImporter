CREATE TABLE [dbo].[UserMentions] (
    [UserMentionID] INT            IDENTITY (1, 1) NOT NULL,
    [TweetID]       INT            NOT NULL,
    [ScreenName]    NVARCHAR (255) NOT NULL,
    CONSTRAINT [PK_UserMentions] PRIMARY KEY CLUSTERED ([UserMentionID] ASC)
);



