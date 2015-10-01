﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Xml.Serialization;
using System.Xml;
using System.Data;

namespace RapidSoft.Etl.Runtime.Steps
{
    [Serializable]
    [DebuggerDisplay("{ToDebuggerDisplayString()}")]
    [TypeConverter(typeof(EtlFieldToVariableAssignmentObjectConverter))]
    public sealed class EtlFieldToVariableAssignment
    {
        #region Constructors

        public EtlFieldToVariableAssignment()
        {
        }

        #endregion

        #region Properties

        [Category("1. Source")]
        public string SourceFieldName
        {
            get;
            set;
        }

        [Category("1. Source")]
        public EtlValueTranslation SourceFieldTranslation
        {
            get;
            set;
        }

        [Category("2. Destination")]
        public string VariableName
        {
            get;
            set;
        }

        [Category("3. Defaults")]
        public string DefaultValue
        {
            get;
            set;
        }

        #endregion

        #region Methods

        private string ToDebuggerDisplayString()
        {
            return string.Concat
            (
                (string.IsNullOrEmpty(SourceFieldName) ? this.DefaultValue : this.SourceFieldName),
                " -> ",
                this.VariableName
            );
        }

        #endregion

        #region Nested classes

        public sealed class EtlFieldToVariableAssignmentObjectConverter : ExpandableObjectConverter
        {
            public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            {
                if (sourceType == typeof(string))
                {
                    return true;
                }

                return false;
            }

            public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
            {
                if (destinationType == typeof(string))
                {
                    return true;
                }

                return false;
            }

            public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
            {
                if (destinationType == typeof(string))
                {
                    var obj = (EtlFieldToVariableAssignment)value;

                    if (obj != null)
                    {
                        return obj.ToDebuggerDisplayString();
                    }
                    else
                    {
                        return null;
                    }
                }

                return null;
            }

            public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
            {
                if (value == null)
                {
                    return null;
                }

                var str = value.ToString();
                if (str == "")
                {
                    return null;
                }

                return new EtlFieldToVariableAssignment();
            }
        }

        #endregion
    }
}
