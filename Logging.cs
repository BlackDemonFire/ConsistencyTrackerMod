using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Celeste.Mod.ConsistencyTracker;

internal class Logging {
    private const int LogFileCount = 10;
    public static bool Initialized { get; private set; }
    private static FileStream LogFile { get; set; }
    private static StreamWriter Writer { get; set; }
    private static readonly string Path = ConsistencyTrackerModule.GetPathToFile("logs/log.txt");
    public static void LogInit() {
        if (Initialized)
            throw new Exception("The logger can only be initialized once.");
        Initialized = true;
        var logFileMax = ConsistencyTrackerModule.GetPathToFile($"logs/log_old{LogFileCount}.txt");
        if (File.Exists(logFileMax)) {
            File.Delete(logFileMax);
        }

        for (var i = LogFileCount - 1; i >= 1; i--) {
            var logFilePath = ConsistencyTrackerModule.GetPathToFile($"logs/log_old{i}.txt");
            if (!File.Exists(logFilePath))
                continue;
            var logFileNewPath = ConsistencyTrackerModule.GetPathToFile($"logs/log_old{i + 1}.txt");
            File.Move(logFilePath, logFileNewPath);
        }

        var lastFile = ConsistencyTrackerModule.GetPathToFile("logs/log.txt");
        if (File.Exists(lastFile)) {
            var logFileNewPath = ConsistencyTrackerModule.GetPathToFile("logs/log_old1.txt");
            File.Move(lastFile, logFileNewPath);
        }

        LogFile = new FileStream(Path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096);
        Writer = new StreamWriter(LogFile, Encoding.UTF8, 4096);

        LogToFile("~~~===============~~~\n");
    }
    public static void Log(string log, [CallerMemberName] string callerName = "", LogLevel logLevel = LogLevel.Info) {
        if (logLevel < ConsistencyTrackerModule.Instance.ModSettings.LogLevel)
            return;
        if (ConsistencyTrackerModule.Instance.ModSettings.LogType == LogType.Everest) {
            Logger.Log(logLevel, nameof(ConsistencyTracker), $"[{callerName}] {log}");
        }
        if (!Initialized) { return; }
        LogToFile($"[{callerName}] {log} \n");
    }
    private static void LogToFile(string toLog) {
        Writer.Write(toLog);
    }
    public static void Unload() {
        Writer.Close();
        LogFile.Dispose();
    }
}
public enum LogType {
    ConsistencyTracker,
    Everest
}