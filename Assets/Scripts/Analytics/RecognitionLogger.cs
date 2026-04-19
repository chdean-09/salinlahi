using System;
using System.IO;
using UnityEngine;

/// Logs every recognition attempt to a CSV file for research
/// analysis. Data feeds into the confusion matrix tool (SALIN-63).
public static class RecognitionLogger
{
    public static bool LoggingEnabled { get; set; } = true;
    private const string FILE_NAME = "recognition_log.csv";

    private static readonly string CSV_HEADER =
        "timestamp,recognizedCharacterID,confidence,"
        + "secondBestCharacterID,secondBestConfidence,"
        + "scoreGap,intendedCharacterID";

    private static string FilePath =>
        Path.Combine(
            Application.persistentDataPath, FILE_NAME);

    // Tracks whether we have written the header this session.
    // If the file already exists on disk, we skip the header.
    private static bool _headerWritten;

    /// Called by RecognitionManager after every recognition
    /// attempt, regardless of whether it passed the confidence
    /// threshold. This ensures failed attempts are also logged.
    public static void LogAttempt(
        RecognitionResult result,
        string intendedCharacterID = "")
    {
        if (!LoggingEnabled) return;

        try
        {
            EnsureHeader();

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
                + $"{intendedCharacterID}";

            File.AppendAllText(FilePath, line + "\n");

            DebugLogger.Log(
                $"RecognitionLogger: Wrote entry for "
                + $"{result.characterID}");
        }
        catch (Exception ex)
        {
            DebugLogger.LogWarning(
                $"RecognitionLogger: Write failed: "
                + $"{ex.Message}");
        }
    }

    /// Copies the CSV to a user-accessible location on Android.
    /// On editor, copies to the project root for convenience.
    public static string ExportLog()
    {
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
        if (!File.Exists(FilePath)) return 0;
        string[] lines = File.ReadAllLines(FilePath);
        // Subtract 1 for the header row
        return Mathf.Max(0, lines.Length - 1);
    }

    /// Clears the log file. Used at the start of a structured
    /// test session so the CSV only contains test data.
    public static void ClearLog()
    {
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
        }

        _headerWritten = true;
    }
}
