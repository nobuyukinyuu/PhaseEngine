using System;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using PE_Json;
using static System.Globalization.CultureInfo;

namespace PE_Json
{
    class JSONBool : JSONDataItem, IConvertible {
        bool value;
            
        public JSONBool(bool value){
            dataType = JSONDataType.JSON_BOOL;
            this.value = value;
        }

        public override bool ToBool() => value;
        
        public override string ToString(){
            if (value){
                return "true";
            } else {
                return "false";
            }
        }

        public override string ToJSONString() => ToString();

        // IConvertible implementations
        public new TypeCode GetTypeCode() => TypeCode.Boolean;
        public new bool ToBoolean(IFormatProvider provider) => value;
        public new string ToString(IFormatProvider provider) => ToString();
    }
}