using System;
using System.Collections.Generic;

using RapidSoft.Etl.Logging;

namespace RapidSoft.Etl.Runtime.Agents
{
    public interface IEtlAgent
    {
        EtlAgentInfo GetEtlAgentInfo();

        void DeployEtlPackage(EtlPackage package, EtlPackageDeploymentOptions options);

        EtlPackage[] GetEtlPackages();
        EtlPackage GetEtlPackage(string etlPackageId);
        EtlSession InvokeEtlPackage(string etlPackageId, EtlVariableAssignment[] parameters, string parentSessionId);

        IEtlLogParser GetEtlLogParser();
    }
}