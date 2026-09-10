using System;
using System.IO;

namespace Simitone.Client
{
    /// <summary>
    /// Lightweight runtime state logger for the native port: records screen transitions and
    /// content-load failures to <UserDir>/game.log so gameplay can be verified headlessly.
    /// </summary>
    public static class GameLog
    {
        private static readonly object Lock = new object();

        public static void Write(string message)
        {
            var line = DateTime.Now.ToString("HH:mm:ss.fff") + " " + message;
            // Mirror to stdout FIRST on its own: under restricted sandboxes the user-doc
            // write can be denied and swallowed, so the console must not depend on it.
            try { Console.WriteLine(line); Console.Out.Flush(); } catch { }
            try
            {
                var dir = FSO.Common.FSOEnvironment.UserDir;
                if (string.IsNullOrEmpty(dir)) dir = AppDomain.CurrentDomain.BaseDirectory;
                Directory.CreateDirectory(dir);
                var file = Path.Combine(dir, "game.log");
                lock (Lock) File.AppendAllText(file, line + Environment.NewLine);
            }
            catch
            {
                // never let logging break gameplay; console mirror already printed
            }
        }
    }
}
