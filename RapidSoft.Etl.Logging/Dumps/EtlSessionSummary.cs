using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

using RapidSoft.Etl.Logging;

namespace RapidSoft.Etl.Logging.Dumps
{
    [Serializable]
    public class EtlSessionSummary
    {
        public string EtlPackageId
        {
            get;
            set;
        }

        public string EtlPackageName
        {
            get;
            set;
        }

        public string EtlSessionId
        {
            get;
            set;
        }

        public DateTime StartDateTime
        {
            get;
            set;
        }

        public DateTime StartUtcDateTime
        {
            get;
            set;
        }

        public DateTime? EndDateTime
        {
            get;
            set;
        }

        public DateTime? EndUtcDateTime
        {
            get;
            set;
        }

        public EtlStatus Status
        {
            get;
            set;
        }

        public string UserName
        {
            get;
            set;
        }

        [XmlArrayItem("Variable")]
        public List<EtlVariable> Variables
        {
            [DebuggerStepThrough]
            get
            {
                return _variables;
            }
        }
        private readonly List<EtlVariable> _variables = new List<EtlVariable>();

        [XmlArrayItem("Counter")]
        public List<EtlCounter> Counters
        {
            [DebuggerStepThrough]
            get
            {
                return _counters;
            }
        }
        private readonly List<EtlCounter> _counters = new List<EtlCounter>();

        [XmlArrayItem("Message")]
        public List<EtlMessage> Messages
        {
            [DebuggerStepThrough]
            get
            {
                return _messages;
            }
        }
        private readonly List<EtlMessage> _messages = new List<EtlMessage>();

        public EtlMessage LastErrorMessage
        {
            get;
            set;
        }
    }
}