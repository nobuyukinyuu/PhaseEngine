using System;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using PE_Json;
using static System.Globalization.CultureInfo;

namespace PE_Json
{
    public abstract class JSONDataItem : IConvertible{

        public int dataType = JSONDataType.JSON_NULL;

        public virtual int ToInt(){
            Debug.Print ("Unsupported conversion to Int for " + this.ToString());
            throw new NotImplementedException();
            // return -1;
        }

        public virtual float ToFloat(){
            Debug.Print ("Unsupported conversion to Float for " + this.ToString());
            throw new NotImplementedException();
            // return -1.0f;
        }

        public virtual bool ToBool(){
            Debug.Print ("Unsupported conversion to Bool for " + this.ToString());
            throw new NotImplementedException();
            // return false;
        }

        //Method ToPrettyString() Abstract
        public override abstract string ToString();
        public virtual string ToJSONString(){ 
            return ToString();
        }

        public TypeCode GetTypeCode() => TypeCode.Object;

        public bool ToBoolean(IFormatProvider provider) =>
            throw new NotImplementedException();

        public byte ToByte(IFormatProvider provider) =>
            throw new NotImplementedException();

        public char ToChar(IFormatProvider provider) =>
            throw new NotImplementedException();

        public DateTime ToDateTime(IFormatProvider provider) =>
            throw new NotImplementedException();

        public decimal ToDecimal(IFormatProvider provider) =>
            throw new NotImplementedException();

        public double ToDouble(IFormatProvider provider) =>
            throw new NotImplementedException();

        public short ToInt16(IFormatProvider provider) =>
            throw new NotImplementedException();

        public int ToInt32(IFormatProvider provider) =>
            throw new NotImplementedException();

        public long ToInt64(IFormatProvider provider) =>
            throw new NotImplementedException();

        public sbyte ToSByte(IFormatProvider provider) =>
            throw new NotImplementedException();

        public float ToSingle(IFormatProvider provider) =>
            throw new NotImplementedException();

        public string ToString(IFormatProvider provider) =>
            throw new NotImplementedException();

        public object ToType(Type conversionType, IFormatProvider provider) =>
            throw new NotImplementedException();

        public ushort ToUInt16(IFormatProvider provider) =>
            throw new NotImplementedException();

        public uint ToUInt32(IFormatProvider provider) =>
            throw new NotImplementedException();

        public ulong ToUInt64(IFormatProvider provider) =>
            throw new NotImplementedException();


    }
}