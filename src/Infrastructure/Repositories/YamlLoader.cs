namespace Videoland4000.Infrastructure.Repositories;

public static class YamlLoader
{
    public static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();
}
