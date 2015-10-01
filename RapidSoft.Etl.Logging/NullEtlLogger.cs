using System;
using System.Collections.Generic;

namespace RapidSoft.Etl.Logging
{
    public sealed class NullEtlLogger : IEtlLogger
    {
        #region IEtlLogger Members

        public void LogEtlSessionStart(EtlSession session)
		{
        }

        public void LogEtlSessionEnd(EtlSession session)
        {
        }

        public void LogEtlVariable(EtlVariable variable)
        {
        }

        public void LogEtlCounter(EtlCounter counter)
        {
        }

        public void LogEtlMessage(EtlMessage message)
        {
        }

		public void BatchLogEtlMessage(EtlMessage[] messages)
		{
		}

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
        }

        #endregion
    }
}