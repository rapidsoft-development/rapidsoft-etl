CREATE TABLE [etl].[EtlSessions] (
    [EtlSessionId]       UNIQUEIDENTIFIER NOT NULL,
    [EtlPackageId]       UNIQUEIDENTIFIER NOT NULL,
    [EtlPackageName]     NVARCHAR (255)   NULL,
    [StartDateTime]      DATETIME         NULL,
    [StartUtcDateTime]   DATETIME         NULL,
    [EndDateTime]        DATETIME         NULL,
    [EndUtcDateTime]     DATETIME         NULL,
    [Status]             INT              NOT NULL,
    [ParentEtlSessionId] UNIQUEIDENTIFIER NULL,
    [UserName]           NVARCHAR (50)    NULL,
    [InsertDateTime]     DATETIME         DEFAULT (getdate()) NOT NULL,
    [InsertUtcDateTime]  DATETIME         DEFAULT (getutcdate()) NOT NULL,
    CONSTRAINT [PK_EtlSessions_EtlPackageIdEtlSessionId] PRIMARY KEY NONCLUSTERED ([EtlPackageId] ASC, [EtlSessionId] ASC),
    CONSTRAINT [FK_EtlSessions_ToTable] FOREIGN KEY ([EtlPackageId]) REFERENCES [etl].[EtlPackages] ([Id]),
    CONSTRAINT [AK_EtlSessions_EtlSessionId] UNIQUE NONCLUSTERED ([EtlSessionId] ASC)
);

