using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ApexWallet.Api.Database.Models;

public partial class ApexWalletDbContext : DbContext
{
    public ApexWalletDbContext()
    {
    }

    public ApexWalletDbContext(DbContextOptions<ApexWalletDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Transaction> Transactions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Wallet> Wallets { get; set; }

    //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    //    => optionsBuilder.UseNpgsql("DefaultConnection");

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Leave this completely blank inside the block!
        // This forces EF Core to strictly use the configuration we registered in Program.cs
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Transactionid).HasName("transactions_pkey");

            entity.ToTable("transactions");

            entity.Property(e => e.Transactionid).HasColumnName("transactionid");
            entity.Property(e => e.Amount)
                .HasPrecision(18, 4)
                .HasColumnName("amount");
            entity.Property(e => e.Receiverwalletid).HasColumnName("receiverwalletid");
            entity.Property(e => e.Senderwalletid).HasColumnName("senderwalletid");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Pending'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.Timestamp)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("timestamp");
            entity.Property(e => e.Transactiontype)
                .HasMaxLength(50)
                .HasColumnName("transactiontype");

            entity.HasOne(d => d.Receiverwallet).WithMany(p => p.TransactionReceiverwallets)
                .HasForeignKey(d => d.Receiverwalletid)
                .HasConstraintName("fk_receiver_wallet");

            entity.HasOne(d => d.Senderwallet).WithMany(p => p.TransactionSenderwallets)
                .HasForeignKey(d => d.Senderwalletid)
                .HasConstraintName("fk_sender_wallet");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Userid).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.Property(e => e.Userid).HasColumnName("userid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .HasColumnName("email");
            entity.Property(e => e.Fullname)
                .HasMaxLength(100)
                .HasColumnName("fullname");
            entity.Property(e => e.Identitynumberencrypted).HasColumnName("identitynumberencrypted");
            entity.Property(e => e.Passwordhash)
                .HasMaxLength(255)
                .HasColumnName("passwordhash");
            entity.Property(e => e.AccountStatus)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Active'::character varying")
                .HasColumnName("AccountStatus");
        });

        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.HasKey(e => e.Walletid).HasName("wallets_pkey");

            entity.ToTable("wallets");

            entity.HasIndex(e => e.Userid, "wallets_userid_key").IsUnique();

            entity.Property(e => e.Walletid).HasColumnName("walletid");
            entity.Property(e => e.Balance)
                .HasPrecision(18, 4)
                .HasColumnName("balance");
            entity.Property(e => e.Currency)
                .HasMaxLength(10)
                .HasDefaultValueSql("'INR'::character varying")
                .HasColumnName("currency");
            entity.Property(e => e.Updatedat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updatedat");
            entity.Property(e => e.Userid).HasColumnName("userid");

            entity.HasOne(d => d.User).WithOne(p => p.Wallet)
                .HasForeignKey<Wallet>(d => d.Userid)
                .HasConstraintName("fk_wallet_user");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
