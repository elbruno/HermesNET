namespace Hermes.Core.Session;

public interface ISessionStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<SessionEntity> CreateAsync(string profileId, string? message = null, CancellationToken cancellationToken = default);
    Task<SessionEntity?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<SessionEntity> UpdateAsync(string id, string lastMessage, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SessionEntity>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default);
}
