using System.Collections.Generic;

using RapidSoft.Etl.Logging;
using RapidSoft.Etl.Logging.Dumps;

namespace RapidSoft.Etl.Monitor.Models
{
    public class EtlSessionListModel
    {
        #region Fields

        public readonly int DefaultTimespanDays = 7;
		public readonly int DefaultMaxSessionCount = 250;

        #endregion

        #region Properties

        [DataTypeValidation(DataType.DateTime, ErrorMessage = "Начальная дата указана неверно")]
        public string DateFrom
        {
            get;
            set;
        }

        [DataTypeValidation(DataType.DateTime, ErrorMessage = "Конечная дата указана неверно")]
        public string DateTo
        {
            get;
            set;
        }

        public EtlStatus Status
        {
            get;
            set;
        }

        public List<EtlSession> Sessions
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
                    return "";
                case EtlStatus.Failed:
                case EtlStatus.FinishedWithLosses:
                case EtlStatus.FinishedWithWarnings:
                default:
                    return "redbox";
            }
        }

        #endregion
    }
}