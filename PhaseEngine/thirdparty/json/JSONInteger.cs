using System;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using PE_Json;
using static System.Globalization.CultureInfo;

namespace PE_Json
{
    class JSONInteger : JSONDataItem, IConvertible {
        int value;
        
        public JSONInteger(int value) {
            dataType = JSONDataType.JSON_INTEGER;
            this.value = value;
        }

        public override int ToInt(){
            return value;
        }

        public override float ToFloat(){
            return (float)(value);
        }

        public override string ToString(){
            return Convert.ToString(value, InvariantCulture);
        }


        // IConvertible implementations
        public new TypeCode GetTypeCode() => TypeCode.Int32;
        public new float ToSingle(IFormatProvider provider) => ToFloat();
        public new double ToDouble(IFormatProvider provider) => ToFloat();
        public new int ToInt32(IFormatProvider provider) => ToInt();
        public new long ToInt64(IFormatProvider provider) => ToInt();

        public new string ToString(IFormatProvider provider) => ToString();
    }
}
