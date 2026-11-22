using Genelife.Api.Hosting;
using Microsoft.Extensions.Configuration;

namespace Genelife.UnitTests.Hosting;

public sealed class DotEnvTests
{
    [Fact]
    public void Load_maps_dotenv_keys_to_configuration()
    {
        var directory = Directory.CreateTempSubdirectory();

        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, ".env"), """
                # Local development values
                Auth__Jwt__AdminSecret="quoted-secret"
                export Hosting__Mode=DedicatedServer
                """);

            var configuration = new ConfigurationManager();
            DotEnv.Load(configuration, directory.FullName);

            Assert.Equal("quoted-secret", configuration["Auth:Jwt:AdminSecret"]);
            Assert.Equal("DedicatedServer", configuration["Hosting:Mode"]);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Load_does_not_override_existing_configuration()
    {
        var directory = Directory.CreateTempSubdirectory();

        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, ".env"),
                "Auth__Jwt__AdminSecret=file-secret");

            var configuration = new ConfigurationManager
            {
                ["Auth:Jwt:AdminSecret"] = "existing-secret"
            };

            DotEnv.Load(configuration, directory.FullName);

            Assert.Equal("existing-secret", configuration["Auth:Jwt:AdminSecret"]);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
