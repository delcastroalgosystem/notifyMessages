using Microsoft.EntityFrameworkCore;
using NotifyMessages.Domain.Entities;

namespace NotifyMessages.Application.Interfaces;

public interface IAppDbContext
{
    DbSet<MessageDispatch> MessageDispatches { get; }
    DbSet<MessageDispatchEvent> MessageDispatchEvents { get; }
    DbSet<Template> Templates { get; }
    DbSet<TenantProviderConfig> TenantProviderConfigs { get; }
    DbSet<Tenant> Tenants { get; }
    DbSet<MessageTrigger> MessageTriggers { get; }
    DbSet<DispatchBatch> DispatchBatches { get; }
    DbSet<Suppression> Suppressions { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
