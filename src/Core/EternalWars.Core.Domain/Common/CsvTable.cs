using System.Globalization;
using System.Text;

namespace EternalWars.Core.Domain.Common;

/// <summary>
/// The spreadsheet-friendly CSV shared by the content tables (<c>CreatureTable</c>, <c>HeroTable</c>, ADR-044):
/// the delimiter comes from the header row (comma, semicolon or tab; a leading <c>sep=;</c> line wins), quotes follow
/// RFC 4180, columns are matched by header name case-insensitively (unknown columns are ignored), empty rows and rows
/// whose first cell starts with <c>#</c> are skipped, and list cells separate items with <c>;</c> or <c>|</c>.
/// </summary>
public sealed class CsvTable
{
    private static readonly char[] ListSeparators = [';', '|'];

    private CsvTable(IReadOnlyList<string> header, IReadOnlyList<Row> rows)
    {
        Header = header;
        Rows = rows;
    }

    /// <summary>Lower-case column names.</summary>
    public IReadOnlyList<string> Header { get; }

    public IReadOnlyList<Row> Rows { get; }

    /// <summary>Reads the table and checks the <paramref name="required"/> columns exist.</summary>
    public static CsvTable Parse(string text, string what, IEnumerable<string> required)
    {
        var rows = ReadRows(text.TrimStart('﻿'));
        if (rows.Count == 0)
        {
            throw new FormatException($"{what} table is empty.");
        }

        var header = rows[0].Cells.Select(c => c.Trim().ToLowerInvariant()).ToList();
        var missing = required.Where(c => !header.Contains(c)).ToList();
        if (missing.Count > 0)
        {
            throw new FormatException($"{what} table lacks the column(s) {string.Join(", ", missing)}.");
        }

        var data = rows.Skip(1)
            .Select(r => new Row(header, r.Cells, r.Line))
            .Where(r => r.Cells.FirstOrDefault()?.TrimStart().StartsWith('#') != true)
            .ToList();
        return new CsvTable(header, data);
    }

    /// <summary>Runs <paramref name="read"/> per row; errors that lack a position get "Line n:" in front.</summary>
    public IReadOnlyList<T> Read<T>(Func<Row, T> read) =>
        Rows.Select(row =>
        {
            try
            {
                return read(row);
            }
            catch (FormatException e) when (!e.Message.StartsWith("Line ", StringComparison.Ordinal))
            {
                throw new FormatException($"Line {row.Line}: {e.Message}", e);
            }
        }).ToList();

    /// <summary>Writes a comma-separated table.</summary>
    public static string Format(IReadOnlyList<string> columns, IEnumerable<IReadOnlyList<string>> rows)
    {
        var text = new StringBuilder();
        text.AppendLine(string.Join(',', columns));
        foreach (var row in rows)
        {
            text.AppendLine(string.Join(',', row.Select(Quote)));
        }

        return text.ToString();
    }

    public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Quote(string cell) =>
        cell.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{cell.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : cell;

    /// <summary>Splits the text into rows of cells, keeping each row's 1-based line number for error messages.</summary>
    private static List<(int Line, List<string> Cells)> ReadRows(string text)
    {
        var lines = text.Split('\n');
        var start = 0;
        char? hinted = null;
        if (lines.Length > 0 && lines[0].TrimEnd('\r') is var first && first.StartsWith("sep=", StringComparison.OrdinalIgnoreCase) && first.Length == 5)
        {
            hinted = first[4];
            start = 1;
        }

        var headerLine = start < lines.Length ? lines[start] : "";
        var delimiter = hinted ?? new[] { ',', ';', '\t' }.MaxBy(c => headerLine.Count(h => h == c));

        var rows = new List<(int, List<string>)>();
        var cells = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        var rowLine = start + 1;
        var line = start + 1;
        var body = string.Join('\n', lines.Skip(start));
        for (var i = 0; i < body.Length; i++)
        {
            var c = body[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < body.Length && body[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    line += c == '\n' ? 1 : 0;
                    cell.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == delimiter)
            {
                cells.Add(cell.ToString());
                cell.Clear();
            }
            else if (c == '\n')
            {
                EndRow();
                line++;
                rowLine = line;
            }
            else if (c != '\r')
            {
                cell.Append(c);
            }
        }

        EndRow();
        return rows;

        void EndRow()
        {
            cells.Add(cell.ToString());
            cell.Clear();
            if (cells.Any(c => c.Trim().Length > 0))
            {
                rows.Add((rowLine, cells));
            }

            cells = [];
        }
    }

    /// <summary>One data row; accessors throw <see cref="FormatException"/> naming the line and column.</summary>
    public sealed class Row(IReadOnlyList<string> header, IReadOnlyList<string> cells, int line)
    {
        public int Line { get; } = line;

        public IReadOnlyList<string> Cells { get; } = cells;

        public string Text(string column)
        {
            var index = IndexOf(column);
            return index >= 0 && index < Cells.Count ? Cells[index].Trim() : "";
        }

        public string? Optional(string column) => Text(column) is { Length: > 0 } text ? text : null;

        public int Int(string column, int? fallback = null)
        {
            var text = Text(column);
            if (text.Length == 0 && fallback is { } value)
            {
                return value;
            }

            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                ? number
                : throw Error(column, $"'{text}' is not a whole number");
        }

        public T Enum<T>(string column) where T : struct, System.Enum =>
            System.Enum.TryParse<T>(Text(column).Replace(" ", "", StringComparison.Ordinal), ignoreCase: true, out var value) && System.Enum.IsDefined(value)
                ? value
                : throw Error(column, $"'{Text(column)}' is not one of {string.Join(", ", System.Enum.GetNames<T>())}");

        public IReadOnlyList<string> List(string column) =>
            Text(column).Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        public FormatException Error(string column, string problem) => new($"Line {Line}, column '{column}': {problem}.");

        private int IndexOf(string column)
        {
            for (var i = 0; i < header.Count; i++)
            {
                if (header[i] == column)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
