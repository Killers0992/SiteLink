using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiteLink.Generator.Models
{
    public class CustomPropertyInfo
    {
        public string TypeName { get; set; } = "";
        public string PropertyName { get; set; } = "";
        public bool IsSyncVar { get; set; } = true;
    }
}
