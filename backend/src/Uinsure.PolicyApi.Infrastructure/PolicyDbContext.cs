using Microsoft.EntityFrameworkCore;
using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Infrastructure;

public sealed class PolicyDbContext(DbContextOptions<PolicyDbContext> options) : DbContext(options)
{
    private const string PolicyForeignKey = "PolicyReference";

    public DbSet<Policy> Policies => Set<Policy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Policy>(policy =>
        {
            policy.ToTable("Policies");

            // The reference is the natural key, so the primary key also enforces uniqueness.
            policy.HasKey(p => p.Reference);
            policy.Property(p => p.Reference).HasMaxLength(PolicyRules.MaxReferenceLength);
            policy.Property(p => p.InsuranceType).HasConversion<string>().HasMaxLength(20);
            policy.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

            // Optimistic concurrency: an update fails if another request changed the policy first.
            policy.Property(p => p.Version).IsConcurrencyToken();

            // Derived from the latest term rather than stored.
            policy.Ignore(p => p.LatestTerm);
            policy.Ignore(p => p.StartDate);
            policy.Ignore(p => p.EndDate);
            policy.Ignore(p => p.Amount);

            policy.OwnsOne(p => p.Property, property =>
            {
                property.Property(a => a.AddressLine1).HasMaxLength(PolicyRules.MaxAddressLineLength).IsRequired();
                property.Property(a => a.AddressLine2).HasMaxLength(PolicyRules.MaxAddressLineLength);
                property.Property(a => a.AddressLine3).HasMaxLength(PolicyRules.MaxAddressLineLength);
                property.Property(a => a.Postcode).HasMaxLength(PolicyRules.MaxPostcodeLength).IsRequired();
            });
            policy.Navigation(p => p.Property).IsRequired();

            policy.OwnsMany(p => p.Policyholders, holder =>
            {
                holder.ToTable("Policyholders");
                holder.WithOwner().HasForeignKey(PolicyForeignKey);
                holder.Property<int>("Id");
                holder.HasKey("Id");
                holder.Property(h => h.FirstName).HasMaxLength(PolicyRules.MaxNameLength);
                holder.Property(h => h.LastName).HasMaxLength(PolicyRules.MaxNameLength);
            });

            policy.OwnsMany(p => p.Terms, term =>
            {
                term.ToTable("PolicyTerms");
                term.WithOwner().HasForeignKey(PolicyForeignKey);
                term.HasKey(PolicyForeignKey, nameof(PolicyTerm.TermNumber));
                term.Property(t => t.TermNumber).ValueGeneratedNever();
                term.Property(t => t.Amount).HasPrecision(18, 2);
            });

            policy.OwnsMany(p => p.Payments, payment =>
            {
                payment.ToTable("Payments");
                payment.WithOwner().HasForeignKey(PolicyForeignKey);
                payment.HasKey(p => p.PaymentReference);
                payment.Property(p => p.Type).HasConversion<string>().HasMaxLength(20);
                payment.Property(p => p.Amount).HasPrecision(18, 2);
            });

            policy.OwnsMany(p => p.Refunds, refund =>
            {
                refund.ToTable("Refunds");
                refund.WithOwner().HasForeignKey(PolicyForeignKey);
                refund.HasKey(r => r.RefundReference);
                refund.Property(r => r.Type).HasConversion<string>().HasMaxLength(20);
                refund.Property(r => r.Amount).HasPrecision(18, 2);
            });
        });
    }
}
