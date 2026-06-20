using System.Collections.Generic;

namespace SiteLink.Generator.Models
{
    public class CustomSerializationInfo
    {
        public string MethodName { get; set; } = "";
        public bool RunOnInitialOnly { get; set; } = false;
        public List<string> WriteCalls { get; set; } = new();
    }
}
