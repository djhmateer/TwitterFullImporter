CREATE TABLE [dbo].[TweetsTmp] (
    [Text]                 NVARCHAR (1024) NOT NULL,
    [UserIDFromTwitter]    BIGINT          NOT NULL,
    [TweetIDFromTwitter]   BIGINT          NOT NULL,
    [Lang]                 NVARCHAR (50)   NULL,
    [LanguageID]           INT             NULL,
    [TimeInserted]         DATETIME2 (7)   NULL,
    [CreatedAtFromTwitter] DATETIME2 (7)   NULL
);





