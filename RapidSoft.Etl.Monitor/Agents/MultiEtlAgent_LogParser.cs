using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using RapidSoft.Etl.Logging;
using RapidSoft.Etl.Runtime;
using RapidSoft.Etl.Runtime.Agents;

namespace RapidSoft.Etl.Monitor.Agents
{
    partial class MultiEtlAgent : IEtlAgent
    {
        private class _LogParser : IEtlLogParser
        {
            #region Constructors

            public _LogParser(IEtlAgent[] etlAgents)
            {
                _etlAgents = etlAgents ?? new IEtlAgent[0];
            }

            #endregion

            #region Fields

            private readonly IEtlAgent[] _etlAgents;

            #endregion

            #region Public methods

            public EtlSession[] GetEtlSessions(EtlSessionQuery query)
            {
                var result = new List<EtlSession>();

                foreach (var agent in _etlAgents)
                {
                    var logParser = agent.GetEtlLogParser();
                    result.AddRange(logParser.GetEtlSessions(query));
                }

                return result.ToArray();
            }

            public EtlSession[] GetLatestEtlSessions(string[] etlPackageIds)
            {
                var result = new List<EtlSession>();

                foreach (var agent in _etlAgents)
                {
                    var logParser = agent.GetEtlLogParser();
                    result.AddRange(logParser.GetLatestEtlSessions(etlPackageIds));
                }

                return result.ToArray();
            }

            public EtlSession GetEtlSession(string etlPackageId, string etlSessionId)
            {
                EtlSession result = null;

                foreach (var agent in _etlAgents)
                {
                    var logParser = agent.GetEtlLogParser();
                    result = logParser.GetEtlSession(etlPackageId, etlSessionId);
                    if (result != null)
                        break;
                }

                return result;
            }

            public EtlVariable[] GetEtlVariables(string etlPackageId, string etlSessionId)
            {
                var result = new List<EtlVariable>();

                foreach (var agent in _etlAgents)
                {
                    var logParser = agent.GetEtlLogParser();
                    result.AddRange(logParser.GetEtlVariables(etlPackageId, etlSessionId));
                }

                return result.ToArray();
            }

            public EtlCounter[] GetEtlCounters(string etlPackageId, string etlSessionId)
            {
                var result = new List<EtlCounter>();

                foreach (var agent in _etlAgents)
                {
                    var logParser = agent.GetEtlLogParser();
                    result.AddRange(logParser.GetEtlCounters(etlPackageId, etlSessionId));
                }

                return result.ToArray();
            }

            public EtlMessage[] GetEtlMessages(string etlPackageId, string etlSessionId)
            {
                var result = new List<EtlMessage>();

                foreach (var agent in _etlAgents)
                {
                    var logParser = agent.GetEtlLogParser();
                    result.AddRange(logParser.GetEtlMessages(etlPackageId, etlSessionId));
                }

                return result.ToArray();
            }

            #endregion
        }
    }
}
