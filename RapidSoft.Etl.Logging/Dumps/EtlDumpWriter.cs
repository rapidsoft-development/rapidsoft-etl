using System;
using System.Collections.Generic;
using System.Xml;

namespace RapidSoft.Etl.Logging.Dumps
{
    public sealed class EtlDumpWriter
    {
        #region Constructors

        public EtlDumpWriter(EtlDumpSettings settings)
        {
            _dump = new EtlDump();
        }

        #endregion

        #region Fields

        private readonly EtlDump _dump;

        #endregion

        #region Methods

        public EtlDump GetDump()
        {
            return _dump;
        }

        public void Write(string packageId, string sessionId, IEtlLogParser logParser)
        {
            if (logParser == null)
            {
                throw new ArgumentNullException("logParser");
            }

            InitDump();

            var session = logParser.GetEtlSession(packageId, sessionId);
            if (session != null)
            {
                var sessionSummary = GetEtlSessionSummary(session, logParser);
                _dump.Sessions.Add(sessionSummary);
            }
        }

        public void Write(EtlSessionQuery query, IEtlLogParser logParser)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            if (logParser == null)
            {
                throw new ArgumentNullException("logParser");
            }

            InitDump();

            var sessions = logParser.GetEtlSessions(query);
            foreach (var session in sessions)
            {
                var sessionSummary = GetEtlSessionSummary(session, logParser);
                _dump.Sessions.Add(sessionSummary);
            }
        }

        private void InitDump()
        {
            if (_dump.DumpDateTime == default(DateTime))
            {
                _dump.DumpDateTime = DateTime.Now;
                _dump.DumpUtcDateTime = _dump.DumpDateTime.ToUniversalTime();
            }
        }

        private static EtlSessionSummary GetEtlSessionSummary(EtlSession session, IEtlLogParser logParser)
        {
           var sessionSummary = new EtlSessionSummary
            {
                EtlPackageId = session.EtlPackageId,
                EtlPackageName = session.EtlPackageName,
                EtlSessionId = session.EtlSessionId,
                StartDateTime = session.StartDateTime,
                StartUtcDateTime = session.StartUtcDateTime,
                EndDateTime = session.EndDateTime,
                EndUtcDateTime = session.EndUtcDateTime,
                Status = session.Status,
                UserName = session.UserName,
            };

            var variables = logParser.GetEtlVariables(session.EtlPackageId, session.EtlSessionId);
            sessionSummary.Variables.AddRange(variables);

            var counters = logParser.GetEtlCounters(session.EtlPackageId, session.EtlSessionId);
            sessionSummary.Counters.AddRange(counters);

            var messages = logParser.GetEtlMessages(session.EtlPackageId, session.EtlSessionId);
            sessionSummary.Messages.AddRange(messages);

            for (var i = messages.Length - 1; i >= 0; i--)
            {
                if (messages[i].MessageType == EtlMessageType.Error)
                {
                    sessionSummary.LastErrorMessage = messages[i];
                    break;
                }
            }

            return sessionSummary;
        }

        #endregion
    }
}