using System;

namespace Avro2ParquetFunction
{

    // [{"ObjectID":200,"Value":"0","ClientID":"G1","TimeStamp":"2017-09-15T01:45:07.4079205Z"}] 
    public class Telemetry
    {
        public int ObjectID { get; set; }
        public string Value { get; set; }
        public string ClientID { get; set; }
        public DateTime TimeStamp { get; set; }
    }
}
