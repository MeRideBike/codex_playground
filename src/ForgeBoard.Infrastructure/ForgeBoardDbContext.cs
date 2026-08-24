using ForgeBoard.Domain;
using Microsoft.EntityFrameworkCore;

namespace ForgeBoard.Infrastructure;

public sealed class ForgeBoardDbContext(DbContextOptions<ForgeBoardDbContext> options) : DbContext(options)
{
    public DbSet<Mission> Missions => Set<Mission>();
    public DbSet<RunStep> RunSteps => Set<RunStep>();
    public DbSet<ApprovalRecord> ApprovalRecords => Set<ApprovalRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Mission>(entity =>
        {
            entity.ToTable("Missions"); entity.HasKey(mission => mission.Id);
            entity.Property(mission => mission.Title).HasMaxLength(200).IsRequired();
            entity.Property(mission => mission.Description).HasMaxLength(10_000).IsRequired();
            entity.Property(mission => mission.WorkflowName).HasMaxLength(200).IsRequired();
            entity.Property(mission => mission.SampleId).HasMaxLength(100);
            entity.Property(mission => mission.TemplateId).HasMaxLength(100);
            entity.Property(mission => mission.OperationPolicy).HasMaxLength(4_000).IsRequired();
            entity.Property(mission => mission.MaxEstimatedCost).HasPrecision(18, 6);
            entity.Property(mission => mission.Status).HasConversion<string>().HasMaxLength(40);
            entity.HasMany(mission => mission.Steps).WithOne().HasForeignKey(step => step.MissionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(mission => mission.Approvals).WithOne().HasForeignKey(approval => approval.MissionId).OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(mission => mission.Steps).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(mission => mission.Approvals).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        modelBuilder.Entity<RunStep>(entity =>
        {
            entity.ToTable("RunSteps"); entity.HasKey(step => step.Id);
            entity.Property(step => step.NodeKey).HasMaxLength(100).IsRequired();
            entity.Property(step => step.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(step => step.Kind).HasConversion<string>().HasMaxLength(50);
            entity.Property(step => step.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(step => step.EstimatedCost).HasPrecision(18, 6);
        });
        modelBuilder.Entity<ApprovalRecord>(entity =>
        {
            entity.ToTable("ApprovalRecords"); entity.HasKey(approval => approval.Id);
            entity.Property(approval => approval.Outcome).HasConversion<string>().HasMaxLength(50);
            entity.Property(approval => approval.Comment).HasMaxLength(4_000).IsRequired();
        });
    }
}
