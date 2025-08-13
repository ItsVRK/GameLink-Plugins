using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace XPlane12Plugin.Model
{
    public class DatarefValue
    {
        public string InputName { get; set; }
        public string Dataref { get; set; }
        public int Id { get; set; }

        [JsonIgnore]
        public float Value { get; set; }

    }
}
