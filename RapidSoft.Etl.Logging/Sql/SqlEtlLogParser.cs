using System;
using System.Collections.Generic;
using System.Data.SqlClient;

using System.Collections;
using System.Text;
using System.Data;

namespace RapidSoft.Etl.Logging.Sql
{
    public sealed class SqlEtlLogParser : IEtlLogParser
    {
        #region Constructors

        public SqlEtlLogParser(string connectionString, string schemaName)
		{
            if (connectionString == null)
            {
                throw new ArgumentNullException("connectionString");
            }

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException("Parameter \"connectionString\" cannot be empty", "connectionString");
            }

			_connectionString = connectionString;
            _schemaName = schemaName;
            if (!string.IsNullOrEmpty(_schemaName))
            {
                _schemaToken = string.Format("[{0}].", _schemaName.TrimStart('[').TrimEnd(']').Replace("]", @"\]"));
            }
            else
            {
                _schemaToken = "";
            }
        }

        #endregion

        #region Fields

        private readonly string _connectionString;
        private readonly string _schemaName;
        private readonly string _schemaToken;

        #endregion

        #region Methods

        public EtlSession GetEtlSession(string packageId, string sessionId)
        {
            if (!(IsGuid(packageId) && IsGuid(sessionId)))
            {
                return null;
            }

            var session = SelectEtlSession(packageId, sessionId);
            return session;
        }

        public EtlSession[] GetEtlSessions(EtlSessionQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            if (!IsGuids(query.EtlPackageIds))
            {
                return new EtlSession[0];
            }

            var sessions = SelectEtlSessions(query);
            return sessions.ToArray();
        }

        public EtlSession[] GetLatestEtlSessions(string[] etlPackageIds)
        {
            if (etlPackageIds == null)
            {
                throw new ArgumentNullException("etlPackageIds");
            }

            if (etlPackageIds.Length == 0 || !IsGuids(etlPackageIds))
            {
                return new EtlSession[0];
            }

            var sessions = SelectLatestEtlSessions(etlPackageIds);
            return sessions.ToArray();
        }

        public EtlVariable[] GetEtlVariables(string etlPackageId, string etlSessionId)
        {
            var variables = SelectEtlVariables(etlPackageId, etlSessionId);
            return variables.ToArray();
        }

        public EtlCounter[] GetEtlCounters(string etlPackageId, string etlSessionId)
        {
            var counters = SelectEtlCounters(etlPackageId, etlSessionId);
            return counters.ToArray();
        }

        public EtlMessage[] GetEtlMessages(string etlPackageId, string etlSessionId)
        {
            var messages = SelectEtlMessages(etlPackageId, etlSessionId);
            return messages.ToArray();
        }

        private EtlSession SelectEtlSession(string packageId, string sessionId)
        {
            var cmd = CreateSelectEtlSessionCommand(packageId, sessionId);
            using (var conn = CreateConnection())
            {
                conn.Open();
                cmd.Connection = conn;

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var session = ReadEtlSession(reader);
                        return session;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        private List<EtlSession> SelectEtlSessions(EtlSessionQuery query)
        {
            var cmd = CreateSelectEtlSessionsCommand(query);
            var sessions = new List<EtlSession>();

            using (var conn = CreateConnection())
            {
                conn.Open();
                cmd.Connection = conn;

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var session = ReadEtlSession(reader);
                        sessions.Add(session);
                    }
                }
            }

            return sessions;
        }

        private List<EtlSession> SelectLatestEtlSessions(string[] etlPackageIds)
        {
            var cmd = CreateSelectLatestEtlSessionsCommand(etlPackageIds);
            var sessions = new List<EtlSession>();

            using (var conn = CreateConnection())
            {
                conn.Open();
                cmd.Connection = conn;

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var session = ReadEtlSession(reader);
                        sessions.Add(session);
                    }
                }
            }

            return sessions;
        }

        private SqlCommand CreateSelectEtlSessionCommand(string packageId, string sessionId)
        {
            var sql = @"
select  
    s.[EtlPackageId],
    s.[EtlPackageName],
    s.[EtlSessionId],
    s.[StartDateTime],
    s.[StartUtcDateTime],
    s.[EndDateTime],
    s.[EndUtcDateTime],
    s.[Status],
    s.[UserName]
from 
	{0}[EtlSessions] s with (nolock)
where 
    s.[EtlPackageId] = @EtlPackageId and
    s.[EtlSessionId] = @EtlSessionId
			";

            var cmd = new SqlCommand();
            cmd.CommandText = string.Format(sql, _schemaToken);

            cmd.Parameters.AddWithValue("@EtlPackageId", packageId);
            cmd.Parameters.AddWithValue("@EtlSessionId", sessionId);

            return cmd;
        }

        private SqlCommand CreateSelectEtlSessionsCommand(EtlSessionQuery query)
        {
			var sql = @"
select {1}
    s.[EtlPackageId],
    s.[EtlPackageName],
    s.[EtlSessionId],
    s.[StartDateTime],
    s.[StartUtcDateTime],
    s.[EndDateTime],
    s.[EndUtcDateTime],
    s.[Status],
    s.[UserName]
from 
	{0}[EtlSessions] s with (nolock)
where 
    {2} {3} {4} s.[StartDateTime] between @DateFrom and @DateTo
order by
    s.[StartDateTime] desc
			";

            var cmd = new SqlCommand();

            var topToken = "";
            if (query.MaxSessionCount.HasValue)
            {
                topToken = string.Concat("top ", query.MaxSessionCount);
            }

            var packageIdsToken = "";
            if (query.EtlPackageIds.Count > 0)
            {
                for (var i = 0; i < query.EtlPackageIds.Count; i++)
                {
                    var paramName = string.Concat("@pid", i);

                    if (i == 0)
                    {
                        packageIdsToken = string.Concat("s.[EtlPackageId] in (", paramName);
                    }
                    else
                    {
                        packageIdsToken = string.Concat(packageIdsToken, ", ", paramName);
                    }

                    cmd.Parameters.AddWithValue(paramName, query.EtlPackageIds[i]);
                }

                packageIdsToken = string.Concat(packageIdsToken, ") and ");
            }

            var variablesToken = "";
            if (query.Variables.Count > 0)
            {
                for (var i = 0; i < query.Variables.Count; i++)
                {
                    var nameParam = string.Concat("@vn", i);
                    var valueParam = string.Concat("@vv", i);

                    if (i > 0)
                    {
                        variablesToken = string.Concat(variablesToken, " or ");
                    }

                    variablesToken = string.Concat
                    (
                        variablesToken, 
                        "(sv.[Name] = ", 
                        nameParam, 
                        " and sv.[Value] = ", 
                        valueParam,
                        ")"
                    );

                    cmd.Parameters.AddWithValue(nameParam, query.Variables[i].Name);
                    cmd.Parameters.AddWithValue(valueParam, query.Variables[i].Value);
                }

                variablesToken = string.Concat
                (
                    String.Format("exists (select 1 from {0}[EtlVariables] sv with (nolock) where (sv.[EtlPackageId] = s.[EtlPackageId]) and (sv.[EtlSessionId] = s.[EtlSessionId]) and ", _schemaToken),
                    variablesToken, 
                    ") and "
                );
            }
            
            var etlStatusesToken = "";
            if (query.EtlStatuses.Count > 0)
            {
                for (var i = 0; i < query.EtlStatuses.Count; i++)
                {
                    var paramName = string.Concat("@etlstatus", i);

                    if (i == 0)
                    {
                        etlStatusesToken = string.Concat("s.[Status] in (", paramName);
                    }
                    else
                    {
                        etlStatusesToken = string.Concat(etlStatusesToken, ", ", paramName);
                    }

                    cmd.Parameters.AddWithValue(paramName, query.EtlStatuses[i]);
                }

                etlStatusesToken = string.Concat(etlStatusesToken, ") and ");
            }

            cmd.CommandText = string.Format
            (
                sql, 
                _schemaToken,
                topToken,
                packageIdsToken,
                variablesToken,
                etlStatusesToken
            );

            cmd.Parameters.AddWithValue("@DateFrom", query.FromDateTime);
            cmd.Parameters.AddWithValue("@DateTo", query.ToDateTime);

            return cmd;
        }

        private SqlCommand CreateSelectLatestEtlSessionsCommand(string[] etlPackageIds)
        {
            var sql = @"
select
    s.[EtlPackageId],
    p.[Name] as [EtlPackageName],
    p.[Text],
    p.[RunIntervalSeconds],
    s.[EtlSessionId],
    s.[StartDateTime],
    s.[StartUtcDateTime],
    s.[EndDateTime],
    s.[EndUtcDateTime],
    s.[Status],
    s.[UserName]
from 
	{0}[EtlSessions] s with (nolock)
	inner join 
	(
		select	[EtlPackageId],
				MAX([StartDateTime]) as [StartDateTime]
		from	{0}[EtlSessions] s with (nolock)
		where {1}
		group by [EtlPackageId]
	) as d on s.EtlPackageId = d.EtlPackageId and s.StartDateTime = d.StartDateTime
    inner join
    {0}[EtlPackages] p on p.Id = s.EtlPackageId
order by
    s.[StartDateTime] desc
			";

            var cmd = new SqlCommand();

            var packageIdsToken = "";
            if (etlPackageIds != null && etlPackageIds.Length > 0)
            {
                for (var i = 0; i < etlPackageIds.Length; i++)
                {
                    var paramName = string.Concat("@pid", i);

                    if (i == 0)
                    {
                        packageIdsToken = string.Concat("s.[EtlPackageId] in (", paramName);
                    }
                    else
                    {
                        packageIdsToken = string.Concat(packageIdsToken, ", ", paramName);
                    }

                    cmd.Parameters.AddWithValue(paramName, etlPackageIds[i]);
                }

                packageIdsToken = string.Concat(packageIdsToken, ")");
            }

            cmd.CommandText = string.Format
            (
                sql,
                _schemaToken,
                packageIdsToken
            );

            return cmd;
        }

        private EtlSession ReadEtlSession(SqlDataReader reader)
        {
            var session = new EtlSession
            {
                EtlPackageId = EtlValueConverter.ToString(reader["EtlPackageId"]),
                EtlPackageName = EtlValueConverter.ToString(reader["EtlPackageName"]),
                EtlSessionId = EtlValueConverter.ToString(reader["EtlSessionId"]),
                StartDateTime = EtlValueConverter.ParseDateTime(reader["StartDateTime"]),
                StartUtcDateTime = EtlValueConverter.ParseDateTime(reader["StartUtcDateTime"]),
                EndDateTime = EtlValueConverter.ParseDateTimeOrNull(reader["EndDateTime"]),
                EndUtcDateTime = EtlValueConverter.ParseDateTimeOrNull(reader["EndUtcDateTime"]),
                Status = (EtlStatus)EtlValueConverter.ParseInt32(reader["Status"]),
                UserName = EtlValueConverter.ToString(reader["UserName"]),
            };

            return session;
        }

        private List<EtlVariable> SelectEtlVariables(string etlPackageId, string etlSessionId)
        {
            const string sql =
@"select 
    sv.[Name],
    sv.[Modifier],
    sv.[Value],
    sv.[LogDateTime], 
    sv.[LogUtcDateTime], 
    sv.[IsSecure]
from
    {0}[EtlVariables] sv with (nolock)
where 
    sv.[EtlPackageId] = @EtlPackageId and
    sv.[EtlSessionId] = @EtlSessionId
";
            var variables = new List<EtlVariable>();
            using (var conn = CreateConnection())
            {
                conn.Open();

                var cmd = conn.CreateCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = string.Format(sql, _schemaToken);
                cmd.Parameters.AddWithValue("@EtlPackageId", etlPackageId);
                cmd.Parameters.AddWithValue("@EtlSessionId", etlSessionId);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var es = new EtlVariable
                        {
                            EtlPackageId = etlPackageId,
                            EtlSessionId = etlSessionId,
                            Name = EtlValueConverter.ToString(reader["Name"]),
                            Modifier = (EtlVariableModifier)EtlValueConverter.ParseInt32(reader["Modifier"]),
                            Value = EtlValueConverter.ToString(reader["Value"]),
                            IsSecure = EtlValueConverter.ParseBoolean(reader["IsSecure"]),
                            DateTime = EtlValueConverter.ParseDateTime(reader["LogDateTime"]),
                            UtcDateTime = EtlValueConverter.ParseDateTime(reader["LogUtcDateTime"]),
                        };

                        variables.Add(es);
                    }
                }
            }

            return variables;
        }

        private List<EtlCounter> SelectEtlCounters(string etlPackageId, string etlSessionId)
        {
            const string sql =
@"select 
    sv.[EntityName],
    sv.[CounterName],
    sv.[CounterValue],
    sv.[LogDateTime], 
    sv.[LogUtcDateTime]
from
    {0}[EtlCounters] sv with (nolock)
where 
    sv.[EtlPackageId] = @EtlPackageId and
    sv.[EtlSessionId] = @EtlSessionId
";
            var counters = new List<EtlCounter>();
            using (var conn = CreateConnection())
            {
                conn.Open();

                var cmd = conn.CreateCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = string.Format(sql, _schemaToken);
                cmd.Parameters.AddWithValue("@EtlPackageId", etlPackageId);
                cmd.Parameters.AddWithValue("@EtlSessionId", etlSessionId);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var es = new EtlCounter
                        {
                            EtlPackageId = etlPackageId,
                            EtlSessionId = etlSessionId,
                            EntityName = EtlValueConverter.ToString(reader["EntityName"]),
                            CounterName = EtlValueConverter.ToString(reader["CounterName"]),
                            CounterValue = EtlValueConverter.ParseInt64(reader["CounterValue"]),
                            DateTime = EtlValueConverter.ParseDateTime(reader["LogDateTime"]),
                            UtcDateTime = EtlValueConverter.ParseDateTime(reader["LogUtcDateTime"]),
                        };

                        counters.Add(es);
                    }
                }
            }

            return counters;
        }

        private List<EtlMessage> SelectEtlMessages(string etlPackageId, string etlSessionId)
        {
            const string sql =
 @"select 
    m.[SequentialId],
	m.[EtlPackageId],
	m.[EtlSessionId],
    m.[EtlStepName],
	m.[LogDateTime],
	m.[LogUtcDateTime],
	m.[MessageType],
	m.[Text],
    m.[Flags],
	m.[StackTrace]
from
    {0}[EtlMessages] m with (nolock)
where 
    m.[EtlPackageId] = @EtlPackageId and
    m.[EtlSessionId] = @EtlSessionId
order by 
    m.[SequentialId]
";
            var messages = new List<EtlMessage>();
            using (var conn = CreateConnection())
            {
                conn.Open();

                var cmd = conn.CreateCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = string.Format(sql, _schemaToken);
                cmd.Parameters.AddWithValue("@EtlPackageId", etlPackageId);
                cmd.Parameters.AddWithValue("@EtlSessionId", etlSessionId);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var msg = new EtlMessage
                        {
                            SequentialId = EtlValueConverter.ParseInt64(reader["SequentialId"]),
                            EtlPackageId = EtlValueConverter.ToString(reader["EtlPackageId"]),
                            EtlSessionId = EtlValueConverter.ToString(reader["EtlSessionId"]),
                            EtlStepName = EtlValueConverter.ToString(reader["EtlStepName"]),
                            LogDateTime = EtlValueConverter.ParseDateTime(reader["LogDateTime"]),
                            LogUtcDateTime = EtlValueConverter.ParseDateTime(reader["LogUtcDateTime"]),
                            MessageType = ConvertToEtlMessageType(EtlValueConverter.ParseInt32(reader["MessageType"])),
                            Text = EtlValueConverter.ToString(reader["Text"]),
                            Flags = EtlValueConverter.ParseInt64OrNull(reader["Flags"]),
                            StackTrace = EtlValueConverter.ToString(reader["StackTrace"]),
                        };

                        messages.Add(msg);
                    }
                }
            }

            return messages;
        }

        private static EtlMessageType ConvertToEtlMessageType(int value)
        {
            //we can use (EtlMessageType)EtlValueConverter.ParseInt32(reader["MessageType"])
            //but codes 6 and 7 must be converted to EtlMessageType.Error due compatibility purposes
            switch (value)
            {
                case 1: return EtlMessageType.SessionStart;
                case 2: return EtlMessageType.SessionEnd;
                case 3: return EtlMessageType.StepStart;
                case 4: return EtlMessageType.StepEnd;
                case 5: case 6: case 7: return EtlMessageType.Error;
                case 8: return EtlMessageType.Information;
                case 9: return EtlMessageType.Debug;
                default: return EtlMessageType.Unknown;
            }
        }

        private SqlConnection CreateConnection()
        {
            var conn = new SqlConnection(_connectionString);
            return conn;
        }
        
        private static bool IsGuid(string str)
        {
            try
            {
                var g = new Guid(str);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static bool IsGuids(IEnumerable<string> strings)
        {
            try
            {
                foreach (var str in strings)
                {
                    var g = new Guid(str);
                }
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        #endregion
    }
}