// KO: UTC 이벤트 로그는 용량에 따라 분할됩니다. 공개 저장소나 배포물에는 개인 실행 로그를 넣지 않습니다.
// EN: UTC event logs rotate by size. Do not ship personal runtime logs in source or release packages.
using System;
using System.IO;
using System.Text;
using Colossal.Logging;

namespace CopyIt
{
    internal static class Diagnostics
    {
        private static readonly object Gate = new object();
        private static readonly ILog Log = LogManager.GetLogger("CopyIt").SetShowsErrorsInUI(false);
        private static StreamWriter writer;
        private static int part;
        private static string directory, session;
        internal static string PathName { get; private set; }
        internal static void Start()
        {
            directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)+"Low", "Colossal Order", "Cities Skylines II", "Logs", "CopyIt");
            session = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            try { Directory.CreateDirectory(directory); Open(); } catch (Exception e) { Log.Error(e,"Cannot open Copy It event log"); }
            Event("session.start", "Copy It 0.5.6");
        }
        private static void Open()
        {
            PathName = Path.Combine(directory, session + "-" + part++ + ".log");
            writer = new StreamWriter(new FileStream(PathName, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false)) { AutoFlush = true };
        }
        internal static void Event(string action, string details = "")
        {
            string line = DateTime.UtcNow.ToString("O") + " | " + action + " | " + details.Replace("\r", "\\r").Replace("\n", "\\n");
            lock (Gate)
            {
                try { if (writer != null && writer.BaseStream.Length > 8*1024*1024) { writer.Dispose(); Open(); } writer?.WriteLine(line); }
                catch (Exception e) { writer = null; Log.Error(e,"Copy It file logging failed"); }
            }
            Log.Info(line);
        }
        internal static void Failure(string action, Exception error) => Event(action, error.ToString());
        internal static void Stop() { Event("session.end"); lock(Gate) { writer?.Dispose(); writer=null; } }
    }
}






