namespace CashFlow.Consolidation.Infrastructure.Data;

using System;
using System.Threading;
using System.Threading.Tasks;
using CashFlow.Shared.Contracts.Security;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Seeder resiliente e idempotente para pré-cadastrar credenciais seguras na base Consolidation com BCrypt.
/// </summary>
public static class ApiClientSeeder
{
    public static async Task SeedAsync(ConsolidationDbContext context, ISecretHasher hasher, CancellationToken cancellationToken = default)
    {
        bool changed = false;

        foreach (var (clientId, secret, name, roles) in DefaultApiClients.All)
        {
            var existing = await context.ApiClients.FirstOrDefaultAsync(c => c.ClientId == clientId, cancellationToken);
            var (hash, salt) = hasher.HashSecret(secret);

            if (existing == null)
            {
                context.ApiClients.Add(new ApiClient
                {
                    Id = Guid.NewGuid(),
                    ClientId = clientId,
                    SecretHash = hash,
                    Salt = salt,
                    Name = name,
                    Roles = roles,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                changed = true;
            }
            else if (!hasher.VerifySecret(secret, existing.SecretHash, existing.Salt))
            {
                // Atualiza o hash para BCrypt e garante a nova senha padrão ("verity-secret")
                existing.SecretHash = hash;
                existing.Salt = salt;
                existing.Name = name;
                existing.Roles = roles;
                existing.IsActive = true;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                changed = true;
            }
        }

        if (changed)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
