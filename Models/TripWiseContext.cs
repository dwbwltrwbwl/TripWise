using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace TripWise.Models;

public partial class TripWiseContext : DbContext
{
    public TripWiseContext()
    {
    }

    public TripWiseContext(DbContextOptions<TripWiseContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ChatMessage> ChatMessages { get; set; }

    public virtual DbSet<Document> Documents { get; set; }

    public virtual DbSet<Expense> Expenses { get; set; }

    public virtual DbSet<ExpenseCategory> ExpenseCategories { get; set; }

    public virtual DbSet<ExpenseShare> ExpenseShares { get; set; }

    public virtual DbSet<InterestCategory> InterestCategories { get; set; }

    public virtual DbSet<ParticipantRole> ParticipantRoles { get; set; }

    public virtual DbSet<PointsOfInterest> PointsOfInterests { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Trip> Trips { get; set; }

    public virtual DbSet<TripParticipant> TripParticipants { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserVote> UserVotes { get; set; }

    public virtual DbSet<VoteOption> VoteOptions { get; set; }

    public virtual DbSet<VotingSystem> VotingSystems { get; set; }

//    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
//#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
//        => optionsBuilder.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=TripWise;Trusted_Connection=true;TrustServerCertificate=true;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.IdMessage);

            entity.HasIndex(e => e.IdPoint, "IX_ChatMessages_idPoint");

            entity.HasIndex(e => e.IdTrip, "IX_ChatMessages_idTrip");

            entity.HasIndex(e => e.IdUser, "IX_ChatMessages_idUser");

            entity.Property(e => e.IdMessage).HasColumnName("idMessage");
            entity.Property(e => e.IdPoint).HasColumnName("idPoint");
            entity.Property(e => e.IdTrip).HasColumnName("idTrip");
            entity.Property(e => e.IdUser).HasColumnName("idUser");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.SentAt).HasColumnName("sentAt");

            entity.HasOne(d => d.IdPointNavigation).WithMany(p => p.ChatMessages).HasForeignKey(d => d.IdPoint);

            entity.HasOne(d => d.IdTripNavigation).WithMany(p => p.ChatMessages).HasForeignKey(d => d.IdTrip);

            entity.HasOne(d => d.IdUserNavigation).WithMany(p => p.ChatMessages).HasForeignKey(d => d.IdUser);
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.IdDocument);

            entity.HasIndex(e => e.IdTrip, "IX_Documents_idTrip");

            entity.HasIndex(e => e.UploadedById, "IX_Documents_uploadedById");

            entity.Property(e => e.IdDocument).HasColumnName("idDocument");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .HasColumnName("description");
            entity.Property(e => e.FileName)
                .HasMaxLength(255)
                .HasColumnName("fileName");
            entity.Property(e => e.FilePath)
                .HasMaxLength(500)
                .HasColumnName("filePath");
            entity.Property(e => e.FileType)
                .HasMaxLength(50)
                .HasColumnName("fileType");
            entity.Property(e => e.IdTrip).HasColumnName("idTrip");
            entity.Property(e => e.UploadedAt).HasColumnName("uploadedAt");
            entity.Property(e => e.UploadedById).HasColumnName("uploadedById");

            entity.HasOne(d => d.IdTripNavigation).WithMany(p => p.Documents).HasForeignKey(d => d.IdTrip);

            entity.HasOne(d => d.UploadedBy).WithMany(p => p.Documents)
                .HasForeignKey(d => d.UploadedById)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.HasKey(e => e.IdExpense);

            entity.HasIndex(e => e.IdPoint, "IX_Expenses_idPoint");

            entity.HasIndex(e => e.IdTrip, "IX_Expenses_idTrip");

            entity.HasIndex(e => e.PaidById, "IX_Expenses_paidById");

            entity.Property(e => e.IdExpense).HasColumnName("idExpense");
            entity.Property(e => e.Amount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("amount");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.ExpenseDate).HasColumnName("expenseDate");
            entity.Property(e => e.IdExpenseCategory).HasColumnName("idExpenseCategory");
            entity.Property(e => e.IdPoint).HasColumnName("idPoint");
            entity.Property(e => e.IdTrip).HasColumnName("idTrip");
            entity.Property(e => e.PaidById).HasColumnName("paidById");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");

            entity.HasOne(d => d.IdExpenseCategoryNavigation).WithMany(p => p.Expenses)
                .HasForeignKey(d => d.IdExpenseCategory)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Expenses_ExpenseCategories");

            entity.HasOne(d => d.IdPointNavigation).WithMany(p => p.Expenses).HasForeignKey(d => d.IdPoint);

            entity.HasOne(d => d.IdTripNavigation).WithMany(p => p.Expenses).HasForeignKey(d => d.IdTrip);

            entity.HasOne(d => d.PaidBy).WithMany(p => p.Expenses)
                .HasForeignKey(d => d.PaidById)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<ExpenseCategory>(entity =>
        {
            entity.HasKey(e => e.IdExpenseCategory);

            entity.Property(e => e.IdExpenseCategory).HasColumnName("idExpenseCategory");
            entity.Property(e => e.ExpenseCategoryName).HasMaxLength(100);
        });

        modelBuilder.Entity<ExpenseShare>(entity =>
        {
            entity.HasKey(e => e.IdExpenseShare);

            entity.HasIndex(e => e.IdExpense, "IX_ExpenseShares_idExpense");

            entity.HasIndex(e => e.IdUser, "IX_ExpenseShares_idUser");

            entity.Property(e => e.IdExpenseShare).HasColumnName("idExpenseShare");
            entity.Property(e => e.IdExpense).HasColumnName("idExpense");
            entity.Property(e => e.IdUser).HasColumnName("idUser");
            entity.Property(e => e.IsPaid).HasColumnName("isPaid");
            entity.Property(e => e.ShareAmount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("shareAmount");

            entity.HasOne(d => d.IdExpenseNavigation).WithMany(p => p.ExpenseShares).HasForeignKey(d => d.IdExpense);

            entity.HasOne(d => d.IdUserNavigation).WithMany(p => p.ExpenseShares)
                .HasForeignKey(d => d.IdUser)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<InterestCategory>(entity =>
        {
            entity.HasKey(e => e.IdInterestCategory);

            entity.Property(e => e.IdInterestCategory).HasColumnName("idInterestCategory");
            entity.Property(e => e.InterestCategory1)
                .HasMaxLength(100)
                .HasColumnName("InterestCategory");
        });

        modelBuilder.Entity<ParticipantRole>(entity =>
        {
            entity.HasKey(e => e.IdParticipantRole);

            entity.Property(e => e.IdParticipantRole).HasColumnName("idParticipantRole");
            entity.Property(e => e.ParticipantRole1)
                .HasMaxLength(50)
                .HasColumnName("ParticipantRole");
        });

        modelBuilder.Entity<PointsOfInterest>(entity =>
        {
            entity.HasKey(e => e.IdPoint);

            entity.ToTable("PointsOfInterest");

            entity.HasIndex(e => e.AddedById, "IX_PointsOfInterest_addedById");

            entity.HasIndex(e => e.IdTrip, "IX_PointsOfInterest_idTrip");

            entity.Property(e => e.IdPoint).HasColumnName("idPoint");
            entity.Property(e => e.AddedById).HasColumnName("addedById");
            entity.Property(e => e.Address)
                .HasMaxLength(255)
                .HasColumnName("address");
            entity.Property(e => e.BookingLink)
                .HasMaxLength(500)
                .HasColumnName("bookingLink");
            entity.Property(e => e.Cost)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("cost");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IdInterestCategory).HasColumnName("idInterestCategory");
            entity.Property(e => e.IdTrip).HasColumnName("idTrip");
            entity.Property(e => e.Latitude)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("latitude");
            entity.Property(e => e.Longitude)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("longitude");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.PlannedDate).HasColumnName("plannedDate");
            entity.Property(e => e.PlannedTime).HasColumnName("plannedTime");

            entity.HasOne(d => d.AddedBy).WithMany(p => p.PointsOfInterests).HasForeignKey(d => d.AddedById);

            entity.HasOne(d => d.IdInterestCategoryNavigation).WithMany(p => p.PointsOfInterests)
                .HasForeignKey(d => d.IdInterestCategory)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PointsOfInterest_InterestCategories");

            entity.HasOne(d => d.IdTripNavigation).WithMany(p => p.PointsOfInterests).HasForeignKey(d => d.IdTrip);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.IdRole);

            entity.Property(e => e.IdRole).HasColumnName("idRole");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Trip>(entity =>
        {
            entity.HasKey(e => e.IdTrip);

            entity.HasIndex(e => e.CreatedById, "IX_Trips_createdById");

            entity.Property(e => e.IdTrip).HasColumnName("idTrip");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.CreatedById).HasColumnName("createdById");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.EndDate).HasColumnName("endDate");
            entity.Property(e => e.StartDate).HasColumnName("startDate");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.TotalBudget)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("totalBudget");

            entity.HasOne(d => d.CreatedBy).WithMany(p => p.Trips)
                .HasForeignKey(d => d.CreatedById)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<TripParticipant>(entity =>
        {
            entity.HasKey(e => e.IdTripParticipant);

            entity.HasIndex(e => e.IdTrip, "IX_TripParticipants_idTrip");

            entity.HasIndex(e => e.IdUser, "IX_TripParticipants_idUser");

            entity.Property(e => e.IdTripParticipant).HasColumnName("idTripParticipant");
            entity.Property(e => e.IdParticipantRole).HasColumnName("idParticipantRole");
            entity.Property(e => e.IdTrip).HasColumnName("idTrip");
            entity.Property(e => e.IdUser).HasColumnName("idUser");
            entity.Property(e => e.JoinedAt).HasColumnName("joinedAt");

            entity.HasOne(d => d.IdParticipantRoleNavigation).WithMany(p => p.TripParticipants)
                .HasForeignKey(d => d.IdParticipantRole)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TripParticipants_ParticipantRoles");

            entity.HasOne(d => d.IdTripNavigation).WithMany(p => p.TripParticipants).HasForeignKey(d => d.IdTrip);

            entity.HasOne(d => d.IdUserNavigation).WithMany(p => p.TripParticipants).HasForeignKey(d => d.IdUser);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.IdUser);

            entity.HasIndex(e => e.IdRole, "IX_Users_idRole");

            entity.Property(e => e.IdUser).HasColumnName("idUser");
            entity.Property(e => e.Age).HasColumnName("age");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.IdRole).HasColumnName("idRole");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.PasswordHash).HasColumnName("passwordHash");

            entity.HasOne(d => d.IdRoleNavigation).WithMany(p => p.Users).HasForeignKey(d => d.IdRole);
        });

        modelBuilder.Entity<UserVote>(entity =>
        {
            entity.HasKey(e => e.IdUserVote);

            entity.HasIndex(e => e.IdUser, "IX_UserVotes_idUser");

            entity.HasIndex(e => e.IdVoteOption, "IX_UserVotes_idVoteOption");

            entity.Property(e => e.IdUserVote).HasColumnName("idUserVote");
            entity.Property(e => e.IdUser).HasColumnName("idUser");
            entity.Property(e => e.IdVoteOption).HasColumnName("idVoteOption");
            entity.Property(e => e.VotedAt).HasColumnName("votedAt");

            entity.HasOne(d => d.IdUserNavigation).WithMany(p => p.UserVotes)
                .HasForeignKey(d => d.IdUser)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.IdVoteOptionNavigation).WithMany(p => p.UserVotes).HasForeignKey(d => d.IdVoteOption);
        });

        modelBuilder.Entity<VoteOption>(entity =>
        {
            entity.HasKey(e => e.IdVoteOption);

            entity.HasIndex(e => e.IdVote, "IX_VoteOptions_idVote");

            entity.Property(e => e.IdVoteOption).HasColumnName("idVoteOption");
            entity.Property(e => e.IdVote).HasColumnName("idVote");
            entity.Property(e => e.OptionText)
                .HasMaxLength(200)
                .HasColumnName("optionText");

            entity.HasOne(d => d.IdVoteNavigation).WithMany(p => p.VoteOptions).HasForeignKey(d => d.IdVote);
        });

        modelBuilder.Entity<VotingSystem>(entity =>
        {
            entity.HasKey(e => e.IdVote);

            entity.ToTable("votingSystems");

            entity.HasIndex(e => e.CreatedById, "IX_votingSystems_createdById");

            entity.HasIndex(e => e.IdPoint, "IX_votingSystems_idPoint");

            entity.HasIndex(e => e.IdTrip, "IX_votingSystems_idTrip");

            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.CreatedById).HasColumnName("createdById");
            entity.Property(e => e.ExpiresAt).HasColumnName("expiresAt");
            entity.Property(e => e.IdPoint).HasColumnName("idPoint");
            entity.Property(e => e.IdTrip).HasColumnName("idTrip");
            entity.Property(e => e.Question)
                .HasMaxLength(200)
                .HasColumnName("question");

            entity.HasOne(d => d.CreatedBy).WithMany(p => p.VotingSystems)
                .HasForeignKey(d => d.CreatedById)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.IdPointNavigation).WithMany(p => p.VotingSystems).HasForeignKey(d => d.IdPoint);

            entity.HasOne(d => d.IdTripNavigation).WithMany(p => p.VotingSystems).HasForeignKey(d => d.IdTrip);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
