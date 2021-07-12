using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UnityLogConverter
{
    enum Severity
    {
        Unknown = -1,
        Info = 0,
        Warning = 1,
        Error = 2,
        Exception = 3,
    }

    class Record
    {
        static Regex rxExceptionLocation = new Regex(@"in\ ([^<>]*):(\d+)", RegexOptions.Compiled);

        public Severity severity = Severity.Unknown;
        public List<string> message = new List<string>();
        public List<string> stacktrace = new List<string>();
        public string filename;
        public int line = -1;
        public int sourceLine = -1;

        public void SetParameters(SqliteParameter paramMessage, SqliteParameter paramSeverity, SqliteParameter paramFilename, SqliteParameter paramLine, SqliteParameter paramStacktrace, SqliteParameter paramSourceLine)
        {
            paramMessage.Value = string.Join('\n', message);
            paramSeverity.Value = (int)severity;

            var dbFilename = filename;
            var dbLine = line;
            if (severity == Severity.Exception)
            {
                Match match;
                foreach (var line in stacktrace)
                {
                    if ((match = rxExceptionLocation.Match(line)).Success)
                    {
                        dbFilename = match.Groups[1].Value;
                        dbLine = System.Convert.ToInt32(match.Groups[2].Value);
                        break;
                    }
                }
            }
            paramFilename.Value = dbFilename;
            paramLine.Value = dbLine;
            paramStacktrace.Value = string.Join('\n', stacktrace);
            paramSourceLine.Value = sourceLine;
        }
    }
}