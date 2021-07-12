using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace UnityLogConverter
{
    class Program
    {
        static Regex rxLocation = new Regex(@"\(Filename:\ (.*)\ Line:\ (\d+)\)", RegexOptions.Compiled);
        static Regex rxStacktrace = new Regex(@"^[^\.:\ ]+(?:\.[^\.:\ ]+)+:.*$|^\ \ at\ .*$", RegexOptions.Compiled);

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: UnityLogConverter log_file output_file");
                return;
            }

            string inputPath = args[0];
            string outputPath = args[1];

            var connString = new SqliteConnectionStringBuilder()
            {
                Mode = SqliteOpenMode.ReadWriteCreate,
                DataSource = outputPath,
            };

            using (var conn = new SqliteConnection(connString.ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                // maybe add stacktrace
                cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS entries (
                    message TEXT,
                    severity INTEGER,
                    filename TEXT,
                    lineNumber INTEGER,
                    stacktrace TEXT
                )
                ";
                cmd.ExecuteNonQuery();

                using (var logFile = new StreamReader(inputPath))
                {
                    string line;
                    Match match;
                    Record currentRecord = new Record();
                    var records = new List<Record>();

                    using (var transaction = conn.BeginTransaction())
                    {
                        var cmdInsert = conn.CreateCommand();
                        cmdInsert.CommandText = @"
                        INSERT INTO entries (message, severity, filename, lineNumber, stacktrace)
                        VALUES ($message, $severity, $filename, $lineNumber, $stacktrace)
                        ";
                        var paramMsg = cmdInsert.CreateParameter();
                        paramMsg.ParameterName = "message";
                        cmdInsert.Parameters.Add(paramMsg);
                        var paramSeverity = cmdInsert.CreateParameter();
                        paramSeverity.ParameterName = "severity";
                        cmdInsert.Parameters.Add(paramSeverity);
                        var paramFilename = cmdInsert.CreateParameter();
                        paramFilename.ParameterName = "filename";
                        cmdInsert.Parameters.Add(paramFilename);
                        var paramLineNumber = cmdInsert.CreateParameter();
                        paramLineNumber.ParameterName = "lineNumber";
                        cmdInsert.Parameters.Add(paramLineNumber);
                        var paramStacktrace = cmdInsert.CreateParameter();
                        paramStacktrace.ParameterName = "stacktrace";
                        cmdInsert.Parameters.Add(paramStacktrace);

                        while ((line = logFile.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line))
                            {
                            }
                            else if (rxStacktrace.Match(line).Success)
                            {
                                if (line.StartsWith("  at"))
                                {
                                    currentRecord.severity = Severity.Exception;
                                    // "  at ".Length == 5
                                    line = line.Substring(5);
                                }
                                else
                                {
                                    if (line.StartsWith("UnityEngine.Debug:Log("))
                                        currentRecord.severity = Severity.Info;
                                    else if (line.StartsWith("UnityEngine.Debug:LogWarning"))
                                        currentRecord.severity = Severity.Warning;
                                    else if (line.StartsWith("UnityEngine.Debug:LogError"))
                                        currentRecord.severity = Severity.Warning;
                                }
                                currentRecord.stacktrace.Add(line);
                            }
                            else if ((match = rxLocation.Match(line)).Success)
                            {
                                currentRecord.filename = match.Groups[1].Value;
                                currentRecord.line = Convert.ToInt32(match.Groups[2].Value);

                                currentRecord.SetParameters(paramMsg, paramSeverity, paramFilename, paramLineNumber, paramStacktrace);

                                cmdInsert.ExecuteNonQuery();

                                currentRecord = new Record();
                            }
                            else
                            {
                                currentRecord.message.Add(line);
                            }
                        }
                        transaction.Commit();
                    }
                }
            }
        }
    }
}
