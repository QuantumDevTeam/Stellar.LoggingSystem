using Stellar.Kernel.Configuration;
using Stellar.Kernel.LoggingSystem;

namespace Stellar.LoggingSystem.Tests;

public class LoggingSettingsTests
{
    private static readonly string Base = Path.Combine(Path.GetTempPath(), "game");

    private static LoggingSettings Read(params (string Name, string Value)[] settings)
    {
        return new LoggingSettings(
            new RuntimeConfiguration { Settings = settings.ToDictionary(it => it.Name, it => it.Value) }, Base);
    }

    [Fact]
    public void Nothing_IsTheDefault()
    {
        LoggingSettings settings = Read();

        settings.EnginePath.Should().BeNull();
        settings.DiskPath.Should().BeNull();
        settings.ConsoleLevel.Should().Be(LogLevel.Debug);
        settings.FileLevel.Should().Be(LogLevel.Info);
        settings.Problems.Should().BeEmpty();
    }

    [Theory]
    [InlineData("LogPath")]
    [InlineData("logPath")]
    [InlineData("LOGPATH")]
    public void Names_AreInAnyCase(string name)
    {
        Read((name, "user://logs/game.log")).EnginePath!.Value.ToString().Should().Be("user://logs/game.log");
    }

    [Fact]
    public void ARelativePath_StartsAtTheApplication()
    {
        LoggingSettings settings = Read(("LogPath", "logs/game.log"));

        settings.DiskPath.Should().Be(Path.GetFullPath("logs/game.log", Base));
        settings.EnginePath.Should().BeNull();
    }

    [Fact]
    public void AFullPath_IsTakenAsItIs()
    {
        string full = Path.GetFullPath("somewhere/game.log", Path.GetTempPath());
        Read(("LogPath", full)).DiskPath.Should().Be(full);
    }

    [Theory]
    [InlineData("://x")]
    [InlineData("user://")]
    [InlineData("user://../out.log")]
    [InlineData("logs/")]
    [InlineData("bad\0path")]
    public void ABadPath_IsAProblem_NotAFailure(string path)
    {
        LoggingSettings settings = Read(("LogPath", path));

        settings.EnginePath.Should().BeNull();
        settings.DiskPath.Should().BeNull();
        settings.Problems.Should().ContainSingle().Which.Should().StartWith("LogPath");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyPath_IsNone(string path)
    {
        LoggingSettings settings = Read(("LogPath", path));

        settings.DiskPath.Should().BeNull();
        settings.Problems.Should().BeEmpty();
    }

    [Fact]
    public void Levels_AreNames()
    {
        LoggingSettings settings = Read(("LogLevel", "warning"), ("LogFileLevel", "Error"));

        settings.ConsoleLevel.Should().Be(LogLevel.Warning);
        settings.FileLevel.Should().Be(LogLevel.Error);
        settings.Problems.Should().BeEmpty();
    }

    [Theory]
    [InlineData("loud")]
    [InlineData("3")]
    [InlineData("99")]
    [InlineData("Debug,Info")]
    public void ABadLevel_IsAProblem_AndTheDefault(string level)
    {
        LoggingSettings settings = Read(("LogLevel", level));

        settings.ConsoleLevel.Should().Be(LogLevel.Debug);
        settings.Problems.Should().ContainSingle().Which.Should().Contain("Warning");
    }
}
