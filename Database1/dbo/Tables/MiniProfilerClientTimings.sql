CREATE TABLE [dbo].[MiniProfilerClientTimings] (
    [RowId]          INT              IDENTITY (1, 1) NOT NULL,
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [MiniProfilerId] UNIQUEIDENTIFIER NOT NULL,
    [Name]           NVARCHAR (200)   NOT NULL,
    [Start]          DECIMAL (9, 3)   NOT NULL,
    [Duration]       DECIMAL (9, 3)   NOT NULL,
    CONSTRAINT [PK_MiniProfilerClientTimings] PRIMARY KEY CLUSTERED ([RowId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_MiniProfilerClientTimings_MiniProfilerId]
    ON [dbo].[MiniProfilerClientTimings]([MiniProfilerId] ASC);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_MiniProfilerClientTimings_Id]
    ON [dbo].[MiniProfilerClientTimings]([Id] ASC);

