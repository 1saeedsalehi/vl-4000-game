namespace Videoland4000.Model.Levels;

public interface ILevelRepository
{
    Task<LevelSpec> LoadLevelAsync(GameManifest manifest, string relativePath);
}
