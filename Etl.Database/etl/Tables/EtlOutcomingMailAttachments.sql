CREATE TABLE [etl].[EtlOutcomingMailAttachments] (
    [Id]                 BIGINT           IDENTITY (1, 1) NOT NULL,
    [EtlOutcomingMailId] BIGINT           NOT NULL,
    [EtlPackageId]       UNIQUEIDENTIFIER NOT NULL,
    [EtlSessionId]       UNIQUEIDENTIFIER NOT NULL,
    [FileName]           NVARCHAR (MAX)   NOT NULL,
    CONSTRAINT [PK_EtlOutcomingMailAttachments] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_EtlOutcomingMailAttachments_EtlOutcomingMails] FOREIGN KEY ([Id]) REFERENCES [etl].[EtlOutcomingMailAttachments] ([Id])
);

