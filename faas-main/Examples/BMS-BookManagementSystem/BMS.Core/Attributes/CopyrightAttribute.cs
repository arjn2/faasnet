using System.Reflection;

namespace BMS.Core.Attributes;

/// <summary>
/// Simple copyright attribute with MIT license
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class CopyrightAttribute : Attribute
{
    public string Author { get; }
    public int Year { get; }
    public string License { get; } = "MIT";

    public CopyrightAttribute(string author, int year)
    {
        Author = author;
        Year = year;
    }

    public override string ToString()
    {
        return $"(c) {Year} {Author} - Licensed under MIT License";
    }
}

/// <summary>
/// Simple service to get copyright info using reflection
/// </summary>
public static class CopyrightHelper
{
    public static string GetCopyright(Type type)
    {
        var attr = type.GetCustomAttribute<CopyrightAttribute>();
        return attr?.ToString() ?? "(c) 2025 ARJUN A L - Licensed under MIT License";
    }
}