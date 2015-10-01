CREATE TABLE [etl].[EtlVariables] (
    [EtlPackageId]   UNIQUEIDENTIFIER NOT NULL,
    [EtlSessionId]   UNIQUEIDENTIFIER NOT NULL,
    [Name]           NVARCHAR (50)    NOT NULL,
    [Modifier]       INT              NOT NULL,
    [Value]          NVARCHAR (1000)  NULL,
    [IsSecure]       BIT              NOT NULL,
    [LogDateTime]    DATETIME         NOT NULL,
    [LogUtcDateTime] DATETIME         NOT NULL,
    CONSTRAINT [FK_EtlVariables_EtlPackageId] FOREIGN KEY ([EtlPackageId]) REFERENCES [etl].[EtlPackages] ([Id]),
    CONSTRAINT [FK_EtlVariables_EtlSessions] FOREIGN KEY ([EtlSessionId]) REFERENCES [etl].[EtlSessions] ([EtlSessionId])
);

