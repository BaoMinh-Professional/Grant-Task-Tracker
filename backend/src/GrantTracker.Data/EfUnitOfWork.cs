using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Common.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GrantTracker.Data;

public sealed class EfUnitOfWork(GrantTrackerDbContext dbContext) : IUnitOfWork
{
    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        new EfUnitOfWorkTransaction(await dbContext.Database.BeginTransactionAsync(cancellationToken));

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new UniqueConstraintViolationException("A unique constraint was violated.", exception);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new VersionConflictException();
        }
    }

    public void DiscardChanges() => dbContext.ChangeTracker.Clear();

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteErrorCode: 19, SqliteExtendedErrorCode: 2067 or 1555 };
}

public sealed class EfUnitOfWorkTransaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
{
    private bool committed;
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await transaction.CommitAsync(cancellationToken);
        committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!committed) await transaction.RollbackAsync();
        await transaction.DisposeAsync();
    }
}
