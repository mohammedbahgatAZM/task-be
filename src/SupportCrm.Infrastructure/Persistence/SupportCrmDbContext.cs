namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;

public class SupportCrmDbContext(DbContextOptions<SupportCrmDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<ContactDetail> ContactDetails => Set<ContactDetail>();
    public DbSet<ContactDetailChangeLogEntry> ContactDetailChangeLogEntries => Set<ContactDetailChangeLogEntry>();
    public DbSet<CustomerNote> CustomerNotes => Set<CustomerNote>();
    public DbSet<CustomerAttachment> CustomerAttachments => Set<CustomerAttachment>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketStatusChangeEntry> TicketStatusChangeEntries => Set<TicketStatusChangeEntry>();
    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();
    public DbSet<TicketFieldChangeEntry> TicketFieldChangeEntries => Set<TicketFieldChangeEntry>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TicketAssignmentChangeEntry> TicketAssignmentChangeEntries => Set<TicketAssignmentChangeEntry>();
    public DbSet<TicketEscalationEntry> TicketEscalationEntries => Set<TicketEscalationEntry>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<TicketNote> TicketNotes => Set<TicketNote>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<TicketMessageDeliveryStatus> TicketMessageDeliveryStatuses => Set<TicketMessageDeliveryStatus>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<WebFormFieldDefinition> WebFormFieldDefinitions => Set<WebFormFieldDefinition>();
    public DbSet<TicketTask> TicketTasks => Set<TicketTask>();
    public DbSet<AgentNotification> AgentNotifications => Set<AgentNotification>();
    public DbSet<QuickReplyTemplate> QuickReplyTemplates => Set<QuickReplyTemplate>();
    public DbSet<TicketCollaborator> TicketCollaborators => Set<TicketCollaborator>();
    public DbSet<SlaTarget> SlaTargets => Set<SlaTarget>();
    public DbSet<BusinessHours> BusinessHours => Set<BusinessHours>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<AgentSkill> AgentSkills => Set<AgentSkill>();
    public DbSet<AgentLanguage> AgentLanguages => Set<AgentLanguage>();
    public DbSet<AssignmentRule> AssignmentRules => Set<AssignmentRule>();
    public DbSet<EscalationRule> EscalationRules => Set<EscalationRule>();
    public DbSet<EscalationTier> EscalationTiers => Set<EscalationTier>();
    public DbSet<EscalationLogEntry> EscalationLogEntries => Set<EscalationLogEntry>();
    public DbSet<AlertPreference> AlertPreferences => Set<AlertPreference>();
    public DbSet<SlaAlertLog> SlaAlertLogs => Set<SlaAlertLog>();
    public DbSet<DigestLogEntry> DigestLogEntries => Set<DigestLogEntry>();
    public DbSet<KbCategory> KbCategories => Set<KbCategory>();
    public DbSet<Faq> Faqs => Set<Faq>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<ArticleAttachment> ArticleAttachments => Set<ArticleAttachment>();
    public DbSet<Guide> Guides => Set<Guide>();
    public DbSet<GuideAttachment> GuideAttachments => Set<GuideAttachment>();
    public DbSet<GuideTicketCategory> GuideTicketCategories => Set<GuideTicketCategory>();
    public DbSet<SearchLog> SearchLogs => Set<SearchLog>();
    public DbSet<ContentVersionEntry> ContentVersionEntries => Set<ContentVersionEntry>();
    public DbSet<TicketAiSummary> TicketAiSummaries => Set<TicketAiSummary>();
    public DbSet<TicketCategorizationSuggestion> TicketCategorizationSuggestions => Set<TicketCategorizationSuggestion>();
    public DbSet<SolutionSuggestionFeedback> SolutionSuggestionFeedback => Set<SolutionSuggestionFeedback>();
    public DbSet<FaqPortalImpression> FaqPortalImpressions => Set<FaqPortalImpression>();
    public DbSet<TicketFeedback> TicketFeedback => Set<TicketFeedback>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<BrandingSettings> BrandingSettings => Set<BrandingSettings>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDeliveryLog> WebhookDeliveryLogs => Set<WebhookDeliveryLog>();
    public DbSet<IntegrationConnector> IntegrationConnectors => Set<IntegrationConnector>();
    public DbSet<ErpSyncLog> ErpSyncLogs => Set<ErpSyncLog>();
    public DbSet<ErpSyncState> ErpSyncStates => Set<ErpSyncState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.CustomerNumber).IsRequired().HasMaxLength(32);
            entity.HasIndex(c => c.CustomerNumber).IsUnique();
            entity.Property(c => c.Name).IsRequired().HasMaxLength(256);
            entity.Property(c => c.Company).HasMaxLength(256);
            entity.Property(c => c.Branch).HasMaxLength(256);
            entity.Property(c => c.CreatedAtUtc).IsRequired();
            entity.Property(c => c.PreferredContactChannel).HasConversion<string?>();
            entity.Property(c => c.Address).HasMaxLength(512);
            entity.Property(c => c.Tier).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(c => c.PreferredLanguage).IsRequired().HasMaxLength(4).HasDefaultValue("en");
            entity.HasIndex(c => c.BranchId);
        });

        modelBuilder.Entity<ContactDetail>(entity =>
        {
            entity.ToTable("ContactDetails");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.ChannelType).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(c => c.Value).IsRequired().HasMaxLength(256);
            entity.HasIndex(c => new { c.CustomerId, c.ChannelType });
        });

        modelBuilder.Entity<ContactDetailChangeLogEntry>(entity =>
        {
            entity.ToTable("ContactDetailChangeLog");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ChangeType).IsRequired().HasMaxLength(32);
            entity.Property(e => e.ChangedBy).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.CustomerId);
        });

        modelBuilder.Entity<CustomerNote>(entity =>
        {
            entity.ToTable("CustomerNotes");
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Text).IsRequired();
            entity.Property(n => n.AuthorName).IsRequired().HasMaxLength(256);
            entity.HasIndex(n => n.CustomerId);
        });

        modelBuilder.Entity<CustomerAttachment>(entity =>
        {
            entity.ToTable("CustomerAttachments");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.FileName).IsRequired().HasMaxLength(512);
            entity.Property(a => a.ContentType).IsRequired().HasMaxLength(128);
            entity.Property(a => a.StorageKey).IsRequired().HasMaxLength(1024);
            entity.Property(a => a.UploadedByName).IsRequired().HasMaxLength(256);
            entity.HasIndex(a => a.CustomerId);
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.ToTable("Tickets");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.ReferenceNumber).IsRequired().HasMaxLength(32);
            entity.HasIndex(t => t.ReferenceNumber).IsUnique();
            entity.Property(t => t.Channel).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(t => t.Subject).IsRequired().HasMaxLength(256);
            entity.Property(t => t.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(t => t.RequesterName).IsRequired().HasMaxLength(256);
            entity.Property(t => t.RequesterContactValue).HasMaxLength(256);
            entity.Property(t => t.Priority).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(t => t.Language).HasMaxLength(16);
            entity.HasIndex(t => t.CustomerId);
            entity.HasIndex(t => t.CategoryId);
            entity.HasIndex(t => t.AssignedAgentId);
            entity.HasIndex(t => t.AssignedTeamId);
            entity.HasIndex(t => t.DepartmentId);
        });

        modelBuilder.Entity<TicketStatusChangeEntry>(entity =>
        {
            entity.ToTable("TicketStatusChanges");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OldStatus).HasConversion<string?>().HasMaxLength(16);
            entity.Property(e => e.NewStatus).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(e => e.ChangedBy).IsRequired().HasMaxLength(256);
            entity.Property(e => e.ChangedByKind).IsRequired().HasMaxLength(16);
            entity.HasIndex(e => e.TicketId);
        });

        modelBuilder.Entity<TicketCategory>(entity =>
        {
            entity.ToTable("TicketCategories");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(128);

            entity.HasIndex(c => c.DepartmentId);

            // Seeded starter categories — fixed GUIDs so the seed is migration-stable.
            entity.HasData(
                new { Id = new Guid("11111111-1111-1111-1111-111111111101"), Name = "Billing", ParentCategoryId = (Guid?)null, IsActive = true, DepartmentId = (Guid?)null },
                new { Id = new Guid("11111111-1111-1111-1111-111111111102"), Name = "Technical Issue", ParentCategoryId = (Guid?)null, IsActive = true, DepartmentId = (Guid?)null },
                new { Id = new Guid("11111111-1111-1111-1111-111111111103"), Name = "General Inquiry", ParentCategoryId = (Guid?)null, IsActive = true, DepartmentId = (Guid?)null },
                new { Id = new Guid("11111111-1111-1111-1111-111111111104"), Name = "Account", ParentCategoryId = (Guid?)null, IsActive = true, DepartmentId = (Guid?)null }
            );
        });

        modelBuilder.Entity<TicketFieldChangeEntry>(entity =>
        {
            entity.ToTable("TicketFieldChanges");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FieldName).IsRequired().HasMaxLength(32);
            entity.Property(e => e.ChangedBy).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.TicketId);
        });

        modelBuilder.Entity<Agent>(entity =>
        {
            entity.ToTable("Agents");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Name).IsRequired().HasMaxLength(256);
            entity.Property(a => a.IsAvailable).IsRequired();
            entity.Property(a => a.IsSupervisor).IsRequired();
            entity.Property(a => a.IsKnowledgeBaseEditor).IsRequired();
            entity.Property(a => a.PreferredLanguage).IsRequired().HasMaxLength(4).HasDefaultValue("en");
            entity.HasIndex(a => a.DepartmentId);
            entity.HasIndex(a => a.BranchId);
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.ToTable("Teams");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(256);
            entity.HasIndex(t => t.DepartmentId);

            // Fixed-GUID seed the auto-assignment fallback (AssignmentRuleEngine.DefaultQueueTeamId)
            // targets when no rule matches — must exist so a ticket never lands on a non-existent team.
            entity.HasData(new { Id = new Guid("33333333-3333-3333-3333-333333333301"), Name = "General Queue", DepartmentId = (Guid?)null });
        });

        modelBuilder.Entity<TicketAssignmentChangeEntry>(entity =>
        {
            entity.ToTable("TicketAssignmentChanges");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ChangedBy).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.TicketId);
        });

        modelBuilder.Entity<TicketEscalationEntry>(entity =>
        {
            entity.ToTable("TicketEscalations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Reason).IsRequired();
            entity.Property(e => e.EscalatedBy).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.TicketId);
        });

        modelBuilder.Entity<TicketMessage>(entity =>
        {
            entity.ToTable("TicketMessages");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Body).IsRequired();
            entity.Property(m => m.AuthorName).IsRequired().HasMaxLength(256);
            entity.Property(m => m.AuthorKind).IsRequired().HasMaxLength(16);
            entity.Property(m => m.Channel).HasConversion<string?>();
            entity.HasIndex(m => m.TicketId);
        });

        modelBuilder.Entity<TicketNote>(entity =>
        {
            entity.ToTable("TicketNotes");
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Text).IsRequired();
            entity.Property(n => n.AuthorName).IsRequired().HasMaxLength(256);
            entity.HasIndex(n => n.TicketId);
        });

        modelBuilder.Entity<TicketAttachment>(entity =>
        {
            entity.ToTable("TicketAttachments");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.FileName).IsRequired().HasMaxLength(512);
            entity.Property(a => a.ContentType).IsRequired().HasMaxLength(128);
            entity.Property(a => a.StorageKey).IsRequired().HasMaxLength(1024);
            entity.Property(a => a.UploadedByName).IsRequired().HasMaxLength(256);
            entity.HasIndex(a => a.TicketId);
        });

        modelBuilder.Entity<TicketMessageDeliveryStatus>(entity =>
        {
            entity.ToTable("TicketMessageDeliveryStatuses");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Status).IsRequired().HasMaxLength(32);
            entity.HasIndex(s => s.TicketMessageId);
        });

        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.ToTable("ChatSessions");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.CustomerName).IsRequired().HasMaxLength(256);
            entity.Property(s => s.CustomerContactValue).HasMaxLength(256);
            entity.Property(s => s.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(s => s.Mode).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.HasIndex(s => s.Status);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("ChatMessages");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Body).IsRequired();
            entity.HasIndex(m => m.ChatSessionId);
        });

        modelBuilder.Entity<WebFormFieldDefinition>(entity =>
        {
            entity.ToTable("WebFormFieldDefinitions");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.FieldName).IsRequired().HasMaxLength(128);
            entity.Property(d => d.FieldType).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.HasIndex(d => d.CategoryId);
        });

        modelBuilder.Entity<TicketTask>(entity =>
        {
            entity.ToTable("TicketTasks");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Note).IsRequired();
            entity.HasIndex(t => t.TicketId);
            entity.HasIndex(t => t.AssignedAgentId);
        });

        modelBuilder.Entity<AgentNotification>(entity =>
        {
            entity.ToTable("AgentNotifications");
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Kind).IsRequired().HasMaxLength(32);
            entity.Property(n => n.Message).IsRequired();
            entity.HasIndex(n => n.AgentId);
        });

        modelBuilder.Entity<QuickReplyTemplate>(entity =>
        {
            entity.ToTable("QuickReplyTemplates");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Category).IsRequired().HasMaxLength(128);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(256);
            entity.Property(t => t.Body).IsRequired();
        });

        modelBuilder.Entity<TicketCollaborator>(entity =>
        {
            entity.ToTable("TicketCollaborators");
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => new { c.TicketId, c.AgentId }).IsUnique();
        });

        modelBuilder.Entity<SlaTarget>(entity =>
        {
            entity.ToTable("SlaTargets");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(256);
            entity.Property(t => t.Priority).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(t => t.Tier).HasConversion<string?>().HasMaxLength(16);
            entity.HasIndex(t => new { t.Priority, t.CategoryId, t.Tier });

            // Seeded defaults preserve Agent Dashboard Story 16's original fixed resolution
            // windows exactly (Urgent 4h/High 8h/Medium 24h/Low 72h = 240/480/1440/4320 min);
            // response targets are new, using common response:resolution ratios. Priority-only
            // (Category=null, Tier=null) so every ticket has a fallback target out of the box.
            entity.HasData(
                new { Id = new Guid("22222222-2222-2222-2222-222222222201"), Name = "Default — Urgent", Priority = TicketPriority.Urgent, CategoryId = (Guid?)null, Tier = (CustomerTier?)null, ResponseTargetMinutes = 30, ResolutionTargetMinutes = 240, IsActive = true },
                new { Id = new Guid("22222222-2222-2222-2222-222222222202"), Name = "Default — High", Priority = TicketPriority.High, CategoryId = (Guid?)null, Tier = (CustomerTier?)null, ResponseTargetMinutes = 60, ResolutionTargetMinutes = 480, IsActive = true },
                new { Id = new Guid("22222222-2222-2222-2222-222222222203"), Name = "Default — Medium", Priority = TicketPriority.Medium, CategoryId = (Guid?)null, Tier = (CustomerTier?)null, ResponseTargetMinutes = 240, ResolutionTargetMinutes = 1440, IsActive = true },
                new { Id = new Guid("22222222-2222-2222-2222-222222222204"), Name = "Default — Low", Priority = TicketPriority.Low, CategoryId = (Guid?)null, Tier = (CustomerTier?)null, ResponseTargetMinutes = 480, ResolutionTargetMinutes = 4320, IsActive = true }
            );
        });

        modelBuilder.Entity<BusinessHours>(entity =>
        {
            entity.ToTable("BusinessHours");
            entity.HasKey(h => h.DayOfWeek);
            entity.Property(h => h.DayOfWeek).HasConversion<string>().HasMaxLength(16);

            // Seeded Mon–Fri 09:00–17:00 working, Sat/Sun non-working — one row per day, required
            // for CalculateBusinessMinutesBetweenAsync/AddBusinessMinutesAsync to have data on first run.
            entity.HasData(
                new { DayOfWeek = DayOfWeek.Monday, IsWorkingDay = true, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0) },
                new { DayOfWeek = DayOfWeek.Tuesday, IsWorkingDay = true, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0) },
                new { DayOfWeek = DayOfWeek.Wednesday, IsWorkingDay = true, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0) },
                new { DayOfWeek = DayOfWeek.Thursday, IsWorkingDay = true, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0) },
                new { DayOfWeek = DayOfWeek.Friday, IsWorkingDay = true, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0) },
                new { DayOfWeek = DayOfWeek.Saturday, IsWorkingDay = false, StartTime = new TimeOnly(0, 0), EndTime = new TimeOnly(0, 0) },
                new { DayOfWeek = DayOfWeek.Sunday, IsWorkingDay = false, StartTime = new TimeOnly(0, 0), EndTime = new TimeOnly(0, 0) }
            );
        });

        modelBuilder.Entity<Holiday>(entity =>
        {
            entity.ToTable("Holidays");
            entity.HasKey(h => h.Id);
            entity.Property(h => h.Name).IsRequired().HasMaxLength(256);
            entity.HasIndex(h => h.Date).IsUnique();
        });

        modelBuilder.Entity<AgentSkill>(entity =>
        {
            entity.ToTable("AgentSkills");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Skill).IsRequired().HasMaxLength(128);
            entity.HasIndex(s => new { s.AgentId, s.Skill }).IsUnique();
        });

        modelBuilder.Entity<AgentLanguage>(entity =>
        {
            entity.ToTable("AgentLanguages");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Language).IsRequired().HasMaxLength(64);
            entity.HasIndex(l => new { l.AgentId, l.Language }).IsUnique();
        });

        modelBuilder.Entity<AssignmentRule>(entity =>
        {
            entity.ToTable("AssignmentRules");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Name).IsRequired().HasMaxLength(256);
            entity.Property(r => r.Channel).HasConversion<string?>().HasMaxLength(16);
            entity.Property(r => r.Language).HasMaxLength(64);
            entity.Property(r => r.RequiredSkill).HasMaxLength(128);
            entity.HasIndex(r => r.SortOrder);
        });

        modelBuilder.Entity<EscalationRule>(entity =>
        {
            entity.ToTable("EscalationRules");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Name).IsRequired().HasMaxLength(256);
            entity.Property(r => r.Priority).HasConversion<string?>().HasMaxLength(16);
            entity.HasIndex(r => r.SortOrder);
        });

        modelBuilder.Entity<EscalationTier>(entity =>
        {
            entity.ToTable("EscalationTiers");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.RaisePriorityTo).HasConversion<string?>().HasMaxLength(16);
            entity.HasIndex(t => new { t.EscalationRuleId, t.TierNumber }).IsUnique();
        });

        modelBuilder.Entity<EscalationLogEntry>(entity =>
        {
            entity.ToTable("EscalationLog");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ActionSummary).IsRequired();
            entity.HasIndex(e => e.TicketId);
            // A tier fires at most once per ticket — enforced here, not just in application
            // logic, so a race between two overlapping evaluation runs can't double-fire it.
            entity.HasIndex(e => new { e.TicketId, e.EscalationRuleId, e.TierNumber }).IsUnique();
        });

        modelBuilder.Entity<AlertPreference>(entity =>
        {
            entity.ToTable("AlertPreferences");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.DigestFrequency).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.HasIndex(p => p.AgentId).IsUnique();
        });

        modelBuilder.Entity<SlaAlertLog>(entity =>
        {
            entity.ToTable("SlaAlertLog");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Kind).IsRequired().HasMaxLength(16);
            // A ticket's Warning/Breach alert fires at most once each — enforced here too, not
            // just in SlaAlertService.SendOnceAsync, so a racing tick can't double-send.
            entity.HasIndex(e => new { e.TicketId, e.Kind }).IsUnique();
        });

        modelBuilder.Entity<DigestLogEntry>(entity =>
        {
            entity.ToTable("DigestLog");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AgentId);
        });

        modelBuilder.Entity<KbCategory>(entity =>
        {
            entity.ToTable("KbCategories");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.NameEn).HasMaxLength(256);
            entity.Property(c => c.NameAr).HasMaxLength(256);
        });

        modelBuilder.Entity<Faq>(entity =>
        {
            entity.ToTable("Faqs");
            entity.HasKey(f => f.Id);
            entity.Property(f => f.QuestionEn).HasMaxLength(512);
            entity.Property(f => f.QuestionAr).HasMaxLength(512);
            entity.HasIndex(f => f.KbCategoryId);
        });

        modelBuilder.Entity<Article>(entity =>
        {
            entity.ToTable("Articles");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.TitleEn).HasMaxLength(512);
            entity.Property(a => a.TitleAr).HasMaxLength(512);
            entity.Property(a => a.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(a => a.AuthorName).IsRequired().HasMaxLength(256);
            entity.Property(a => a.LastUpdatedByName).IsRequired().HasMaxLength(256);
            entity.Property(a => a.HasBeenPublished).IsRequired();
            entity.HasIndex(a => a.KbCategoryId);
            entity.HasIndex(a => a.Status);
        });

        modelBuilder.Entity<ArticleAttachment>(entity =>
        {
            entity.ToTable("ArticleAttachments");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.FileName).IsRequired().HasMaxLength(512);
            entity.Property(a => a.ContentType).IsRequired().HasMaxLength(128);
            entity.Property(a => a.StorageKey).IsRequired().HasMaxLength(1024);
            entity.Property(a => a.UploadedByName).IsRequired().HasMaxLength(256);
            entity.HasIndex(a => a.ArticleId);
        });

        modelBuilder.Entity<Guide>(entity =>
        {
            entity.ToTable("Guides");
            entity.HasKey(g => g.Id);
            entity.Property(g => g.TitleEn).HasMaxLength(512);
            entity.Property(g => g.TitleAr).HasMaxLength(512);
            entity.Property(g => g.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(g => g.AuthorName).IsRequired().HasMaxLength(256);
            entity.Property(g => g.LastUpdatedByName).IsRequired().HasMaxLength(256);
            entity.Property(g => g.VideoUrl).HasMaxLength(1024);
            entity.Property(g => g.HasBeenPublished).IsRequired();
            entity.HasIndex(g => g.Status);
        });

        modelBuilder.Entity<GuideAttachment>(entity =>
        {
            entity.ToTable("GuideAttachments");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.FileName).IsRequired().HasMaxLength(512);
            entity.Property(a => a.ContentType).IsRequired().HasMaxLength(128);
            entity.Property(a => a.StorageKey).IsRequired().HasMaxLength(1024);
            entity.Property(a => a.UploadedByName).IsRequired().HasMaxLength(256);
            entity.HasIndex(a => a.GuideId);
        });

        modelBuilder.Entity<GuideTicketCategory>(entity =>
        {
            entity.ToTable("GuideTicketCategories");
            entity.HasKey(l => l.Id);
            entity.HasIndex(l => new { l.GuideId, l.TicketCategoryId }).IsUnique();
        });

        modelBuilder.Entity<SearchLog>(entity =>
        {
            entity.ToTable("SearchLogs");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Query).IsRequired().HasMaxLength(512);
            entity.HasIndex(s => s.ResultCount);
        });

        modelBuilder.Entity<ContentVersionEntry>(entity =>
        {
            entity.ToTable("ContentVersions");
            entity.HasKey(v => v.Id);
            entity.Property(v => v.ContentType).IsRequired().HasMaxLength(16);
            entity.Property(v => v.ChangedBy).IsRequired().HasMaxLength(256);
            entity.HasIndex(v => new { v.ContentType, v.ContentId, v.VersionNumber }).IsUnique();
        });

        modelBuilder.Entity<TicketAiSummary>(entity =>
        {
            entity.ToTable("TicketAiSummaries");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.SummaryText).IsRequired();
            entity.HasIndex(s => s.TicketId).IsUnique();
        });

        modelBuilder.Entity<TicketCategorizationSuggestion>(entity =>
        {
            entity.ToTable("TicketCategorizationSuggestions");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.SuggestedPriority).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.HasIndex(s => s.TicketId).IsUnique();
        });

        modelBuilder.Entity<SolutionSuggestionFeedback>(entity =>
        {
            entity.ToTable("SolutionSuggestionFeedback");
            entity.HasKey(f => f.Id);
            entity.Property(f => f.ContentType).IsRequired().HasMaxLength(16);
            entity.Property(f => f.FlaggedByName).IsRequired().HasMaxLength(256);
            entity.HasIndex(f => new { f.ContentType, f.ContentId });
        });

        modelBuilder.Entity<FaqPortalImpression>(entity =>
        {
            entity.ToTable("FaqPortalImpressions");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.DraftSessionId).IsRequired().HasMaxLength(64);
            entity.HasIndex(i => i.FaqId);
            entity.HasIndex(i => i.DraftSessionId);
        });

        modelBuilder.Entity<TicketFeedback>(entity =>
        {
            entity.ToTable("TicketFeedback");
            entity.HasKey(f => f.Id);
            entity.HasIndex(f => f.TicketId).IsUnique();
        });

        // Security & Administration — seed GUIDs and shared seed data below.
        var seedTimestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var defaultAdminUserId = new Guid("55555555-5555-5555-5555-555555555501");
        var agentRoleId = new Guid("55555555-5555-5555-5555-555555555502");
        var teamLeadRoleId = new Guid("55555555-5555-5555-5555-555555555503");
        var managerRoleId = new Guid("55555555-5555-5555-5555-555555555504");
        var adminRoleId = new Guid("55555555-5555-5555-5555-555555555505");

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.MfaSecret).HasMaxLength(64);

            // Bootstraps the system — without a seeded admin, nobody could ever log in to create
            // the first user. Dev-only credential (admin@supportcrm.local / ChangeMe123!) —
            // change it immediately in any real deployment.
            //
            // The hash below is a FIXED, pre-computed PasswordHasher<T> output for "ChangeMe123!"
            // (verified to round-trip via VerifyHashedPassword) — NOT computed inline here.
            // PasswordHasher<T>.HashPassword salts randomly on every call, so calling it live
            // inside OnModelCreating makes the seed value change on every build, which EF Core
            // reports as a permanently "pending model change" (the model can never stabilize
            // against a snapshot). A hardcoded literal is required for HasData to be reproducible.
            entity.HasData(new
            {
                Id = defaultAdminUserId,
                Email = "admin@supportcrm.local",
                PasswordHash = "AQAAAAIAAYagAAAAENB/BIDiPZplJMqE54eutY2QGsuAYNAxN4m/ltMy75o9lbelpJ2Op7u7DEm+O9vRXA==",
                IsActive = true,
                MfaEnabled = false,
                MfaSecret = (string?)null,
                PasswordChangedAtUtc = seedTimestamp,
                CreatedAtUtc = seedTimestamp,
                FailedLoginAttempts = 0,
                LockedUntilUtc = (DateTimeOffset?)null
            });
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Name).IsRequired().HasMaxLength(128);
            entity.HasIndex(r => r.Name).IsUnique();

            entity.HasData(
                new { Id = agentRoleId, Name = "Agent", IsSystemDefined = true },
                new { Id = teamLeadRoleId, Name = "Team Lead", IsSystemDefined = true },
                new { Id = managerRoleId, Name = "Manager", IsSystemDefined = true },
                new { Id = adminRoleId, Name = "Admin", IsSystemDefined = true }
            );
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles");
            entity.HasKey(ur => ur.Id);
            entity.HasIndex(ur => new { ur.UserId, ur.RoleId }).IsUnique();

            entity.HasData(new { Id = new Guid("55555555-5555-5555-5555-555555555601"), UserId = defaultAdminUserId, RoleId = adminRoleId });
        });

        var permissionModules = new[] { "Tickets", "Customers", "KnowledgeBase", "Sla", "Ai", "CustomerPortal", "Reports", "Administration", "Integrations" };
        var permissionActions = new[] { "View", "Create", "Edit", "Delete", "Export" };
        var permissionSeed = new List<(Guid Id, string Module, string Action)>();
        var seedIndex = 0;
        foreach (var module in permissionModules)
            foreach (var action in permissionActions)
                permissionSeed.Add((Guid.Parse($"66666666-6666-6666-6666-{seedIndex++:D12}"), module, action));

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("Permissions");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Module).IsRequired().HasMaxLength(64);
            entity.Property(p => p.Action).IsRequired().HasMaxLength(32);
            entity.HasIndex(p => new { p.Module, p.Action }).IsUnique();

            entity.HasData(permissionSeed.Select(p => new { p.Id, p.Module, p.Action }));
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("RolePermissions");
            entity.HasKey(rp => rp.Id);
            entity.HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();

            // The seeded Admin role starts with every permission — every other seeded role
            // (Agent/Team Lead/Manager) starts with none, assigned deliberately by an admin.
            var grantIndex = 0;
            entity.HasData(permissionSeed.Select(p => new { Id = Guid.Parse($"77777777-7777-7777-7777-{grantIndex++:D12}"), RoleId = adminRoleId, PermissionId = p.Id }));
        });

        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.ToTable("AuditLogEntries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserEmail).IsRequired().HasMaxLength(256);
            entity.Property(e => e.HttpMethod).IsRequired().HasMaxLength(16);
            entity.Property(e => e.Path).IsRequired().HasMaxLength(512);
            entity.Property(e => e.ActionSummary).IsRequired().HasMaxLength(256);
            entity.Property(e => e.IpAddress).HasMaxLength(64);
            entity.HasIndex(e => e.OccurredAtUtc);
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.ToTable("SystemSettings");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Key).IsRequired().HasMaxLength(128);
            entity.HasIndex(s => s.Key).IsUnique();
            entity.Property(s => s.Value).IsRequired();
            entity.Property(s => s.UpdatedBy).IsRequired().HasMaxLength(256);
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("Departments");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Name).IsRequired().HasMaxLength(256);
            entity.Property(d => d.DefaultForChannel).HasConversion<string?>().HasMaxLength(16);
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.ToTable("Branches");
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Name).IsRequired().HasMaxLength(256);
            entity.Property(b => b.Code).IsRequired().HasMaxLength(32);
            entity.HasIndex(b => b.Code).IsUnique();
            entity.Property(b => b.DefaultLanguage).HasMaxLength(4);
            entity.Property(b => b.ContactNumber).HasMaxLength(64);
        });

        modelBuilder.Entity<BrandingSettings>(entity =>
        {
            entity.ToTable("BrandingSettings");
            entity.HasKey(b => b.Id);
            entity.Property(b => b.PrimaryColorHex).IsRequired().HasMaxLength(7);
            entity.Property(b => b.SecondaryColorHex).IsRequired().HasMaxLength(7);
            entity.Property(b => b.LogoStorageKey).HasMaxLength(512);
            entity.Property(b => b.LogoContentType).HasMaxLength(128);
            entity.Property(b => b.UpdatedBy).IsRequired().HasMaxLength(256);
            entity.HasIndex(b => b.BranchId).IsUnique();
        });

        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.ToTable("ApiKeys");
            entity.HasKey(k => k.Id);
            entity.Property(k => k.Name).IsRequired().HasMaxLength(256);
            entity.Property(k => k.KeyHash).IsRequired().HasMaxLength(128);
            entity.HasIndex(k => k.KeyHash).IsUnique();
            entity.Property(k => k.Scopes).IsRequired();
            entity.Property(k => k.CreatedBy).IsRequired().HasMaxLength(256);
        });

        modelBuilder.Entity<WebhookSubscription>(entity =>
        {
            entity.ToTable("WebhookSubscriptions");
            entity.HasKey(w => w.Id);
            entity.Property(w => w.Url).IsRequired().HasMaxLength(2048);
            entity.Property(w => w.Secret).IsRequired().HasMaxLength(128);
            entity.Property(w => w.EventTypes).IsRequired();
        });

        modelBuilder.Entity<WebhookDeliveryLog>(entity =>
        {
            entity.ToTable("WebhookDeliveryLogs");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.EventType).IsRequired().HasMaxLength(64);
            entity.Property(d => d.PayloadJson).IsRequired();
            entity.Property(d => d.ErrorMessage).HasMaxLength(2048);
            entity.HasIndex(d => d.WebhookSubscriptionId);
        });

        modelBuilder.Entity<IntegrationConnector>(entity =>
        {
            entity.ToTable("IntegrationConnectors");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Type).HasConversion<string>().HasMaxLength(32);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(256);
            entity.Property(c => c.ConfigJson).IsRequired();
        });

        modelBuilder.Entity<ErpSyncLog>(entity =>
        {
            entity.ToTable("ErpSyncLogs");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(l => l.Message).IsRequired().HasMaxLength(2048);
            entity.HasIndex(l => l.CustomerId);
        });

        modelBuilder.Entity<ErpSyncState>(entity =>
        {
            entity.ToTable("ErpSyncStates");
            entity.HasKey(s => s.CustomerId);
            entity.Property(s => s.LastSyncedRemoteCompany).HasMaxLength(256);
            entity.Property(s => s.LastSyncedLocalCompany).HasMaxLength(256);
        });
    }
}
