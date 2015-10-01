CREATE TABLE [etl].[EtlCounters] (
    [EtlPackageId]   UNIQUEIDENTIFIER NOT NULL,
    [EtlSessionId]   UNIQUEIDENTIFIER NOT NULL,
    [EntityName]     NVARCHAR (255)   NOT NULL,
    [CounterName]    NVARCHAR (255)   NOT NULL,
    [CounterValue]   BIGINT           NOT NULL,
    [LogDateTime]    DATETIME         NOT NULL,
    [LogUtcDateTime] DATETIME         NOT NULL,
    CONSTRAINT [FK_EtlCounters_EtlPackageId] FOREIGN KEY ([EtlPackageId]) REFERENCES [etl].[EtlPackages] ([Id]),
    CONSTRAINT [FK_EtlCounters_EtlSessions] FOREIGN KEY ([EtlSessionId]) REFERENCES [etl].[EtlSessions] ([EtlSessionId])
);

