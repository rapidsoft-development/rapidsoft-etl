CREATE TABLE [etl].[EtlPackages] (
    [Id]                 UNIQUEIDENTIFIER NOT NULL,
    [Name]               NVARCHAR (255)   NOT NULL,
    [Text]               NVARCHAR (MAX)   NULL,
    [RunIntervalSeconds] INT              CONSTRAINT [DF_EtlPackages_RunIntervalSeconds] DEFAULT ((0)) NOT NULL,
    [Enabled]            BIT              NULL,
    CONSTRAINT [PK_EtlPackages_Id] PRIMARY KEY NONCLUSTERED ([Id] ASC)
);

