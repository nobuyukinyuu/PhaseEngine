using System;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using PE_Json;
using static System.Globalization.CultureInfo;

namespace PE_Json
{
    class JSONObjectMember : JSONDataItem, IConvertible
    {
        string name;
        JSONDataItem dataItem;

        JSONObjectMember(string name, JSONDataItem dataItem){
            dataType = JSONDataType.JSON_OBJECT_MEMBER;
            this.name = name;
            this.dataItem = dataItem;
        }

        new bool ToBool(){
            return dataItem.ToBool();
        }
        
        new int ToInt(){
            return dataItem.ToInt();
        }
        
        new float ToFloat(){
            return dataItem.ToFloat();
        }
        
        public override string ToString(){
            return dataItem.ToString();
        }

        public override string ToJSONString(){
            return dataItem.ToJSONString();
        }

        // IConvertible implementations
        public new bool ToBoolean(IFormatProvider provider) => ToBool();
        public new float ToSingle(IFormatProvider provider) => ToFloat();
        public new double ToDouble(IFormatProvider provider) => ToFloat();
        public new int ToInt32(IFormatProvider provider) => ToInt();
        public new long ToInt64(IFormatProvider provider) => ToInt();

        public new string ToString(IFormatProvider provider) => ToString();
    }
}