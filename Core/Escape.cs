using System.Text;

namespace WebToAppStudio.Core;

public static class Escape
{
    /// <summary>Escapes text for an Android string resource (strings.xml).</summary>
    public static string AndroidString(string s)
    {
        var t = s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"")
                 .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                 .Replace("\r", "").Replace("\n", "\\n");
        if (t.StartsWith('@') || t.StartsWith('?')) t = "\\" + t;
        return t;
    }

    /// <summary>Escapes text for XML/plist content.</summary>
    public static string Xml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");

    /// <summary>Writes UTF-8 (no BOM), creating folders as needed.</summary>
    public static void WriteFile(string root, string relativePath, string content)
    {
        var full = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content.Replace("\r\n", "\n"), new UTF8Encoding(false));
    }
}
