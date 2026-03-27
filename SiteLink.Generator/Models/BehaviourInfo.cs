using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiteLink.Generator.Models;

public class BehaviourInfo
{
    public string Name { get; set; }

    public string NormalName => Name.CapitalizeFirst().ReplaceWhitespace(string.Empty);
    public string ClassName => $"{NormalName}Component";

    public Type BehaviourType { get; set; }

    public List<SyncVarInfo> SyncVars { get; set; } = new List<SyncVarInfo>();
    public List<SyncListInfo> SyncLists { get; set; } = new List<SyncListInfo>();

    public List<CommandInfo> Commands { get; set; } = new List<CommandInfo>();
}
