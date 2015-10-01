CREATE TABLE [etl].[EtlMessages] (
    [SequentialId]   BIGINT           IDENTITY (1, 1) NOT NULL,
    [EtlPackageId]   UNIQUEIDENTIFIER NOT NULL,
    [EtlSessionId]   UNIQUEIDENTIFIER NOT NULL,
    [EtlStepName]    NVARCHAR (255)   NULL,
    [MessageType]    INT              NOT NULL,
    [Text]           NVARCHAR (MAX)   NULL,
    [Flags]          BIGINT           NULL,
    [StackTrace]     NVARCHAR (MAX)   NULL,
    [LogDateTime]    DATETIME         NOT NULL,
    [LogUtcDateTime] DATETIME         NOT NULL,
    CONSTRAINT [PK_EtlMessages_SequentialId] PRIMARY KEY CLUSTERED ([SequentialId] ASC),
    CONSTRAINT [FK_EtlMessages_EtlPackageId] FOREIGN KEY ([EtlPackageId]) REFERENCES [etl].[EtlPackages] ([Id]),
    CONSTRAINT [FK_EtlMessages_EtlSessions] FOREIGN KEY ([EtlSessionId]) REFERENCES [etl].[EtlSessions] ([EtlSessionId])
);


GO
CREATE NONCLUSTERED INDEX [IX_EtlMessages_by_package_session]
    ON [etl].[EtlMessages]([EtlPackageId] ASC, [EtlSessionId] ASC);

