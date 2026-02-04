using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace SiteLink.Generator;

public static class Extensions
{
    private static readonly Dictionary<Type, string> Aliases =
        new Dictionary<Type, string>()
        {
            { typeof(byte), "byte" },
            { typeof(sbyte), "sbyte" },
            { typeof(short), "short" },
            { typeof(ushort), "ushort" },
            { typeof(int), "int" },
            { typeof(uint), "uint" },
            { typeof(long), "long" },
            { typeof(ulong), "ulong" },
            { typeof(float), "float" },
            { typeof(double), "double" },
            { typeof(decimal), "decimal" },
            { typeof(object), "object" },
            { typeof(bool), "bool" },
            { typeof(char), "char" },
            { typeof(string), "string" },
            { typeof(void), "void" },

            // From C# 11 onwards
            { typeof(nint), "nint" },
            { typeof(nuint), "nuint" },
        };

    private static readonly Regex sWhitespace = new Regex(@"\s+");

    public static string ToAlias(this Type type)
    {
        if (Aliases.TryGetValue(type, out string res))
            return res;

        return type.Name;
    }

    public static string CapitalizeFirst(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToUpper(input[0]) + input.Substring(1);
    }

    public static string LowerFirst(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToLower(input[0]) + input.Substring(1);
    }

    public static string ReplaceWhitespace(this string input, string replacement)
    {
        return sWhitespace.Replace(input, replacement);
    }

    public static string FindWriterMethodName(this Type fieldType, out string nameSpace)
    {
        try
        {
            Type writerGeneric = typeof(Writer<>).MakeGenericType(fieldType);

            var writeField = writerGeneric.GetField("write", BindingFlags.Public | BindingFlags.Static);

            var del = writeField?.GetValue(null) as Delegate;

            nameSpace = del?.Method?.DeclaringType?.Namespace ?? null;

            return del?.Method?.Name ?? "(no writer found)";
        }
        catch
        {
            nameSpace = null;
            return "(no writer found)";
        }
    }

    public static string FindReaderMethodName(this Type fieldType)
    {
        try
        {
            Type readerGeneric = typeof(Reader<>).MakeGenericType(fieldType);
            var readField = readerGeneric.GetField("read", BindingFlags.Public | BindingFlags.Static);
            var del = readField?.GetValue(null) as Delegate;
            return del?.Method?.Name ?? "(no reader found)";
        }
        catch { return "(no reader found)"; }
    }

    public static IEnumerable<FieldInfo> GetSyncVars(this Type type)
    {
        if (type == null || type == typeof(NetworkBehaviour))
            yield break;

        foreach (var field in GetSyncVars(type.BaseType))
            yield return field;

        foreach (var field in type
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(f => f.IsDefined(typeof(SyncVarAttribute), true))
            .OrderBy(f => f.MetadataToken))
        {
            yield return field;
        }
    }

    public static string ToFunctionFullName(this MethodInfo method)
    {
        StringBuilder stringBuilder = new StringBuilder();

        stringBuilder.Append(method.ReturnType.FullName).Append(" ").Append(MemberFullName(method));

        MethodSignatureFullName(method, stringBuilder);

        return stringBuilder.ToString();
    }

    static string MemberFullName(MethodInfo method)
    {
        if (method.DeclaringType == null)
            return method.Name;

        return method.DeclaringType.FullName + "::" + method.Name;
    }

    static void MethodSignatureFullName(MethodInfo method, StringBuilder builder)
    {
        builder.Append("(");
        var parameters = method.GetParameters();

        for (int i = 0; i < parameters.Length; i++)
        {
            ParameterInfo parameterDefinition = parameters[i];
            if (i > 0)
            {
                builder.Append(",");
            }

            //if (parameterDefinition.ParameterType.IsSentinel)
            //{
            ///    builder.Append("...,");
            //}

            builder.Append(parameterDefinition.ParameterType.FullName.Replace("+", "/"));
        }

        builder.Append(")");
    }
}
