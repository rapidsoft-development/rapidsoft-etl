using System;
using System.ComponentModel;

using RapidSoft.Etl.Logging;

namespace RapidSoft.Etl.Monitor.Models
{
    public static class EtlPackageMonitorItemStatuses
    {
        public static string ToString(EtlPackageMonitorItemStatus status)
        {
            //todo: localize statuses
            switch (status)
            {
                case EtlPackageMonitorItemStatus.Succeeded:
                    return "Успешно выполнен";
                case EtlPackageMonitorItemStatus.TooFar:
                    return "Выполнен давно";
                case EtlPackageMonitorItemStatus.Running:
                    return "Выполняется...";
                case EtlPackageMonitorItemStatus.Never:
                    return "Ранее не выполнялся";
                case EtlPackageMonitorItemStatus.Failed:
                    return "Выполнен с проблемами";
                default:
                    return "(неизвестно)";
            }
        }

        public static EtlPackageMonitorItemStatus GetMonitorItemStatus(EtlSession session, int runInvervalSeconds)
        {
            return session.Status == 
                EtlStatus.Failed ? EtlPackageMonitorItemStatus.Failed :
                // пакет выполнялся слишком давно
                session.EndDateTime.HasValue ?
                    runInvervalSeconds > 0 && DateTime.Now.Subtract(session.EndDateTime.Value).TotalSeconds >= runInvervalSeconds ? 
                        EtlPackageMonitorItemStatus.TooFar :
                                session.Status == EtlStatus.Succeeded ? 
                                    EtlPackageMonitorItemStatus.Succeeded : 
                                    EtlPackageMonitorItemStatus.Failed : 
                    EtlPackageMonitorItemStatus.Running;
        }
    }
}
