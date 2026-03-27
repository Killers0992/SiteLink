using System;

namespace SiteLink.Generator.Models;

public class SyncVarInfo
{
    public string Name { get; set; }

    public string NormalName
    {
        get
        {
            string final = Name.ReplaceWhitespace(string.Empty);

            if (final.StartsWith("_"))
                final = final.Substring(1);

            return final.CapitalizeFirst();
        }
    }

    public string PrivateName => $"_{NormalName.LowerFirst()}";

    public string ValueName => ValueType.ToAlias();

    public Type ValueType { get; set; }
    public ulong Bit { get; set; }

    public string WriterName
    {
        get
        {
            string name = ValueType.FindWriterMethodName(out _);

            // Invalid
            if (name.StartsWith("_"))
                name = "Write";

            return name;
        }
    }

    public string WriterNamespace
    {
        get
        {
            string nameSpace;
            ValueType.FindWriterMethodName(out nameSpace);
            return nameSpace;
        }
    }

    public string ReaderName => ValueType.FindReaderMethodName();
}
