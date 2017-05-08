CREATE TABLE [dbo].[MiniProfilerTimings] (
    [RowId]                INT              IDENTITY (1, 1) NOT NULL,
    [Id]                   UNIQUEIDENTIFIER NOT NULL,
    [MiniProfilerId]       UNIQUEIDENTIFIER NOT NULL,
    [ParentTimingId]       UNIQUEIDENTIFIER NULL,
    [Name]                 NVARCHAR (200)   NOT NULL,
    [DurationMilliseconds] DECIMAL (9, 3)   NOT NULL,
    [StartMilliseconds]    DECIMAL (9, 3)   NOT NULL,
    [IsRoot]               BIT              NOT NULL,
    [Depth]                SMALLINT         NOT NULL,
    [CustomTimingsJson]    NVARCHAR (MAX)   NULL,
    CONSTRAINT [PK_MiniProfilerTimings] PRIMARY KEY CLUSTERED ([RowId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_MiniProfilerTimings_MiniProfilerId]
    ON [dbo].[MiniProfilerTimings]([MiniProfilerId] ASC);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_MiniProfilerTimings_Id]
    ON [dbo].[MiniProfilerTimings]([Id] ASC);

