using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Net.Mail;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.Xsl;

using RapidSoft.Etl.Logging;
using RapidSoft.Etl.Packages;

namespace RapidSoft.Etl.Console
{
    [Obsolete]
    public sealed class EtlRunDBCommand : IEtlConsoleCommand
    {
        #region Constants

        private const string COMMAND_NAME = "rundb";
        private const string PARAM_NAME_CONNECTION_STRING = "connectionstring";
        private const string PARAM_NAME_PROVIDER_NAME = "providername";
        private const string PARAM_NAME_PACKAGE_QUERY = "packagequery";
        private const string PARAM_NAME_SCHEMA = "schema";
        private const string PARAM_NAME_LOGGER_TYPE = "loggertype";

        #endregion

        #region Properties

        public string CommandName
        {
            get
            {
                return COMMAND_NAME;
            }
        }

        public string Description
        {
            get
            {
                //todo: localize message
                return "invokes ETL package";
            }
        }

        public EtlConsoleCommandOptionInfo[] OptionsInfo
        {
            get
            {
                return new[]
                {
                    new EtlConsoleCommandOptionInfo(PARAM_NAME_CONNECTION_STRING, "connection string to datasource"),
                    new EtlConsoleCommandOptionInfo(PARAM_NAME_PROVIDER_NAME, "invariant name of data provider"),
                    new EtlConsoleCommandOptionInfo(PARAM_NAME_PACKAGE_QUERY, "query to execute"),
                    new EtlConsoleCommandOptionInfo(PARAM_NAME_SCHEMA, "data source schema", true),
                    new EtlConsoleCommandOptionInfo(PARAM_NAME_LOGGER_TYPE, "full name of logger type", true),
                };
            }
        }

        #endregion

        #region Methods

        //todo: localize log message
        public void ExecuteCommand(EtlConsoleArguments options)
        {
            System.Console.WriteLine("Verifying input parameters...");

            var errorMsg = VerifyArguments(options);

            if (!String.IsNullOrEmpty(errorMsg))
            {
                throw new Exception(String.Format("Input parameters incorrect: {0}", errorMsg));
            }

            var providerFactory = DbProviderFactories.GetFactory(options.CommandOptions[PARAM_NAME_PROVIDER_NAME]);

            var packageText = String.Empty;

            System.Console.WriteLine("Receiving package text");

            using (var connection = providerFactory.CreateConnection())
            {
                connection.ConnectionString = options.CommandOptions[PARAM_NAME_CONNECTION_STRING];

                var command = connection.CreateCommand();

                command.CommandText = options.CommandOptions[PARAM_NAME_PACKAGE_QUERY];
                command.CommandTimeout = 60; // 1 minute
                command.CommandType = System.Data.CommandType.Text;

                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        if (reader.FieldCount == 1)
                        {
                            packageText = reader[0].ToString();
                        }
                        else
                        {
                            throw new Exception("Package text query result contains more than one element");
                        }
                    }
                    else
                    {
                        throw new Exception("Package text query result contains no rows");
                    }

                    if (reader.Read())
                    {
                        throw new Exception("Package text query result contains multiple rows");
                    }
                }
            }

            if (String.IsNullOrEmpty(packageText))
            {
                throw new Exception("Package text cannot be null or empty");
            }

            System.Console.WriteLine("Deserializing package");

            var etlPackageSerializer = new EtlPackageXmlSerializer();
            var package = etlPackageSerializer.Deserialize(packageText);

            var etlLoggerInfo = new EtlLoggerInfo()
            {
                ConnectionString = options.CommandOptions[PARAM_NAME_CONNECTION_STRING],
                LoggerType = options.CommandOptions[PARAM_NAME_LOGGER_TYPE],
                SchemaName = options.CommandOptions.ContainsKey(PARAM_NAME_SCHEMA) ? options.CommandOptions[PARAM_NAME_SCHEMA] : String.Empty,
            };

            var packageLogger = EtlLoggers.GetLogger(etlLoggerInfo);

            System.Console.WriteLine("Executing package in a sub-session");
            var result = package.Invoke(packageLogger);
            System.Console.WriteLine("Package has been executed");
        }

        private string VerifyArguments(EtlConsoleArguments options)
        {
            StringBuilder sb = new StringBuilder();

            if (!options.CommandOptions.ContainsKey(PARAM_NAME_CONNECTION_STRING) || String.IsNullOrEmpty(options.CommandOptions[PARAM_NAME_CONNECTION_STRING]))
                sb.AppendFormat("{0};", PARAM_NAME_CONNECTION_STRING);

            if (!options.CommandOptions.ContainsKey(PARAM_NAME_PROVIDER_NAME) || String.IsNullOrEmpty(options.CommandOptions[PARAM_NAME_PROVIDER_NAME]))
                sb.AppendFormat("{0};", PARAM_NAME_PROVIDER_NAME);

            if (!options.CommandOptions.ContainsKey(PARAM_NAME_PACKAGE_QUERY) || String.IsNullOrEmpty(options.CommandOptions[PARAM_NAME_PACKAGE_QUERY]))
                sb.AppendFormat("{0};", PARAM_NAME_PACKAGE_QUERY);

            if (!options.CommandOptions.ContainsKey(PARAM_NAME_LOGGER_TYPE) || String.IsNullOrEmpty(options.CommandOptions[PARAM_NAME_LOGGER_TYPE]))
                sb.AppendFormat("{0};", PARAM_NAME_LOGGER_TYPE);

            var result = sb.ToString();

            return sb.ToString();
        }

        #endregion
    }
}