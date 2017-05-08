CREATE TABLE [dbo].[MiniProfilers] (
    [RowId]                      INT              IDENTITY (1, 1) NOT NULL,
    [Id]                         UNIQUEIDENTIFIER NOT NULL,
    [RootTimingId]               UNIQUEIDENTIFIER NULL,
    [Started]                    DATETIME         NOT NULL,
    [DurationMilliseconds]       DECIMAL (7, 1)   NOT NULL,
    [User]                       NVARCHAR (100)   NULL,
    [HasUserViewed]              BIT              NOT NULL,
    [MachineName]                NVARCHAR (100)   NULL,
    [CustomLinksJson]            NVARCHAR (MAX)   NULL,
    [ClientTimingsRedirectCount] INT              NULL,
    CONSTRAINT [PK_MiniProfilers] PRIMARY KEY CLUSTERED ([RowId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_MiniProfilers_User_HasUserViewed_Includes]
    ON [dbo].[MiniProfilers]([User] ASC, [HasUserViewed] ASC)
    INCLUDE([Id], [Started]);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_MiniProfilers_Id]
    ON [dbo].[MiniProfilers]([Id] ASC);

