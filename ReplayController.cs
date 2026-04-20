using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ChatGalvanometer
{
    public static class ReplayController
    {
        public static async Task Play(
            string filePath,
            string? startTimeStr,
            Action<MessageNotification, DateTimeOffset> onMessage,
            CancellationToken ct)
        {
            var entries = ParseCsv(filePath);
            Trace.WriteLine($"Replay: parsed {entries.Count} entries from {filePath}");
            if (entries.Count == 0)
                throw new InvalidOperationException($"No valid entries could be parsed from: {filePath}");

            int startIndex = FindStartIndex(entries, startTimeStr);
            Trace.WriteLine($"Replay: starting at index {startIndex} (timestamp: {entries[startIndex].Timestamp:O})");

            DateTimeOffset? prevTime = null;
            for (int i = startIndex; i < entries.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                if (prevTime.HasValue)
                {
                    var gap = entries[i].Timestamp - prevTime.Value;
                    if (gap > TimeSpan.Zero)
                    {
                        // Cap gaps at 30s so slow chat periods don't stall the replay
                        var delay = gap < TimeSpan.FromSeconds(30) ? gap : TimeSpan.FromSeconds(30);
                        await Task.Delay(delay, ct);
                    }
                }

                onMessage(entries[i].Message, entries[i].Timestamp);
                prevTime = entries[i].Timestamp;
            }
        }

        private static int FindStartIndex(
            List<(MessageNotification Message, DateTimeOffset Timestamp)> entries,
            string? startTimeStr)
        {
            if (string.IsNullOrWhiteSpace(startTimeStr)) return 0;

            if (!DateTime.TryParse(startTimeStr, null,
                DateTimeStyles.AllowWhiteSpaces, out DateTime startDt))
            {
                Trace.WriteLine($"Replay: failed to parse start time '{startTimeStr}'");
                return 0;
            }

            // Treat the entered components as UTC directly — avoids AssumeUniversal
            // ambiguity where .NET may silently apply a local offset
            var startTime = new DateTimeOffset(
                startDt.Year, startDt.Month, startDt.Day,
                startDt.Hour, startDt.Minute, startDt.Second,
                TimeSpan.Zero);

            Trace.WriteLine($"Replay: seeking to {startTime:O}, file spans {entries[0].Timestamp:O} – {entries[^1].Timestamp:O}");

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Timestamp >= startTime)
                {
                    Trace.WriteLine($"Replay: start index {i}, timestamp {entries[i].Timestamp:O}");
                    return i;
                }
            }

            Trace.WriteLine($"Replay: start time is after all entries, starting from beginning");
            return 0;
        }

        private static List<(MessageNotification Message, DateTimeOffset Timestamp)> ParseCsv(string filePath)
        {
            var result = new List<(MessageNotification, DateTimeOffset)>();
            foreach (var line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Format: "message text",<timestamp>
                // Split on the last comma — timestamps never contain commas
                int lastComma = line.LastIndexOf(',');
                if (lastComma < 2) continue;

                string timestampStr = line[(lastComma + 1)..].Trim();
                string quotedText = line[..lastComma];
                if (quotedText.Length < 2 || quotedText[0] != '"' || quotedText[^1] != '"') continue;
                string text = quotedText[1..^1].Replace("\"\"", "\"");

                if (!TryParseTimestamp(timestampStr, out DateTimeOffset timestamp)) continue;

                result.Add((new MessageNotification(text, timestampStr), timestamp));
            }
            return result;
        }

        private static bool TryParseTimestamp(string timestampStr, out DateTimeOffset result)
        {
            // Twitch ISO 8601 timestamps have nanosecond precision (9 fractional digits);
            // DateTimeOffset only supports up to 7, so truncate before parsing
            string truncated = TruncateFractionalSeconds(timestampStr);

            // Try ISO 8601 / round-trip format first (covers the common Twitch timestamp)
            if (DateTimeOffset.TryParse(truncated, null,
                DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal, out result))
                return true;

            // Fall back to locale-aware parse for any other format written to the log
            // (e.g. Windows local-time format "4/19/2026 6:32:57 PM")
            if (DateTimeOffset.TryParse(truncated, null,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out result))
                return true;

            return false;
        }

        private static string TruncateFractionalSeconds(string timestamp)
        {
            int dotIndex = timestamp.IndexOf('.');
            if (dotIndex < 0) return timestamp;
            int endIndex = timestamp.IndexOfAny(['Z', '+', '-'], dotIndex + 1);
            if (endIndex < 0) endIndex = timestamp.Length;
            if (endIndex - dotIndex - 1 > 7)
                timestamp = timestamp[..(dotIndex + 8)] + timestamp[endIndex..];
            return timestamp;
        }
    }
}
