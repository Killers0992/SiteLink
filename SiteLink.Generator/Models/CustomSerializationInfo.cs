using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiteLink.Generator.Models
{
    public class CustomSerializationInfo
    {
        public string MethodName { get; set; } = "";
        public bool RunOnInitialOnly { get; set; } = false;
        public List<string> WriteCalls { get; set; } = new();
    }
}
