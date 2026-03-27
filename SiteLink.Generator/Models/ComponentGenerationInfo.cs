using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiteLink.Generator.Models
{
    public class ComponentGenerationInfo
    {
        public List<CustomPropertyInfo> ExtraProperties { get; set; } = new();
        public List<CustomSerializationInfo> SerializationHooks { get; set; } = new();
    }
}
