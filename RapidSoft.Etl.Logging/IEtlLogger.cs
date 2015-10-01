using System;
using System.Collections.Generic;

namespace RapidSoft.Etl.Logging
{
    public interface IEtlLogger : IDisposable
    {
        void LogEtlSessionStart(EtlSession session);

        void LogEtlSessionEnd(EtlSession session);

        void LogEtlVariable(EtlVariable variables);

        void LogEtlCounter(EtlCounter counters);
        
        void LogEtlMessage(EtlMessage message);

		void BatchLogEtlMessage(EtlMessage[] messages);
    }
}