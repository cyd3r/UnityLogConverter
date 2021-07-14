using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace UnityLogConverter
{
    class Program
    {
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
                CREATE TABLE entries (
                    message TEXT NOT NULL,
                    severity INTEGER,
                    filename TEXT,
                    line INTEGER,
                    stacktrace TEXT,
                    source_line INTEGER NOT NULL
                )
                ";
                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch (SqliteException)
                {
                    Console.WriteLine("Could not create table. Maybe the database already exists?");
                    return;
                }

                using (var logFile = new StreamReader(inputPath))
                {
                    string lastLine, line;
                    var records = new List<Record>();

                    using (var transaction = conn.BeginTransaction())
                    {
                        var cmdInsert = conn.CreateCommand();
                        cmdInsert.CommandText = @"
                        INSERT INTO entries (message, severity, filename, line, stacktrace, source_line)
                        VALUES ($message, $severity, $filename, $line, $stacktrace, $source)
                        ";
                        var paramMsg = cmdInsert.Parameters.Add("message", SqliteType.Text);
                        var paramSeverity = cmdInsert.Parameters.Add("severity", SqliteType.Integer);
                        var paramFilename = cmdInsert.Parameters.Add("filename", SqliteType.Text);
                        var paramLine = cmdInsert.Parameters.Add("line", SqliteType.Integer);
                        var paramStacktrace = cmdInsert.Parameters.Add("stacktrace", SqliteType.Text);
                        var paramSourceLine = cmdInsert.Parameters.Add("source", SqliteType.Integer);

                        bool containsUnknownSeverity = false;
                        int logLineNumber = 0;
                        Record record = new Record(0);
                        lastLine = logFile.ReadLine();
                        while ((line = logFile.ReadLine()) != null)
                        {
                            record.ReadLine(lastLine, line);
                            if (record.isComplete)
                            {
                                // severity
                                paramMsg.Value = record.messageStr;
                                if (record.severity == Severity.Unknown)
                                {
                                    paramSeverity.Value = DBNull.Value;
                                    containsUnknownSeverity = true;
                                }
                                else
                                {
                                    paramSeverity.Value = (int)record.severity;
                                }
                                // location (filename & linenumber)
                                if (record.origin.HasValue)
                                {
                                    paramFilename.Value = record.origin.Value.filename;
                                    paramLine.Value = record.origin.Value.line;
                                }
                                else
                                {
                                    paramFilename.Value = DBNull.Value;
                                    paramLine.Value = DBNull.Value;
                                }
                                // stacktrace
                                if (record.stacktrace == null)
                                    paramStacktrace.Value = DBNull.Value;
                                else
                                    paramStacktrace.Value = record.stacktrace;
                                // source line
                                paramSourceLine.Value = record.sourceLine;

                                cmdInsert.ExecuteNonQuery();

                                record = new Record(logLineNumber);
                            }
                            lastLine = line;
                            logLineNumber++;
                        }
                        transaction.Commit();

                        if (containsUnknownSeverity)
                        {
                            Console.WriteLine("WARNING: Could not determine severity for at least one message!");
                        }
                    }
                }
            }
        }
    }
}
