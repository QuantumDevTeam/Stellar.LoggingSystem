namespace Stellar.LoggingSystem.Tests;

public class LoggingSystemModuleTests
{
    [Fact]
    public void TheAssemblyIsTheModule() =>
        typeof(LoggingSystemModule).Assembly.GetName().Name.Should().Be(LoggingSystemModule.Id);
}
