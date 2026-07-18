using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RealTimeChatAPI.Exceptions;
using RealTimeChatAPI.Models;

namespace RealTimeChatAPI.Data.Repositories;

internal class UsersRepository(RealTimeChatDbContext dbContext) : IUsersRepository
{
    public async Task Add(User user)
    {
        dbContext.Users.Add(user);

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            dbContext.Entry(user).State = EntityState.Detached;
            throw new UsernameAlreadyUsedException(user.Username);
        }
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await dbContext.Users.SingleOrDefaultAsync(x => x.Id == id);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        var normalizedUsername = username.ToLowerInvariant();
        return await dbContext.Users.SingleOrDefaultAsync(u => u.Username == normalizedUsername);
    }

    public async Task UpdateAsync(User user)
    {
        dbContext.Users.Update(user);

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            dbContext.Entry(user).State = EntityState.Unchanged;
            throw new UsernameAlreadyUsedException(user.Username);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
