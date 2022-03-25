using System;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using PE_Json;
using static System.Globalization.CultureInfo;

namespace PE_Json
{
    class JSONString : JSONDataItem, IConvertible {        
        public string value;
        string jsonReady = "";
        
        
        public JSONString(string value, bool isMonkeyString = true) {
            dataType = JSONDataType.JSON_STRING;
            if (!isMonkeyString){
                this.value = JSONData.UnEscapeJSON(value);
                jsonReady = "\"" + value + "\"";
            } else {
                this.value = value;
            }
        }

        public override string ToJSONString(){
            if (jsonReady == ""){
                jsonReady = "\"" + JSONData.EscapeJSON(value) + "\"";
            }
            return jsonReady;
        }

        public override string ToString(){
            return value;
        }

        // IConvertible implementations
        public new TypeCode GetTypeCode() => TypeCode.String;
        public new string ToString(IFormatProvider provider) => ToString();
    }
}