using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// Logs every recognition attempt to a CSV file for research
/// analysis. Data feeds into the confusion matrix tool (SALIN-63).
public static class RecognitionLogger
{
    private const string FILE_NAME = "recognition_log.csv";
    private const int FlushThreshold = 10;

    private static readonly string CSV_HEADER =
        "timestamp,recognizedCharacterID,confidence,"
        + "secondBestCharacterID,secondBestConfidence,"
        + "scoreGap,intendedCharacterID,outcome";

    private static readonly string LEGACY_CSV_HEADER =
        "timestamp,recognizedCharacterID,confidence,"
        + "secondBestCharacterID,secondBestConfidence,"
        + "scoreGap,intendedCharacterID";

    private static string FilePath =>
        Path.Combine(
            Application.persistentDataPath, FILE_NAME);

    // Tracks whether we have validated/migrated the header this session.
    private static bool _headerWritten;

    private static readonly List<string> _buffer
        = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterQuitHandler()
    {
        Application.quitting += Flush;
    }

    /// Called by RecognitionManager after every recognition
    /// attempt, regardless of whether it passed the confidence
    /// threshold. This ensures failed attempts are also logged.
    public static void LogAttempt(
        RecognitionResult result,
        string intendedCharacterID = "",
        string outcome = "attempt")
    {
        try
        {
            string timestamp = DateTime.Now.ToString(
                "yyyy-MM-dd HH:mm:ss.fff");

            float secondConf = result.secondBestScore < 0f
                ? 0f : result.secondBestScore;
            float gap = result.score - secondConf;

            string secondID = result.secondBestID == "NONE"
                ? "" : result.secondBestID;

            string line =
                $"{timestamp},"
                + $"{result.characterID},"
                + $"{result.score:F4},"
                + $"{secondID},"
                + $"{secondConf:F4},"
                + $"{gap:F4},"
                + $"{intendedCharacterID},"
                + $"{outcome}";

            _buffer.Add(line);
            if (_buffer.Count >= FlushThreshold)
                Flush();
        }
        catch (Exception ex)
        {
            DebugLogger.LogWarning(
                $"RecognitionLogger: Buffer failed: "
                + $"{ex.Message}");
        }
    }

    public static void LogOutcome(
        string outcome,
        string recognizedCharacterID = "",
        string intendedCharacterID = "")
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string line =
                $"{timestamp},"
                + $"{recognizedCharacterID},"
                + "0.0000,"
                + ","
                + "0.0000,"
                + "0.0000,"
                + $"{intendedCharacterID},"
                + $"{outcome}";

            _buffer.Add(line);
            if (_buffer.Count >= FlushThreshold)
                Flush();
        }
        catch (Exception ex)
        {
            DebugLogger.LogWarning(
                $"RecognitionLogger: Outcome buffer failed: "
                + $"{ex.Message}");
        }
    }

    public static void Flush()
    {
        if (_buffer.Count == 0) return;

        try
        {
            EnsureHeader();
            string combined = string.Join("\n", _buffer) + "\n";
            File.AppendAllText(FilePath, combined);
            DebugLogger.Log($"RecognitionLogger: Flushed {_buffer.Count} entries.");
            _buffer.Clear();
        }
        catch (Exception ex)
        {
            DebugLogger.LogWarning($"RecognitionLogger: Flush failed: {ex.Message}");
        }
    }

    /// Copies the CSV to a user-accessible location on Android.
    /// On editor, copies to the project root for convenience.
    public static string ExportLog()
    {
        Flush();

        if (!File.Exists(FilePath))
        {
            DebugLogger.LogWarning(
                "RecognitionLogger: No log file to export.");
            return null;
        }

        string exportPath;

#if UNITY_ANDROID && !UNITY_EDITOR
        // Copy to Android Downloads folder via shared storage.
        exportPath = Path.Combine(
            "/storage/emulated/0/Download",
            "salinlahi_recognition_log.csv");
#else
        // Editor: copy to project root
        exportPath = Path.Combine(
            Application.dataPath, "..",
            "salinlahi_recognition_log.csv");
#endif

        try
        {
            File.Copy(FilePath, exportPath, overwrite: true);
            DebugLogger.Log(
                $"RecognitionLogger: Exported to "
                + $"{exportPath}");
            return exportPath;
        }
        catch (Exception ex)
        {
            DebugLogger.LogWarning(
                $"RecognitionLogger: Export failed: "
                + $"{ex.Message}");
            return null;
        }
    }

    /// Returns the total number of log entries (excluding header).
    public static int GetEntryCount()
    {
        int diskCount = 0;
        if (File.Exists(FilePath))
        {
            string[] lines = File.ReadAllLines(FilePath);
            // Subtract 1 for the header row
            diskCount = Mathf.Max(0, lines.Length - 1);
        }

        return diskCount + _buffer.Count;
    }

    /// Clears the log file. Used at the start of a structured
    /// test session so the CSV only contains test data.
    public static void ClearLog()
    {
        _buffer.Clear();
        if (File.Exists(FilePath))
            File.Delete(FilePath);

        _headerWritten = false;
        DebugLogger.Log(
            "RecognitionLogger: Log cleared.");
    }

    private static void EnsureHeader()
    {
        if (_headerWritten) return;

        if (!File.Exists(FilePath))
        {
            File.WriteAllText(
                FilePath, CSV_HEADER + "\n");
            _headerWritten = true;
            return;
        }

        string[] lines = File.ReadAllLines(FilePath);
        if (lines.Length > 0 && lines[0] == LEGACY_CSV_HEADER)
        {
            lines[0] = CSV_HEADER;
            for (int i = 1; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                    lines[i] += ",legacy";
            }

            File.WriteAllLines(FilePath, lines);
        }

        _headerWritten = true;
    }
}
