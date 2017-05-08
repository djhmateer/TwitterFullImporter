CREATE TABLE [dbo].[Languages] (
    [LanguageID] INT            NOT NULL,
    [ShortCode]  NVARCHAR (50)  NOT NULL,
    [Name]       NVARCHAR (255) NOT NULL,
    CONSTRAINT [PK_Language] PRIMARY KEY CLUSTERED ([LanguageID] ASC)
);





