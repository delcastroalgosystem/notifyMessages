using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Domain.Entities;

namespace NotifyMessages.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public const string Schema = "NotifyMsg";

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Histórico de migrações no schema NotifyMsg: numa BD partilhada (ex. SMA_EXTRATOOLS)
    /// não se mistura com o dbo.__EFMigrationsHistory da outra aplicação.
    /// </summary>
    public static void ConfigureSqlServer(SqlServerDbContextOptionsBuilder sqlServer)
        => sqlServer.MigrationsHistoryTable("__EFMigrationsHistory", Schema);

    public DbSet<MessageDispatch> MessageDispatches { get; set; }
    public DbSet<MessageDispatchEvent> MessageDispatchEvents { get; set; }
    public DbSet<Template> Templates { get; set; }
    public DbSet<TenantProviderConfig> TenantProviderConfigs { get; set; }
    public DbSet<Tenant> Tenants { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasIndex(e => e.Name)
                  .HasDatabaseName("IX_Tenant_Name");

            entity.HasIndex(e => e.ApiKeyHash)
                  .IsUnique()
                  .HasDatabaseName("IX_Tenant_ApiKeyHash");
        });

        modelBuilder.Entity<MessageDispatch>(entity =>
        {
            entity.HasIndex(e => e.IdempotencyKey)
                  .IsUnique()
                  .HasDatabaseName("IX_MessageDispatch_IdempotencyKey");

            entity.HasIndex(e => new { e.CurrentStatus, e.CreatedAt })
                  .HasDatabaseName("IX_MessageDispatch_QueueProcessing");

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<Template>(entity =>
        {
            entity.HasIndex(e => e.Name)
                  .HasDatabaseName("IX_Template_Name");

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<TenantProviderConfig>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ProviderType })
                  .HasDatabaseName("IX_TenantProviderConfig_Tenant_Provider");

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<MessageDispatchEvent>(entity =>
        {
            entity.HasIndex(e => e.DispatchId)
                  .HasDatabaseName("IX_MessageDispatchEvent_DispatchId");

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne<MessageDispatch>()
                  .WithMany()
                  .HasForeignKey(e => e.DispatchId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}