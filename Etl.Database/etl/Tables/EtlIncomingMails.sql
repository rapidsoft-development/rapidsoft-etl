CREATE TABLE [etl].[EtlIncomingMails] (
    [EtlPackageId] UNIQUEIDENTIFIER NOT NULL,
    [EtlSessionId] UNIQUEIDENTIFIER NOT NULL,
    [MessageUid]   BIGINT           NOT NULL,
    [MessageRaw]   NVARCHAR (MAX)   NULL,
    [IsDeleted]    BIT              DEFAULT ((0)) NOT NULL,
    [InsertedDate] DATETIME         CONSTRAINT [DF_EtlIncomingMails_InsertedDate] DEFAULT (getdate()) NOT NULL,
    CONSTRAINT [PK_EtlIncomingMails] PRIMARY KEY CLUSTERED ([EtlSessionId] ASC, [MessageUid] ASC)
);

