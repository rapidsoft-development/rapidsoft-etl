﻿using System;

namespace RapidSoft.Etl.Runtime.DataSources.Csv
{
    public enum MissingFieldAction
    {
        ParseError = 0,
        ReplaceByEmpty = 1,
        ReplaceByNull = 2,
    }
}