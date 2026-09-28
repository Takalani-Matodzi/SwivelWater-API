using Microsoft.EntityFrameworkCore;
using SwivelWater.API.Models;

namespace SwivelWater.API.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }

    public DbSet<Customer> Customers { get; set; }

    public DbSet<Address> Addresses { get; set; }

    public DbSet<Employee> Employees { get; set; }
    public DbSet<EmployeeShift> EmployeeShifts { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<RefillLoyaltyCard> RefillLoyaltyCards { get; set; }
    public DbSet<RefillLoyaltyTransaction> RefillLoyaltyTransactions { get; set; }
    public DbSet<Order> Orders { get; set; }

    public DbSet<OrderItem> OrderItems { get; set; }

    public DbSet<Payment> Payments { get; set; }

    public DbSet<Delivery> Deliveries { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // USER
        modelBuilder.Entity<User>()
            .HasKey(u => u.UserId);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();


        // CUSTOMER
        modelBuilder.Entity<Customer>()
            .HasKey(c => c.CustomerId);

        modelBuilder.Entity<Customer>()
            .HasOne(c => c.User)
            .WithOne(u => u.Customer)
            .HasForeignKey<Customer>(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

                // REFILL LOYALTY CARD
        modelBuilder.Entity<RefillLoyaltyCard>()
            .HasKey(r => r.RefillLoyaltyCardId);

        modelBuilder.Entity<RefillLoyaltyCard>()
            .HasOne(r => r.Customer)
            .WithMany()
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RefillLoyaltyCard>()
            .HasIndex(r => r.CustomerId)
            .IsUnique();

        modelBuilder.Entity<RefillLoyaltyCard>()
            .ToTable("RefillLoyaltyCards", table =>
            {
                table.HasCheckConstraint(
                    "CK_RefillLoyaltyCards_TickCount",
                    "\"TickCount\" >= 0 AND \"TickCount\" <= 9");

                table.HasCheckConstraint(
                    "CK_RefillLoyaltyCards_FreeRefills",
                    "\"FreeRefillsAvailable\" >= 0");
            });
                // REFILL LOYALTY TRANSACTION
        modelBuilder.Entity<RefillLoyaltyTransaction>()
            .HasKey(r => r.RefillLoyaltyTransactionId);

        modelBuilder.Entity<RefillLoyaltyTransaction>()
            .HasOne(r => r.RefillLoyaltyCard)
            .WithMany()
            .HasForeignKey(r => r.RefillLoyaltyCardId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RefillLoyaltyTransaction>()
            .HasOne(r => r.Order)
            .WithMany()
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RefillLoyaltyTransaction>()
            .HasOne(r => r.OrderItem)
            .WithMany()
            .HasForeignKey(r => r.OrderItemId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<RefillLoyaltyTransaction>()
            .ToTable("RefillLoyaltyTransactions", table =>
            {
                table.HasCheckConstraint(
                    "CK_RefillLoyaltyTransactions_Type",
                    "\"TransactionType\" IN ('TICK_EARNED', 'FREE_REFILL_REDEEMED')");

                table.HasCheckConstraint(
                    "CK_RefillLoyaltyTransactions_Litres",
                    "\"Litres\" > 0");

                table.HasCheckConstraint(
                    "CK_RefillLoyaltyTransactions_TicksAdded",
                    "\"TicksAdded\" >= 0");

                table.HasCheckConstraint(
                    "CK_RefillLoyaltyTransactions_FreeAdded",
                    "\"FreeRefillsAdded\" >= 0");

                table.HasCheckConstraint(
                    "CK_RefillLoyaltyTransactions_FreeUsed",
                    "\"FreeRefillsUsed\" >= 0");
            });    
                // ADDRESS
        modelBuilder.Entity<Address>()
            .HasKey(a => a.AddressId);

        modelBuilder.Entity<Address>()
            .HasOne(a => a.Customer)
            .WithMany(c => c.Addresses)
            .HasForeignKey(a => a.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);


        // EMPLOYEE
        modelBuilder.Entity<Employee>().HasKey(e => e.EmployeeId);

modelBuilder.Entity<Employee>()
    .HasOne(e => e.User)
    .WithOne(u => u.Employee)
    .HasForeignKey<Employee>(e => e.UserId)
    .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<Employee>()
    .HasIndex(e => e.EmployeeNumber)
    .IsUnique();


        // PRODUCT
        modelBuilder.Entity<Product>()
            .HasKey(p => p.ProductId);


        // ORDER
        modelBuilder.Entity<Order>()
            .HasKey(o => o.OrderId);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Customer)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Address)
            .WithMany()
            .HasForeignKey(o => o.AddressId)
            .OnDelete(DeleteBehavior.Restrict);


        // ORDER ITEM
        modelBuilder.Entity<OrderItem>()
            .HasKey(oi => oi.OrderItemId);

        modelBuilder.Entity<OrderItem>()
            .HasOne(oi => oi.Order)
            .WithMany(o => o.OrderItems)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OrderItem>()
            .HasOne(oi => oi.Product)
            .WithMany(p => p.OrderItems)
            .HasForeignKey(oi => oi.ProductId)
            .OnDelete(DeleteBehavior.Restrict);


        // PAYMENT
        modelBuilder.Entity<Payment>()
            .HasKey(p => p.PaymentId);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Order)
            .WithMany(o => o.Payments)
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Cascade);


        // DELIVERY
        modelBuilder.Entity<Delivery>()
            .HasKey(d => d.DeliveryId);

        modelBuilder.Entity<Delivery>()
            .HasOne(d => d.Order)
            .WithOne(o => o.Delivery)
            .HasForeignKey<Delivery>(d => d.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Delivery>()
            .HasOne(d => d.Employee)
            .WithMany(e => e.Deliveries)
            .HasForeignKey(d => d.EmployeeId)
            .OnDelete(DeleteBehavior.SetNull);


        // DECIMAL PRECISION
        modelBuilder.Entity<Product>()
            .Property(p => p.Price)
            .HasPrecision(12, 2);

        modelBuilder.Entity<Order>()
            .Property(o => o.TotalAmount)
            .HasPrecision(12, 2);

        modelBuilder.Entity<OrderItem>()
            .Property(oi => oi.UnitPrice)
            .HasPrecision(12, 2);

        modelBuilder.Entity<OrderItem>()
            .Property(oi => oi.SubTotal)
            .HasPrecision(12, 2);

        modelBuilder.Entity<Payment>()
            .Property(p => p.Amount)
            .HasPrecision(12, 2);
    }
}