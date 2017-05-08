CREATE TABLE [dbo].[Places] (
    [PlaceID]     INT               NOT NULL,
    [PlaceType]   NCHAR (10)        NULL,
    [Name]        NVARCHAR (255)    NULL,
    [CountryCode] NVARCHAR (50)     NULL,
    [BoxLL]       [sys].[geography] NULL,
    [BoxUL]       [sys].[geography] NULL,
    [BoxUR]       [sys].[geography] NULL,
    [BoxLR]       [sys].[geography] NULL,
    CONSTRAINT [PK_Places] PRIMARY KEY CLUSTERED ([PlaceID] ASC)
);





