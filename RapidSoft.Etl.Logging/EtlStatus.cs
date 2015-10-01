using System;
using System.ComponentModel;

namespace RapidSoft.Etl.Logging
{
	[Serializable]
	public enum EtlStatus
	{
		Started = 1,
		Succeeded = 2,
		FinishedWithLosses = 4,
		FinishedWithWarnings = 5,
		Failed = 10,
	}
}