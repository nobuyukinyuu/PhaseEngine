using System;
using System.Collections.Generic;
using PE_Json;
using static System.Globalization.CultureInfo;

namespace PE_Json
{
    public interface IJSONSerializable
    {
        public string ToJSONString() => ToJSONObject().ToJSONString();
        public JSONObject ToJSONObject();


        public bool FromJSON(JSONObject data);

        public bool FromString(string input)  //Basic reader for classes which don't implement their own
        {
            var P = JSONData.ReadJSON(input);
            if (P is JSONDataError) throw new ArgumentException("JSON Data invalid. " + P.ToString());
            var j = (JSONObject) P;
            return FromJSON(j);
        }
    }

}