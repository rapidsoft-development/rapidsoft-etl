using System;

namespace RapidSoft.Etl.Logging
{
	public interface IEtlLogParser
	{
        EtlSession[] GetEtlSessions(EtlSessionQuery query);
        EtlSession[] GetLatestEtlSessions(string[] etlPackageIds);
        EtlSession GetEtlSession(string etlPackageId, string etlSessionId);
       
        EtlVariable[] GetEtlVariables(string etlPackageId, string etlSessionId);
        EtlCounter[] GetEtlCounters(string etlPackageId, string etlSessionId);
        EtlMessage[] GetEtlMessages(string etlPackageId, string etlSessionId);
	}
}