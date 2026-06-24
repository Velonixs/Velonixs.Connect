using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Persistence.Identity;
using Velonixs.Connect.Persistence.Security;

namespace Velonixs.Connect.Persistence.Persistence;

public sealed class RestaurantConnectDbContext(
    DbContextOptions<RestaurantConnectDbContext> options,
    IFieldEncryptionService fieldEncryption)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Domain.Entities.Restaurant> Restaurants => Set<Domain.Entities.Restaurant>();
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    public DbSet<MessageLog> MessageLogs => Set<MessageLog>();
    public DbSet<DataProtectionState> DataProtectionStates => Set<DataProtectionState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var encryptedStringConverter = new ValueConverter<string, string>(
            value => fieldEncryption.Encrypt(value),
            value => fieldEncryption.Decrypt(value));
        var nullableEncryptedStringConverter = new ValueConverter<string?, string?>(
            value => value == null ? null : fieldEncryption.Encrypt(value),
            value => value == null ? null : fieldEncryption.Decrypt(value));

        ConfigureIdentity(modelBuilder);
        ConfigureDataProtectionState(modelBuilder);
        ConfigureRestaurant(modelBuilder, nullableEncryptedStringConverter);
        ConfigureMenuCategory(modelBuilder);
        ConfigureMenuItem(modelBuilder);
        ConfigureCustomer(modelBuilder, nullableEncryptedStringConverter);
        ConfigureConversation(modelBuilder, encryptedStringConverter, nullableEncryptedStringConverter);
        ConfigureOrder(modelBuilder, encryptedStringConverter, nullableEncryptedStringConverter);
        ConfigureOrderItem(modelBuilder);
        ConfigureOrderStatusHistory(modelBuilder, nullableEncryptedStringConverter);
        ConfigureMessageLog(modelBuilder, encryptedStringConverter);
    }

    private static void ConfigureDataProtectionState(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DataProtectionState>(entity =>
        {
            entity.ToTable("DataProtectionState");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(100);
        });
    }

    private static void ConfigureIdentity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("AuthUser");
            entity.Property(x => x.DisplayName).HasMaxLength(200);
            entity.HasIndex(x => x.BusinessId);
            entity.HasOne(x => x.Business)
                .WithMany()
                .HasForeignKey(x => x.BusinessId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("AuthRole");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("AuthUserRole");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("AuthUserClaim");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("AuthUserLogin");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("AuthRoleClaim");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("AuthUserToken");
    }

    private static void ConfigureRestaurant(ModelBuilder modelBuilder, ValueConverter<string?, string?> encryptedStringConverter)
    {
        modelBuilder.Entity<Domain.Entities.Restaurant>(entity =>
        {
            entity.ToTable("Restaurant");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.BusinessType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.WhatsAppPhoneNumberId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.BusinessPhone).HasColumnType("nvarchar(max)").HasConversion(encryptedStringConverter);
            entity.Property(x => x.NotificationEmail).HasColumnType("nvarchar(max)").HasConversion(encryptedStringConverter);
            entity.Property(x => x.StaffWhatsAppNumber).HasColumnType("nvarchar(max)").HasConversion(encryptedStringConverter);
            entity.Property(x => x.Address).HasColumnType("nvarchar(max)").HasConversion(encryptedStringConverter);

            entity.HasIndex(x => x.WhatsAppPhoneNumberId).IsUnique();
        });
    }

    private static void ConfigureMenuCategory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MenuCategory>(entity =>
        {
            entity.ToTable("MenuCategory");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();

            entity.HasIndex(x => new { x.RestaurantId, x.Name }).IsUnique();
            entity.HasOne(x => x.Restaurant)
                .WithMany(x => x.MenuCategories)
                .HasForeignKey(x => x.RestaurantId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureMenuItem(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.ToTable("MenuItem");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Price).HasPrecision(18, 2);

            entity.HasIndex(x => new { x.RestaurantId, x.ItemCode }).IsUnique();
            entity.HasOne(x => x.Restaurant)
                .WithMany(x => x.MenuItems)
                .HasForeignKey(x => x.RestaurantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Category)
                .WithMany(x => x.MenuItems)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCustomer(ModelBuilder modelBuilder, ValueConverter<string?, string?> encryptedStringConverter)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customer");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.PhoneNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Name).HasColumnType("nvarchar(max)").HasConversion(encryptedStringConverter);
            entity.Property(x => x.LastAddress).HasColumnType("nvarchar(max)").HasConversion(encryptedStringConverter);

            entity.HasIndex(x => new { x.RestaurantId, x.PhoneNumber }).IsUnique();
            entity.HasOne(x => x.Restaurant)
                .WithMany(x => x.Customers)
                .HasForeignKey(x => x.RestaurantId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureConversation(
        ModelBuilder modelBuilder,
        ValueConverter<string, string> encryptedStringConverter,
        ValueConverter<string?, string?> nullableEncryptedStringConverter)
    {
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.ToTable("Conversation");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.WhatsAppNumber).HasColumnType("nvarchar(max)").HasConversion(encryptedStringConverter).IsRequired();
            entity.Property(x => x.CurrentState).HasMaxLength(50).IsRequired();
            entity.Property(x => x.TempOrderJson).HasColumnType("nvarchar(max)").HasConversion(nullableEncryptedStringConverter);

            entity.HasIndex(x => new { x.RestaurantId, x.CustomerId, x.IsActive });
            entity.HasOne(x => x.Restaurant)
                .WithMany(x => x.Conversations)
                .HasForeignKey(x => x.RestaurantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Conversations)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOrder(
        ModelBuilder modelBuilder,
        ValueConverter<string, string> encryptedStringConverter,
        ValueConverter<string?, string?> nullableEncryptedStringConverter)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Order");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.OrderNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.CustomerName).HasColumnType("nvarchar(max)").HasConversion(encryptedStringConverter).IsRequired();
            entity.Property(x => x.CustomerPhone).HasColumnType("nvarchar(max)").HasConversion(encryptedStringConverter).IsRequired();
            entity.Property(x => x.Address).HasColumnType("nvarchar(max)").HasConversion(encryptedStringConverter).IsRequired();
            entity.Property(x => x.OrderStatus).HasMaxLength(50).IsRequired();
            entity.Property(x => x.RestaurantComment).HasColumnType("nvarchar(max)").HasConversion(nullableEncryptedStringConverter);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.Source).HasMaxLength(30).IsRequired();

            entity.HasIndex(x => x.OrderNumber).IsUnique();
            entity.HasOne(x => x.Restaurant)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.RestaurantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOrderStatusHistory(
        ModelBuilder modelBuilder,
        ValueConverter<string?, string?> nullableEncryptedStringConverter)
    {
        modelBuilder.Entity<OrderStatusHistory>(entity =>
        {
            entity.ToTable("OrderStatusHistory");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.PreviousStatus).HasMaxLength(50).IsRequired();
            entity.Property(x => x.NewStatus).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Comment).HasColumnType("nvarchar(max)").HasConversion(nullableEncryptedStringConverter);
            entity.Property(x => x.UpdatedBy).HasMaxLength(200).IsRequired();

            entity.HasIndex(x => new { x.OrderId, x.NewStatus, x.UpdatedAtUtc });
            entity.HasOne(x => x.Order)
                .WithMany(x => x.StatusHistory)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureOrderItem(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItem");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ItemName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);

            entity.HasOne(x => x.Order)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.MenuItem)
                .WithMany(x => x.OrderItems)
                .HasForeignKey(x => x.MenuItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureMessageLog(ModelBuilder modelBuilder, ValueConverter<string, string> encryptedStringConverter)
    {
        modelBuilder.Entity<MessageLog>(entity =>
        {
            entity.ToTable("MessageLog");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Direction).HasMaxLength(20).IsRequired();
            entity.Property(x => x.MessageText).HasColumnType("nvarchar(max)").HasConversion(encryptedStringConverter).IsRequired();
            entity.Property(x => x.WhatsAppMessageId).HasMaxLength(512);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();

            entity.HasIndex(x => x.WhatsAppMessageId);
            entity.HasOne(x => x.Restaurant)
                .WithMany(x => x.MessageLogs)
                .HasForeignKey(x => x.RestaurantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Customer)
                .WithMany(x => x.MessageLogs)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Conversation)
                .WithMany(x => x.MessageLogs)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
