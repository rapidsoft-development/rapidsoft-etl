CREATE TABLE [etl].[EtlFileCounter] (
    [EtlPackageId] UNIQUEIDENTIFIER NOT NULL,
    [EtlSessionId] UNIQUEIDENTIFIER NOT NULL,
    [FileTemplate] NVARCHAR (255)   NOT NULL,
    [FileCount]    INT              NOT NULL,
    [CounterDate]  DATETIME              NOT NULL,
    CONSTRAINT [PK_EtlFileCounter] PRIMARY KEY CLUSTERED ([FileTemplate] ASC, [CounterDate] ASC)
);

