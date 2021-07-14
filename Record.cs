using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UnityLogConverter
{
    enum ParseProgress
    {
        Message,
        Stacktrace,
        BlankAfterStacktrace,
        Location,
        Done,
    }

    enum SpecialMessageType
    {
        None,
        LogHead,
        UnloadingSerialized,
        SystemMemory,
        UnloadingAssets,
    }

    enum Severity
    {
        Unknown = -1,
        Info = 0,
        Warning = 1,
        Error = 2,
        Exception = 3,
    }

    struct CodeLocation
    {
        public string filename;
        public int line;
    }

    class Record
    {
        static readonly Regex rxExceptionLocation = new Regex(@"in\ ([^<>]*):(\d+)", RegexOptions.Compiled);
        static readonly Regex rxLocation = new Regex(@"\(Filename:\ (.*)\ Line:\ (\d+)\)", RegexOptions.Compiled);
        static readonly Regex rxStacktrace = new Regex(@"^[^\.:\ ]+(?:\.[^\.:\ ]+)+:.*$|^\ \ at\ .*$", RegexOptions.Compiled);

        public Severity severity { get; private set; } = Severity.Unknown;
        List<string> messages = new List<string>();
        List<string> stacktraceLines = new List<string>();

        CodeLocation? _origin;
        public CodeLocation? origin
        {
            get
            {
                if (severity == Severity.Exception)
                {
                    Match match;
                    foreach (var line in stacktraceLines)
                    {
                        if ((match = rxExceptionLocation.Match(line)).Success)
                        {
                            return new CodeLocation()
                            {
                                filename = match.Groups[1].Value,
                                line = Convert.ToInt32(match.Groups[2].Value),
                            };
                        }
                    }
                }
                return _origin;
            }
        }

        public readonly int sourceLine;
        public string stacktrace { get { return stacktraceLines.Count == 0 ? null : string.Join('\n', stacktraceLines); } }
        public string messageStr { get { return string.Join('\n', messages); } }

        public bool isComplete
        {
            get
            {
                return progress == ParseProgress.Done;
            }
        }

        SpecialMessageType specialMsgType = SpecialMessageType.None;
        ParseProgress progress = ParseProgress.Message;

        public Record(int sourceLine)
        {
            this.sourceLine = sourceLine;
        }

        public void ReadLine(string line, string nextLine)
        {
            // possible state transitions
            // message -> complete
            // message -> stacktrace
            // stacktrace -> location
            // location -> complete

            if (isComplete)
            {
                throw new InvalidOperationException("Record is already complete");
            }

            Match match;

            switch (progress)
            {
                case ParseProgress.Message:
                    // change to stacktrace?
                    if (rxStacktrace.IsMatch(line))
                    {
                        progress = ParseProgress.Stacktrace;
                    }
                    break;

                case ParseProgress.Stacktrace:
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        progress = ParseProgress.BlankAfterStacktrace;
                    }
                    break;

                case ParseProgress.BlankAfterStacktrace:
                    progress = ParseProgress.Location;
                    break;
            }

            switch (progress)
            {
                case ParseProgress.Message:
                    if (messages.Count > 0 || !string.IsNullOrEmpty(line))
                    {
                        if (messages.Count == 0 && specialMsgType == SpecialMessageType.None)
                        {
                            // potential special message
                            if (line.StartsWith("Mono path["))
                            {
                                specialMsgType = SpecialMessageType.LogHead;
                                severity = Severity.Info;
                            }
                            else if (line.Contains("Unused Serialized"))
                            {
                                specialMsgType = SpecialMessageType.UnloadingSerialized;
                                severity = Severity.Info;
                            }
                            else if (line.StartsWith("System memory in use before: "))
                            {
                                specialMsgType = SpecialMessageType.SystemMemory;
                                severity = Severity.Info;
                            }
                            else if (line.Contains("unused Assets to reduce"))
                            {
                                specialMsgType = SpecialMessageType.UnloadingAssets;
                                severity = Severity.Info;
                            }
                        }
                        messages.Add(line);
                    }

                    switch (specialMsgType)
                    {
                        case SpecialMessageType.UnloadingSerialized:
                            if (line.StartsWith("UnloadTime: "))
                            {
                                progress = ParseProgress.Done;
                            }
                            break;
                        case SpecialMessageType.SystemMemory:
                            if (line.StartsWith("System memory in use after: "))
                            {
                                progress = ParseProgress.Done;
                            }
                            break;
                        case SpecialMessageType.UnloadingAssets:
                            if (line.StartsWith("Total: "))
                            {
                                progress = ParseProgress.Done;
                            }
                            break;
                        case SpecialMessageType.LogHead:
                            if (line.StartsWith("  Thread -> id: ") && !nextLine.StartsWith("  Thread -> id: "))
                            {
                                progress = ParseProgress.Done;
                            }
                            break;
                    }
                    break;

                case ParseProgress.Stacktrace:
                    if (line.StartsWith("  at"))
                    {
                        severity = Severity.Exception;
                        line = line.Substring(5);
                    }
                    else
                    {
                        if (line.StartsWith("UnityEngine.Debug:Log("))
                            severity = Severity.Info;
                        else if (line.StartsWith("UnityEngine.Debug:LogWarning"))
                            severity = Severity.Warning;
                        else if (line.StartsWith("UnityEngine.Debug:LogError"))
                            severity = Severity.Warning;
                    }
                    stacktraceLines.Add(line);
                    break;

                case ParseProgress.Location:
                    match = rxLocation.Match(line);
                    if (match.Success)
                    {
                        this._origin = new CodeLocation()
                        {
                            filename = match.Groups[1].Value,
                            line = Convert.ToInt32(match.Groups[2].Value),
                        };
                        progress = ParseProgress.Done;
                    }
                    else if (line[0] == '[')
                    {
                        // these are warning messages that are caused by C++ code, e.g.
                        // [C:\buildslave\unity\build\Modules/Physics/Rigidbody.cpp line 748]
                        this.severity = Severity.Warning;
                    }
                    break;
            }
        }
    }
}