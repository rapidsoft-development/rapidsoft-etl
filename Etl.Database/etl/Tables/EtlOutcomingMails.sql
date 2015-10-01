CREATE TABLE [etl].[EtlOutcomingMails] (
    [Id]           BIGINT           IDENTITY (1, 1) NOT NULL,
    [EtlPackageId] UNIQUEIDENTIFIER NOT NULL,
    [EtlSessionId] UNIQUEIDENTIFIER NOT NULL,
    [Subject]      NVARCHAR (MAX)   NOT NULL,
    [From]         NVARCHAR (MAX)   NOT NULL,
    [To]           NVARCHAR (MAX)   NOT NULL,
    [InsertedDate] DATETIME         DEFAULT (getdate()) NULL,
    CONSTRAINT [PK_EtlOutcomingMails] PRIMARY KEY CLUSTERED ([Id] ASC)
);

