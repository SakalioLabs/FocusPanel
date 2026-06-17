using Microsoft.EntityFrameworkCore;
using FocusPanel.Models;

namespace FocusPanel.Data;

public class AppDbContext : DbContext
{
    public DbSet<TodoItem> Todos { get; set; }
    public DbSet<PomodoroSession> PomodoroSessions { get; set; }
    public DbSet<DesktopPartition> DesktopPartitions { get; set; }
    public DbSet<DesktopFilePreference> DesktopFilePreferences { get; set; }
    public DbSet<AppConfig> AppConfigs { get; set; }
    public DbSet<OkrObjective> OkrObjectives { get; set; }
    public DbSet<OkrKeyResult> OkrKeyResults { get; set; }
    public DbSet<OkrSyncLog> OkrSyncLogs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        string appDataPath = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), 
            "FocusPanel");
            
        if (!System.IO.Directory.Exists(appDataPath))
        {
            System.IO.Directory.CreateDirectory(appDataPath);
        }
            
        string dbPath = System.IO.Path.Combine(appDataPath, "focuspanel.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure self-referencing relationship
        modelBuilder.Entity<TodoItem>()
            .HasOne(t => t.Parent)
            .WithMany(t => t.Children)
            .HasForeignKey(t => t.ParentId)
            .IsRequired(false) // Explicitly make optional
            .OnDelete(DeleteBehavior.Cascade);
            
        // OKR relationships
        modelBuilder.Entity<OkrKeyResult>()
            .HasOne(kr => kr.Objective)
            .WithMany(o => o.KeyResults)
            .HasForeignKey(kr => kr.ObjectiveId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed default Inbox project (Root Item)
        modelBuilder.Entity<TodoItem>().HasData(
            new TodoItem 
            { 
                Id = 1, 
                Title = "Inbox", 
                ParentId = null,
                ViewMode = ProjectViewMode.List, 
                ColumnsJson = "[\"To Do\", \"Done\"]",
                IsCompleted = false,
                Status = "Active"
            }
        );
    }

    public void EnsureSchema()
    {
        // Manual migration for existing databases
        // Check if tables exist, if not create them
        try
        {
            Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS PomodoroSessions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    StartTime TEXT NOT NULL,
                    EndTime TEXT NOT NULL,
                    DurationMinutes INTEGER NOT NULL,
                    Status TEXT
                );
                
                CREATE TABLE IF NOT EXISTS DesktopPartitions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT,
                    OrderIndex INTEGER NOT NULL,
                    ColumnIndex INTEGER DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS DesktopFilePreferences (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FilePath TEXT,
                    PartitionName TEXT,
                    IsHiddenFromDesktop INTEGER DEFAULT 0,
                    DesktopX REAL,
                    DesktopY REAL
                );
                
                CREATE TABLE IF NOT EXISTS AppConfigs (
                    Key TEXT PRIMARY KEY,
                    Value TEXT
                );

                CREATE TABLE IF NOT EXISTS OkrObjectives (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FeishuObjectiveId TEXT,
                    UserId TEXT,
                    Name TEXT NOT NULL DEFAULT '',
                    Note TEXT,
                    Progress REAL NOT NULL DEFAULT 0,
                    Period TEXT,
                    Weight REAL NOT NULL DEFAULT 1.0,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    FeishuCreatedAt TEXT,
                    FeishuUpdatedAt TEXT,
                    SyncStatus INTEGER NOT NULL DEFAULT 0,
                    LastSyncedAt TEXT,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS OkrKeyResults (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FeishuKrId TEXT,
                    ObjectiveId INTEGER NOT NULL,
                    Name TEXT NOT NULL DEFAULT '',
                    CurrentValue REAL NOT NULL DEFAULT 0,
                    StartValue REAL NOT NULL DEFAULT 0,
                    TargetValue REAL NOT NULL DEFAULT 100,
                    Progress REAL NOT NULL DEFAULT 0,
                    Weight REAL NOT NULL DEFAULT 1.0,
                    Unit TEXT DEFAULT '%',
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    FeishuUpdatedAt TEXT,
                    SyncStatus INTEGER NOT NULL DEFAULT 0,
                    LastSyncedAt TEXT,
                    IsDeleted INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (ObjectiveId) REFERENCES OkrObjectives(Id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS OkrSyncLogs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    Action TEXT NOT NULL DEFAULT '',
                    EntityType TEXT NOT NULL DEFAULT '',
                    LocalId INTEGER,
                    FeishuId TEXT,
                    Message TEXT NOT NULL DEFAULT '',
                    DetailsJson TEXT
                );
            ");

                        // Migration: Add IsHiddenFromDesktop if not exists
            try 
            {
                Database.ExecuteSqlRaw("ALTER TABLE DesktopFilePreferences ADD COLUMN IsHiddenFromDesktop INTEGER DEFAULT 0;");
            } 
            catch { }

            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE DesktopFilePreferences ADD COLUMN DesktopX REAL;");
            }
            catch { }

            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE DesktopFilePreferences ADD COLUMN DesktopY REAL;");
            }
            catch { }

            //
            try 
            {
                Database.ExecuteSqlRaw("ALTER TABLE DesktopPartitions ADD COLUMN ColumnIndex INTEGER DEFAULT 0;");
            } 
            catch { /* Column likely exists */ }
        }
        catch { }
    }
}
