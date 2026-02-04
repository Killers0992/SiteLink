using System.Reflection;

namespace SiteLink.Generator.Models;

public class CommandInfo
{
    public string FunctionFullName { get; set; }
    public string Name { get; set; }
    public ParameterInfo[] Parameters { get; set; }

    public int Hash { get; set; }
}
