using System;
using System.Collections.Generic;
using System.Linq;

namespace MechanicalCataphract.Discord;

/// <summary>
/// Parses player messages from Discord into structured commands.
/// Handles :envelope: (messages) and :scroll: (orders) formats.
/// </summary>
public static class DiscordMessageParser
{
    /// <summary>
    /// Attempts to parse a Discord message into a player command.
    /// Returns null if the message doesn't match any known format.
    /// </summary>
    public static ParsedCommand? Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var lines = content.Split('\n', StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
            return null;

        // Check first line for command emoji
        var firstLine = lines[0];

        if (ContainsEnvelopeEmoji(firstLine))
            return ParseEnvelope(lines);

        if (ContainsScrollEmoji(firstLine))
            return ParseScroll(lines);

        return null;
    }

    private static bool ContainsEnvelopeEmoji(string line)
    {
        // Match Discord emoji format :envelope: or the actual Unicode ✉️/📩/📨
        return line.Contains(":envelope:", StringComparison.OrdinalIgnoreCase)
            || line.Contains("\u2709")  // ✉
            || line.Contains("\U0001F4E9") // 📩
            || line.Contains("\U0001F4E8"); // 📨
    }

    private static bool ContainsScrollEmoji(string line)
    {
        return line.Contains(":scroll:", StringComparison.OrdinalIgnoreCase)
            || line.Contains("\U0001F4DC"); // 📜
    }

    /// <summary>
    /// Parses an :envelope: message. Format:
    ///   :envelope:
    ///   Target commander name (optional)
    ///   Target location col,row (optional)
    ///   Content (remaining lines)
    /// </summary>
    private static ParsedCommand ParseEnvelope(string[] lines)
    {
        string? targetCommander = null;
        (int col, int row)? targetLocation = null;
        var contentLines = new List<string>();
        var state = EnvelopeParseState.Header;

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            switch (state)
            {
                case EnvelopeParseState.Header:
                    // Try to parse as coordinates first (col,row)
                    if (TryParseLocation(line, out var loc))
                    {
                        targetLocation = loc;
                        state = EnvelopeParseState.Content;
                    }
                    else
                    {
                        // First non-empty line after header is target commander
                        targetCommander = line.Trim();
                        state = EnvelopeParseState.LocationOrContent;
                    }
                    break;

                case EnvelopeParseState.LocationOrContent:
                    if (TryParseLocation(line, out var loc2))
                    {
                        targetLocation = loc2;
                        state = EnvelopeParseState.Content;
                    }
                    else
                    {
                        // Not a location — this is content
                        contentLines.Add(line);
                        state = EnvelopeParseState.Content;
                    }
                    break;

                case EnvelopeParseState.Content:
                    contentLines.Add(line);
                    break;
            }
        }

        return new ParsedCommand
        {
            Type = CommandType.Envelope,
            TargetCommanderName = targetCommander,
            TargetLocationCol = targetLocation?.col,
            TargetLocationRow = targetLocation?.row,
            Content = string.Join("\n", contentLines),
        };
    }

    /// <summary>
    /// Parses a :scroll: order. Format:
    ///   :scroll:
    ///   Content (all remaining lines)
    /// </summary>
    private static ParsedCommand ParseScroll(string[] lines)
    {
        var contentLines = new List<string>();
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!string.IsNullOrWhiteSpace(line) || contentLines.Count > 0)
                contentLines.Add(line);
        }

        return new ParsedCommand
        {
            Type = CommandType.Scroll,
            Content = string.Join("\n", contentLines).Trim(),
        };
    }

    /// <summary>
    /// Tries to parse a location string in "col,row" format.
    /// </summary>
    private static bool TryParseLocation(string line, out (int col, int row) location)
    {
        location = default;
        var parts = line.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 2
            && int.TryParse(parts[0], out var col)
            && int.TryParse(parts[1], out var row))
        {
            location = (col, row);
            return true;
        }
        return false;
    }

    private enum EnvelopeParseState
    {
        Header,
        LocationOrContent,
        Content,
    }
}

public enum CommandType
{
    Envelope,
    Scroll,
}

public class ParsedCommand
{
    public CommandType Type { get; init; }
    public string? TargetCommanderName { get; init; }
    public int? TargetLocationCol { get; init; }
    public int? TargetLocationRow { get; init; }
    public string Content { get; init; } = string.Empty;
}
