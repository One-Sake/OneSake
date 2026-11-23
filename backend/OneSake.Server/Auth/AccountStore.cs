using Microsoft.EntityFrameworkCore;
using OneSake.Persistence;
using OneSake.Persistence.Entities;

namespace OneSake.Server.Auth;

// Storage of accounts. An interface so AccountService can be tested without a database.
public interface IAccountStore
{
    Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Account?> FindByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default);
    Task<bool> IsPlayerIdTakenAsync(string playerId, CancellationToken cancellationToken = default);

    // False when the username or the player ID already exists (unique index).
    Task<bool> AddAsync(Account account, CancellationToken cancellationToken = default);
    Task SaveAsync(Account account, CancellationToken cancellationToken = default);
}

public sealed class EfAccountStore : IAccountStore
{
    private readonly OneSakeDbContext _dbContext;

    public EfAccountStore(OneSakeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Accounts.FirstOrDefaultAsync(account => account.Id == id, cancellationToken);
    }

    public Task<Account?> FindByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default)
    {
        return _dbContext.Accounts.FirstOrDefaultAsync(account => account.NormalizedUsername == normalizedUsername, cancellationToken);
    }

    public Task<bool> IsPlayerIdTakenAsync(string playerId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Accounts.AsNoTracking().AnyAsync(account => account.PlayerId == playerId, cancellationToken);
    }

    public async Task<bool> AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        _dbContext.Accounts.Add(account);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // Two registrations with the same name at the same time: the unique index decides.
            _dbContext.Entry(account).State = EntityState.Detached;
            return false;
        }
    }

    public Task SaveAsync(Account account, CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
