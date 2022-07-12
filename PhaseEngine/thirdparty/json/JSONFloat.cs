using System;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using PE_Json;
using static System.Globalization.CultureInfo;

namespace PE_Json
{
    public class JSONFloat : JSONDataItem, IConvertible {
        float value;
        string unparsedStr;
        bool unparsed = false;
        
        public JSONFloat(float value){
            dataType = JSONDataType.JSON_FLOAT ;
            this.value = value;
        }

        //This constructor creates a float container that stores the unparsed
        //value string. This is to spread the load of parsing the data
        //as parsing floats is very expensive on Android.
        public JSONFloat(string unparsedStr){
            dataType = JSONDataType.JSON_FLOAT;
            this.unparsedStr = unparsedStr;
            this.unparsed = true;
        }
        
        public void Parse(){
            if (unparsed){
                value = Convert.ToSingle(unparsedStr);
                unparsed = false;
            }
        }
        
        public override int ToInt(){
            Parse();
            return (int)(value);
        }

        public override float ToFloat(){
            Parse();
            return value;
        }

        public override string ToString(){
            Parse();
            return Convert.ToString(value, InvariantCulture);
        }

        // IConvertible implementations
        public new TypeCode GetTypeCode() => TypeCode.Single;
        public new float ToSingle(IFormatProvider provider) => ToFloat();
        public new double ToDouble(IFormatProvider provider) => ToFloat();
        public new int ToInt32(IFormatProvider provider) => ToInt();
        public new long ToInt64(IFormatProvider provider) => ToInt();

        public new string ToString(IFormatProvider provider) => ToString();
    }    
}