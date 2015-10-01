using System;
using System.Collections.Generic;

namespace RapidSoft.Etl.Runtime.DataSources.Csv
{
    [Serializable]
    public struct CsvReaderOptions
    {
        public int BufferSize
        {
            get;
            set;
        }

        public ValueTrimmingOptions ValueTrimmingOptions
        {
            get;
            set;
        }

        public bool PreserveEmptyStrings
        {
            get;
            set;
        }
    }
}
