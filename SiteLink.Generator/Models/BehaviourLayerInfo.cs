using System;
using System.Collections.Generic;

namespace SiteLink.Generator.Models;

public class BehaviourLayerInfo
{
    public Type BehaviourType;
    public string ComponentClassName;
    public string BaseComponentClassName;
    public int SyncVarBitOffset;

    public List<SyncVarInfo> DeclaredSyncVars = new();
    public List<SyncListInfo> DeclaredSyncLists = new();
}
