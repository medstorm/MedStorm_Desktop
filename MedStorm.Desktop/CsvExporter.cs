using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

public static class CsvExporter
{
    /// <summary>
    /// Export list of any class to CSV. Handles escaping.
    /// </summary>
    public static void ExportToCsv<T>(IEnumerable<T> data, string filePath)
    {
        var sb = new StringBuilder();
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Header
        sb.AppendLine(string.Join(",", props.Select(p => $"\"{p.Name}\"")));

        // Rows
        foreach (var item in data)
        {
            var line = string.Join(",", props.Select(p =>
            {
                var val = p.GetValue(item, null)?.ToString() ?? "";
                val = val.Replace("\"", "\"\"");
                return $"\"{val}\"";
            }));
            sb.AppendLine(line);
        }
        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }
}
