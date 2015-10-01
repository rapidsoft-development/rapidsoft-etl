using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Text;

using RapidSoft.Etl.Runtime.DataSources.Csv;
using RapidSoft.Etl.Runtime.DataSources.DB;
using RapidSoft.Etl.Logging;

namespace RapidSoft.Etl.Runtime.Steps
{
    //todo: add statistics
    [Serializable]
    public sealed class EtlExportCsvFileStep : EtlStep
    {
        #region Constructors

        public EtlExportCsvFileStep()
        {
        }

        #endregion

        #region Constants

        public static readonly char DefaultQuote = '"';
        public static readonly char DefaultEscape = '"';
        public static readonly string DefaultLineDelimiter = "\r\n";

        #endregion

        #region Properties

        [Category("2. Source")]
        public EtlQuerySourceInfo Source
        {
            get;
            set;
        }

        [Category("3. Destination")]
        public EtlCsvFileInfo Destination
        {
            get;
            set;
        }

        [Category("4. Mappings")]
        [Editor(EtlComponentModelInfo.EtlCollectionModelEditorType, EtlComponentModelInfo.EtlCollectionModelEditorBase)]
        [XmlArrayItem("Mapping")]
        public List<EtlFieldMapping> Mappings
        {
            [DebuggerStepThrough]
            get
            {
                return _mappings;
            }
        }
        private List<EtlFieldMapping> _mappings = new List<EtlFieldMapping>();
        
        #endregion

        #region Methods

        public override EtlStepResult Invoke(EtlContext context, IEtlLogger logger)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            if (this.Source == null)
            {
                throw new InvalidOperationException(string.Format(Properties.Resources.PropertyCannotBeNull, "Source"));
            }

            if (this.Destination == null)
            {
                throw new InvalidOperationException(string.Format(Properties.Resources.PropertyCannotBeNull, "Destination"));
            }

            if (string.IsNullOrEmpty(this.Source.ConnectionString))
            {
                throw new InvalidOperationException(string.Format(Properties.Resources.PropertyCannotBeNull, "Destination.ConnectionString"));
            }

            if (string.IsNullOrEmpty(this.Source.ProviderName))
            {
                throw new InvalidOperationException(string.Format(Properties.Resources.PropertyCannotBeNull, "Source.ProviderName"));
            }

            if (string.IsNullOrEmpty(this.Source.Text))
            {
                throw new InvalidOperationException(string.Format(Properties.Resources.PropertyCannotBeNull, "Source.Text"));
            }

            if (string.IsNullOrEmpty(this.Destination.FieldDelimiter))
            {
                throw new InvalidOperationException(string.Format(Properties.Resources.PropertyCannotBeNull, "Destination.FieldDelimiter"));
            }

            if (string.IsNullOrEmpty(this.Destination.FilePath))
            {
                throw new InvalidOperationException(string.Format(Properties.Resources.PropertyCannotBeNull, "Destination.FilePath"));
            }

            if (this.Destination.FieldDelimiter.Length != 1)
            {
                throw new InvalidOperationException(string.Format(Properties.Resources.PropertyTooLong, "Destination.FieldDelimiter", 1));
            }

            if (this.Destination.Quote != null && this.Destination.Quote.Length != 1)
            {
                throw new InvalidOperationException(string.Format(Properties.Resources.PropertyTooLong, "Destination.Quote", 1));
            }

            if (this.Destination.Escape != null && this.Destination.Escape.Length != 1)
            {
                throw new InvalidOperationException(string.Format(Properties.Resources.PropertyTooLong, "Destination.Escape", 1));
            }

			if (this.Destination != null)
			{
				var destinationFolderPath = Path.GetDirectoryName(this.Destination.FilePath);

				if (!string.IsNullOrEmpty(destinationFolderPath))
				{
					if (!Directory.Exists(destinationFolderPath))
					{
						Directory.CreateDirectory(destinationFolderPath);
					}
				}
			}

            //if (this.Mappings.Count == 0)
            //{
            //    throw new InvalidOperationException("Mappings not specified");
            //}

            var result = new EtlStepResult(EtlStatus.Succeeded, null);

            var sourceRowCount = 0L;
            var writtenRowCount = 0L;

            var encoding = Encoding.GetEncoding(this.Destination.CodePage);
            var csvSyntax = new CsvSyntaxInfo
            {
                HasHeaders = this.Destination.HasHeaders,
                FieldDelimiter = this.Destination.FieldDelimiter[0],
                Quote = this.Destination.Quote != null ? this.Destination.Quote[0] : DefaultQuote,
                Escape = this.Destination.Escape != null ? this.Destination.Escape[0] : DefaultEscape,
                LineDelimiter1 = this.Destination.LineDelimiter != null ? this.Destination.LineDelimiter[0] : DefaultLineDelimiter[0],
                LineDelimiter2 = this.Destination.LineDelimiter != null ? this.Destination.LineDelimiter[1] : DefaultLineDelimiter[1],
            };

            using (var dbAccessor = new DBAccessor(this.Source.ConnectionString, this.Source.ProviderName))
            {
                using (var dbReader = dbAccessor.ExecuteQuery(this.Source.Text, EtlQueryParameter.ToDictionary(this.Source.Parameters), this.TimeoutMilliseconds))
                {
                    using (var mapReader = new EtlMappedDataReader(dbReader, this.Mappings))
                    {
                        using (var fileWriter = new StreamWriter(this.Destination.FilePath, false, encoding))
                        {
                            var csvWriter = new CsvWriter(fileWriter, csvSyntax);

                            writtenRowCount = csvWriter.Write(mapReader);
                            sourceRowCount = writtenRowCount;
                        }
                    }
                }
            }

            //logger.LogEtlMessage(new EtlMessage
            //{
            //    EtlPackageId = context.EtlPackageId,
            //    EtlSessionId = context.EtlSessionId,
            //    EtlStepId = this.Id,
            //    LogDateTime = endDateTime,
            //    LogUtcDateTime = endDateTime.ToUniversalTime(),
            //    MessageType = EtlMessageType.Statistics,
            //    Text = "Found",
            //    Flags = sourceRowCount,
            //});

            //logger.LogEtlMessage(new EtlMessage
            //{
            //    EtlPackageId = context.EtlPackageId,
            //    EtlSessionId = context.EtlSessionId,
            //    EtlStepId = this.Id,
            //    LogDateTime = endDateTime,
            //    LogUtcDateTime = endDateTime.ToUniversalTime(),
            //    MessageType = EtlMessageType.Statistics,
            //    Text = "Exported",
            //    Flags = writtenRowCount,
            //});

            return result;
        }

        private static DbConnection CreateConnection(string connectionString, string providerName)
        {
            var factory = DbProviderFactories.GetFactory(providerName);
            var connection = factory.CreateConnection();
            connection.ConnectionString = connectionString;

            return connection;
        }

        private static IDataReader CreateTableReader(DbConnection connection, string queryText, int timeout)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = queryText;
            cmd.CommandType = CommandType.Text;
            cmd.CommandTimeout = timeout;

            var reader = cmd.ExecuteReader();
            return reader;
        }

        #endregion
    }
}
