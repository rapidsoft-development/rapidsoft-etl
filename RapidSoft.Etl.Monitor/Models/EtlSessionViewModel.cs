using System;
using System.Collections.Generic;

using RapidSoft.Etl.Logging;
using RapidSoft.Etl.Logging.Dumps;

namespace RapidSoft.Etl.Monitor.Models
{
    public class EtlSessionViewModel
	{
        #region Properties

        public string BackUrl
        {
            get;
            set;
        }

        public bool NotFound
        {
            get;
            set;
        }

        public EtlSessionSummary Session
        {
            get;
            set;
        }

        #endregion

        #region Methods

        public static string GetEtlStatusClass(EtlStatus status)
        {
            switch (status)
            {
                case EtlStatus.Started:
                    return "yellowbox";
                case EtlStatus.Succeeded:
                    //return "greenbox";
                    return "";
                case EtlStatus.Failed:
                case EtlStatus.FinishedWithLosses:
                case EtlStatus.FinishedWithWarnings:
                default:
                    return "redbox";
            }
        }

        public static string GetEtlMessageClass(EtlMessageType type)
        {
            switch (type)
            {
                case EtlMessageType.Error:
                //case EtlMessageType.DataLostWarning:
                //case EtlMessageType.ValidationWarning:
                    return "redbox";
                default:
                    return "";
            }
        }

        #endregion
	}
}