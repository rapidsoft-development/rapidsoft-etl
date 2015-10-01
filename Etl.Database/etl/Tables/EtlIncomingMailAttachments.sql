CREATE TABLE [etl].[EtlIncomingMailAttachments] (
    [SeqId]        INT              IDENTITY (1, 1) NOT NULL,
    [EtlPackageId] UNIQUEIDENTIFIER NOT NULL,
    [EtlSessionId] UNIQUEIDENTIFIER NOT NULL,
    [FileName]     NVARCHAR (255)   NOT NULL,
    CONSTRAINT [PK_EtlIncomingMailAttachments] PRIMARY KEY CLUSTERED ([SeqId] ASC)
);

