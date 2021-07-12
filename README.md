# Unity Log Converter

Unity produces log files that are not really machine-readable. It uses for example different formatting for similar things and makes it difficult to extract information like filename or linenumber.

This tool can convert Unity logs to an SQLite database.

## Usage

The tool needs to be run from the command line:

    UnityLogConverter.exe Player.log output.sqlite

## Development

This tool is written as a .NET Core 3.1 application, so you need at least this version of dotnet. You can get it here: https://dotnet.microsoft.com/download

1. Download and install the SDK
1. From inside this directory, run `dotnet build -c Release`
