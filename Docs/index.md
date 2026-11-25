# Stellar.LoggingSystem

logging system of the engine

A `CoreModule` module of [StellarEngine](https://quantumdevteam.github.io/StellarEngine-Mono/). Its site is https://quantumdevteam.github.io/Stellar.LoggingSystem/, built from the Markdown
files of `Docs/` with DocFX; the whole engine, with every module, is on the [site of the engine](https://quantumdevteam.github.io/StellarEngine-Mono/).

## Using it

Add `Stellar.LoggingSystem` to `Dependencies` of your module or project descriptor, then:

```powershell
stellar modules sync
```

## Settings

In the `Runtime` section of `.stellar.project`, or on the command line (`--stellar:LogPath=logs/game.log`), which wins.
Names in any case. A value that cannot be read is told in the log and left at its default.

| Setting | Default | What it is |
|---|---|---|
| `LogPath` | none, no file | Where the log is also written. A path of the disk, relative to the folder of the application (`logs/game.log`), needs nothing else. `domain://path` goes through the file system of the engine: the lines wait (up to 1024) till somebody publishes `IFileSystem` and mounts the domain |
| `LogLevel` | `Debug` | The least serious level of the console |
| `LogFileLevel` | `Info` | The least serious level of the file |

The file is appended to, and can be read while the engine runs.

## A logger of your own

Derive from `LoggerBase` (or implement `ILogger`, with `ILabeled` for a name), add it to `ILoggerCatalog` when your
module starts and take it out when it stops. The catalog never disposes what it did not make.

## Working on it

```powershell
stellar modules build Stellar.LoggingSystem --tests
```

See [the README](../README.md) for the layout of the module.
