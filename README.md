# Unity Log Converter

Unity produces log files that are not really machine-readable. It uses for example different formatting for similar things and makes it difficult to extract information like filename or linenumber.

This tool can convert Unity logs to an SQLite database.

## Usage

The tool needs to be run from the command line:

    UnityLogConverter.exe Player.log output.sqlite

This will create an SQLite database file named `output.sqlite`.

## Example Queries

Here are some example queries that could be done with the generated database:

``` sql
-- select unique messages
SELECT *, count(*) AS occurences
FROM entries
GROUP BY coalesce(filename, ROWID), line
ORDER BY source_line;

-- how many messages of which type occured?
SELECT severity, count(*) AS occurences
FROM entries
GROUP BY severity
ORDER BY severity;

-- show only fatal error messages from latest to first
SELECT *
FROM entries
WHERE severity >= 2
ORDER BY source_line DESC;

-- search for messages that contain a certain string
SELECT *
FROM entries
WHERE message like "%kinematic body%"
ORDER BY source_line;
```

## Development

This tool is written as a .NET Core 3.1 application, so you need at least this version of dotnet. You can get it here: https://dotnet.microsoft.com/download

1. Download and install the SDK
1. From inside this directory, run `dotnet build -c Release`

To build a self-contained executable, run

    dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true

The binary will be located in `bin/Release/netcoreapp3.1/win-x64/publish`.

## Known issues/TODO

+ Not tested on other architectures, but in the build process it should work if you replace `win-x64` with your architecture
+ Not all warnings are correctly parsed
+ The first wall of text is not handled yet and included in the first log message
+ Support multiple log sessions?
