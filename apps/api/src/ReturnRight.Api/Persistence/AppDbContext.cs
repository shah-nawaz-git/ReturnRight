using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ReturnRight.Api.Domain;

namespace ReturnRight.Api.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<TemporaryIntake> TemporaryIntakes => Set<TemporaryIntake>();
    public DbSet<IssueCase> IssueCases => Set<IssueCase>();
    public DbSet<CaseAffectedItem> CaseAffectedItems => Set<CaseAffectedItem>();
    public DbSet<Evidence> Evidences => Set<Evidence>();
    public DbSet<Interaction> Interactions => Set<Interaction>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<InAppNotification> InAppNotifications => Set<InAppNotification>();
    public DbSet<CaseTimelineEvent> CaseTimelineEvents => Set<CaseTimelineEvent>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<FieldSource>().HaveConversion<string>();
        configurationBuilder.Properties<DocumentCategory>().HaveConversion<string>();
        configurationBuilder.Properties<IntakeStatus>().HaveConversion<string>();
        configurationBuilder.Properties<ProblemType>().HaveConversion<string>();
        configurationBuilder.Properties<RequestedOutcomeType>().HaveConversion<string>();
        configurationBuilder.Properties<CaseStatus>().HaveConversion<string>();
        configurationBuilder.Properties<FinalOutcomeType>().HaveConversion<string>();
        configurationBuilder.Properties<EvidenceType>().HaveConversion<string>();
        configurationBuilder.Properties<InteractionType>().HaveConversion<string>();
        configurationBuilder.Properties<ReminderChannel>().HaveConversion<string>();
        configurationBuilder.Properties<ReminderStatus>().HaveConversion<string>();
        configurationBuilder.Properties<TimelineEventType>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Purchase>(entity =>
        {
            entity.Property(p => p.MerchantName).HasMaxLength(200);
            entity.Property(p => p.OrderNumber).HasMaxLength(100);
            entity.Property(p => p.Currency).HasMaxLength(3);
            entity.Property(p => p.Notes).HasMaxLength(2000);
            entity.Property(p => p.TotalAmount).HasPrecision(12, 2);
            entity.OwnsOne(p => p.MerchantNameProvenance);
            entity.OwnsOne(p => p.OrderNumberProvenance);
            entity.OwnsOne(p => p.PurchaseDateProvenance);
            entity.OwnsOne(p => p.TotalAmountProvenance);
            entity.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(p => p.Items).WithOne(i => i.Purchase).HasForeignKey(i => i.PurchaseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(p => p.Documents).WithOne(d => d.Purchase).HasForeignKey(d => d.PurchaseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(p => p.Cases).WithOne(c => c.Purchase).HasForeignKey(c => c.PurchaseId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PurchaseItem>(entity =>
        {
            entity.Property(i => i.ProductName).HasMaxLength(200);
            entity.Property(i => i.UnitPrice).HasPrecision(12, 2);
            entity.OwnsOne(i => i.ReturnDeadlineProvenance);
            entity.OwnsOne(i => i.CommercialWarrantyEndProvenance);
        });

        builder.Entity<Document>(entity =>
        {
            entity.HasIndex(d => d.StorageKey).IsUnique();
            entity.Property(d => d.OriginalFileName).HasMaxLength(255);
            entity.Property(d => d.ContentType).HasMaxLength(100);
            entity.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TemporaryIntake>(entity =>
        {
            entity.Property(t => t.OriginalFileName).HasMaxLength(255);
            entity.Property(t => t.ContentType).HasMaxLength(100);
            entity.Property(t => t.CandidatesJson).HasColumnType("jsonb");
            entity.Property(t => t.ItemsJson).HasColumnType("jsonb");
            entity.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<IssueCase>(entity =>
        {
            entity.Property(c => c.Description).HasMaxLength(4000);
            entity.Property(c => c.RequestedCurrency).HasMaxLength(3);
            entity.Property(c => c.RequestedAmount).HasPrecision(12, 2);
            entity.Property(c => c.FinalCurrency).HasMaxLength(3);
            entity.Property(c => c.FinalAmount).HasPrecision(12, 2);
            entity.Property(c => c.FinalNote).HasMaxLength(2000);
            entity.Property(c => c.DismissedNextActionKey).HasMaxLength(100);
            entity.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(c => c.AffectedItems).WithOne(a => a.Case).HasForeignKey(a => a.CaseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(c => c.Evidence).WithOne(e => e.Case).HasForeignKey(e => e.CaseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(c => c.Interactions).WithOne(i => i.Case).HasForeignKey(i => i.CaseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(c => c.FollowUps).WithOne(f => f.Case).HasForeignKey(f => f.CaseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(c => c.TimelineEvents).WithOne(t => t.Case).HasForeignKey(t => t.CaseId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CaseAffectedItem>(entity =>
        {
            entity.HasKey(a => new { a.CaseId, a.PurchaseItemId });
            entity.HasOne(a => a.PurchaseItem).WithMany().HasForeignKey(a => a.PurchaseItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Evidence>(entity =>
        {
            entity.HasIndex(e => new { e.CaseId, e.DocumentId }).IsUnique();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasOne(e => e.Document).WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Interaction>(entity =>
        {
            entity.Property(i => i.Note).HasMaxLength(1000);
            entity.HasOne(i => i.Document).WithMany().HasForeignKey(i => i.DocumentId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<FollowUp>(entity =>
        {
            entity.Property(f => f.Title).HasMaxLength(200);
        });

        builder.Entity<Reminder>(entity =>
        {
            entity.HasIndex(r => new { r.Status, r.NextAttemptAt });
            entity.Property(r => r.LastError).HasMaxLength(500);
            entity.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(r => r.Case).WithMany().HasForeignKey(r => r.CaseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(r => r.FollowUp).WithMany().HasForeignKey(r => r.FollowUpId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<InAppNotification>(entity =>
        {
            entity.Property(n => n.Title).HasMaxLength(200);
            entity.Property(n => n.Body).HasMaxLength(1000);
            entity.HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(n => n.Case).WithMany().HasForeignKey(n => n.CaseId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<CaseTimelineEvent>(entity =>
        {
            entity.Property(t => t.Summary).HasMaxLength(500);
            entity.Property(t => t.MetadataJson).HasColumnType("jsonb");
        });
    }
}
