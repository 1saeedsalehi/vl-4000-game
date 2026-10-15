namespace Videoland4000.Model.Manifest;

public interface IGameManifestRepository
{
    Task<IReadOnlyCollection<GameManifest>> LoadGameManifestsAsync();

    Task<GameManifest?> TryLoadGameManifestAsync(int diskNumber);
}
