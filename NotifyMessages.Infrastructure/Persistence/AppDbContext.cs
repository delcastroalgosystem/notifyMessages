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
    public DbSet<MessageTrigger> MessageTriggers { get; set; }
    public DbSet<DispatchBatch> DispatchBatches { get; set; }
    public DbSet<Suppression> Suppressions { get; set; }

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

            entity.HasIndex(e => e.RefName)
                  .IsUnique()
                  .HasFilter("[REF_NAME] IS NOT NULL")
                  .HasDatabaseName("IX_Tenant_RefName");
        });

        modelBuilder.Entity<MessageDispatch>(entity =>
        {
            entity.HasIndex(e => e.IdempotencyKey)
                  .IsUnique()
                  .HasDatabaseName("IX_MessageDispatch_IdempotencyKey");

            entity.HasIndex(e => new { e.CurrentStatus, e.CreatedAt })
                  .HasDatabaseName("IX_MessageDispatch_QueueProcessing");

            // Idempotência pela chave do cliente (ex. "SOCIO:1234:2026"), sem janela temporal.
            entity.HasIndex(e => new { e.TenantId, e.ExternalKey })
                  .IsUnique()
                  .HasFilter("[EXTERNAL_KEY] IS NOT NULL")
                  .HasDatabaseName("IX_MessageDispatch_Tenant_ExternalKey");

            entity.HasIndex(e => e.BatchId)
                  .HasDatabaseName("IX_MessageDispatch_BatchId");

            // Restrict: um lote com envios não pode ser apagado (é o histórico).
            entity.HasOne(e => e.Batch)
                  .WithMany()
                  .HasForeignKey(e => e.BatchId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<Template>(entity =>
        {
            entity.HasIndex(e => e.Name)
                  .HasDatabaseName("IX_Template_Name");

            entity.HasIndex(e => e.TenantId)
                  .HasDatabaseName("IX_Template_TenantId");

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

        modelBuilder.Entity<MessageTrigger>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Name })
                  .IsUnique()
                  .HasDatabaseName("IX_MessageTrigger_Tenant_Name");

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.ToTable(t => t.HasCheckConstraint("CK_MessageTrigger_ScheduleDay",
                "[SCHEDULE_DAY] IS NULL OR [SCHEDULE_DAY] BETWEEN 1 AND 28"));
        });

        modelBuilder.Entity<DispatchBatch>(entity =>
        {
            // Um lote de conector por gatilho e por dia: pedir outra vez o mesmo dia não duplica.
            entity.HasIndex(e => new { e.TriggerId, e.RunDate })
                  .IsUnique()
                  .HasFilter("[TRIGGER_ID] IS NOT NULL AND [RUN_DATE] IS NOT NULL AND [SOURCE] = 1")
                  .HasDatabaseName("IX_DispatchBatch_Trigger_RunDate");

            entity.HasIndex(e => new { e.TenantId, e.CreatedAt })
                  .HasDatabaseName("IX_DispatchBatch_Tenant_CreatedAt");

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<Suppression>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Contact })
                  .IsUnique()
                  .HasDatabaseName("IX_Suppression_Tenant_Contact");

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