namespace SupportCrm.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SupportCrm.Application.Ai;
using SupportCrm.Application.CustomerPortal;
using SupportCrm.Application.Customers;
using SupportCrm.Application.Integrations;
using SupportCrm.Application.KnowledgeBase;
using SupportCrm.Application.Platform;
using SupportCrm.Application.Reports;
using SupportCrm.Application.Security;
using SupportCrm.Application.Sla;
using SupportCrm.Application.Tickets;
using SupportCrm.Domain.Repositories;
using SupportCrm.Infrastructure.Persistence;
using SupportCrm.Infrastructure.Reports;
using SupportCrm.Infrastructure.Storage;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // --- DbContext -----------------------------------------------------------------
        services.AddDbContext<SupportCrmDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        // --- HTTP clients (required by WebhookService and its consumers) ---------------
        services.AddHttpClient();

        // --- Repositories (Domain.Repositories -> Infrastructure.Persistence) ----------
        services.AddScoped<IAgentNotificationRepository, AgentNotificationRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<IAlertPreferenceRepository, AlertPreferenceRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IArticleAttachmentRepository, ArticleAttachmentRepository>();
        services.AddScoped<IArticleRepository, ArticleRepository>();
        services.AddScoped<IAssignmentRuleRepository, AssignmentRuleRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IBrandingSettingsRepository, BrandingSettingsRepository>();
        services.AddScoped<IBusinessCalendarRepository, BusinessCalendarRepository>();
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IContentVersionRepository, ContentVersionRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IErpSyncRepository, ErpSyncRepository>();
        services.AddScoped<IEscalationRuleRepository, EscalationRuleRepository>();
        services.AddScoped<IFaqPortalImpressionRepository, FaqPortalImpressionRepository>();
        services.AddScoped<IFaqRepository, FaqRepository>();
        services.AddScoped<IGuideAttachmentRepository, GuideAttachmentRepository>();
        services.AddScoped<IGuideRepository, GuideRepository>();
        services.AddScoped<IIntegrationConnectorRepository, IntegrationConnectorRepository>();
        services.AddScoped<IKbCategoryRepository, KbCategoryRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IQuickReplyTemplateRepository, QuickReplyTemplateRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ISearchLogRepository, SearchLogRepository>();
        services.AddScoped<ISlaTargetRepository, SlaTargetRepository>();
        services.AddScoped<ISolutionSuggestionFeedbackRepository, SolutionSuggestionFeedbackRepository>();
        services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<ITicketAiSummaryRepository, TicketAiSummaryRepository>();
        services.AddScoped<ITicketAttachmentRepository, TicketAttachmentRepository>();
        services.AddScoped<ITicketCategorizationSuggestionRepository, TicketCategorizationSuggestionRepository>();
        services.AddScoped<ITicketCategoryRepository, TicketCategoryRepository>();
        services.AddScoped<ITicketCollaboratorRepository, TicketCollaboratorRepository>();
        services.AddScoped<ITicketFeedbackRepository, TicketFeedbackRepository>();
        services.AddScoped<ITicketMessageRepository, TicketMessageRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<ITicketTaskRepository, TicketTaskRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IWebFormFieldDefinitionRepository, WebFormFieldDefinitionRepository>();
        services.AddScoped<IWebhookRepository, WebhookRepository>();

        // Repositories that live under Application/Customers (interfaces only) but are
        // implemented in Infrastructure.Persistence, same as the ones above.
        services.AddScoped<IContactDetailRepository, ContactDetailRepository>();
        services.AddScoped<INoteAndAttachmentRepository, NoteAndAttachmentRepository>();

        // --- Storage abstractions (Application interfaces -> Infrastructure.Storage LocalDisk*) ---
        services.AddScoped<IAttachmentStorage, LocalDiskAttachmentStorage>();
        services.AddScoped<IArticleAttachmentStorage, LocalDiskArticleAttachmentStorage>();
        services.AddScoped<IGuideAttachmentStorage, LocalDiskGuideAttachmentStorage>();
        services.AddScoped<IBrandingAssetStorage, LocalDiskBrandingAssetStorage>();
        services.AddScoped<ITicketAttachmentStorage, LocalDiskTicketAttachmentStorage>();

        // --- Reports ---------------------------------------------------------------------
        services.AddScoped<IReportExporter, ReportExporter>();

        // --- AI provider seams (mock implementations until a real provider exists) -------
        services.AddScoped<IAiCategorizationProvider, MockAiCategorizationProvider>();
        services.AddScoped<IAiChatbotProvider, MockAiChatbotProvider>();
        services.AddScoped<IAiReplyDraftProvider, MockAiReplyDraftProvider>();
        services.AddScoped<IAiSummaryProvider, MockAiSummaryProvider>();

        // --- External system connectors (both feed IEnumerable<IExternalSystemConnector>) ---
        services.AddScoped<IExternalSystemConnector, MockBillingConnector>();
        services.AddScoped<IExternalSystemConnector, MockErpConnector>();

        // --- Customer interaction sources (both feed IEnumerable<ICustomerInteractionSource>) ---
        services.AddScoped<ICustomerInteractionSource, NotesInteractionSource>();
        services.AddScoped<ICustomerInteractionSource, TicketInteractionSource>();

        // CustomerActivitySummaryProvider (real, asks every ICustomerInteractionSource) supersedes
        // StubCustomerActivitySummaryProvider — see the doc comment on CustomerActivitySummaryProvider.
        services.AddScoped<ICustomerActivitySummaryProvider, CustomerActivitySummaryProvider>();

        // --- Notification / messaging seams ----------------------------------------------
        services.AddScoped<IAssignmentNotifier, NoOpAssignmentNotifier>();
        // No real notification channel exists yet — see the doc comment on ICustomerStatusNotifier.
        services.AddScoped<ICustomerStatusNotifier, NoOpCustomerStatusNotifier>();
        services.AddScoped<IEmailSender, MockEmailSender>();
        services.AddScoped<ISlaAlertNotifier, NoOpSlaAlertNotifier>();
        services.AddScoped<ISmsSender, MockSmsSender>();
        services.AddScoped<IWhatsAppSender, MockWhatsAppSender>();

        // --- Security -----------------------------------------------------------------------
        services.AddScoped<IPermissionChecker, PermissionChecker>();
        services.AddScoped<PasswordHashingService>();
        services.AddScoped<PasswordPolicyValidator>();
        services.AddScoped<TotpService>();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<AuditLogService>();
        services.AddScoped<RoleManagementService>();
        services.AddScoped<SystemSettingsService>();
        services.AddScoped<UserManagementService>();

        // --- Ai -----------------------------------------------------------------------------
        services.AddScoped<AiChatbotService>();
        services.AddScoped<AiReplyDraftService>();
        services.AddScoped<TicketCategorizationService>();
        services.AddScoped<TicketSolutionSuggestionService>();
        services.AddScoped<TicketSummaryService>();

        // --- CustomerPortal -------------------------------------------------------------------
        services.AddScoped<CustomerPortalTicketService>();
        services.AddScoped<FaqPortalAnalyticsService>();
        services.AddScoped<TicketFeedbackService>();

        // --- Customers ------------------------------------------------------------------------
        services.AddScoped<ContactDetailService>();
        services.AddScoped<CustomerAgentPanelService>();
        services.AddScoped<CustomerProfileService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<CustomerTimelineService>();
        services.AddScoped<NoteAndAttachmentService>();

        // --- Integrations ---------------------------------------------------------------------
        services.AddScoped<ApiKeyService>();
        services.AddScoped<ErpSyncService>();
        services.AddScoped<ExternalApiService>();
        services.AddScoped<ExternalDataService>();
        services.AddScoped<IntegrationConnectorService>();
        services.AddScoped<WebhookService>();

        // --- KnowledgeBase ----------------------------------------------------------------------
        services.AddScoped<ArticleAttachmentService>();
        services.AddScoped<ArticleService>();
        services.AddScoped<ContentWorkflowService>();
        services.AddScoped<FaqService>();
        services.AddScoped<GuideAttachmentService>();
        services.AddScoped<GuideService>();
        services.AddScoped<KbCategoryService>();
        services.AddScoped<KbSearchService>();

        // --- Platform -------------------------------------------------------------------------
        services.AddScoped<BranchService>();
        services.AddScoped<BrandingService>();
        services.AddScoped<DepartmentService>();
        services.AddScoped<TicketDepartmentRoutingService>();

        // --- Reports --------------------------------------------------------------------------
        services.AddScoped<AgentPerformanceService>();
        services.AddScoped<CsatReportService>();
        services.AddScoped<ManagementDashboardService>();
        services.AddScoped<SlaComplianceService>();
        services.AddScoped<TicketReportService>();

        // --- Sla ------------------------------------------------------------------------------
        services.AddScoped<BusinessCalendarConfigService>();
        services.AddScoped<BusinessCalendarService>();
        services.AddScoped<SlaCalculationService>();
        services.AddScoped<SlaTargetService>();

        // --- Tickets --------------------------------------------------------------------------
        services.AddScoped<AgentDashboardService>();
        services.AddScoped<AgentNotificationService>();
        services.AddScoped<AgentService>();
        services.AddScoped<AssignmentRuleEngine>();
        services.AddScoped<AssignmentRuleService>();
        services.AddScoped<ChannelReplyDispatcher>();
        services.AddScoped<ChatService>();
        services.AddScoped<EmailChannelService>();
        services.AddScoped<EscalationRuleEngine>();
        services.AddScoped<EscalationRuleService>();
        services.AddScoped<QuickReplyTemplateService>();
        services.AddScoped<SlaAlertService>();
        services.AddScoped<SmsChannelService>();
        services.AddScoped<TeamService>();
        services.AddScoped<TicketAssignmentService>();
        services.AddScoped<TicketAttachmentService>();
        services.AddScoped<TicketCategoryService>();
        services.AddScoped<TicketCollaborationService>();
        services.AddScoped<TicketCustomerResolver>();
        services.AddScoped<TicketEscalationService>();
        services.AddScoped<TicketIngestionService>();
        services.AddScoped<TicketMessageService>();
        services.AddScoped<TicketService>();
        services.AddScoped<TicketTaskService>();
        services.AddScoped<TicketTimelineService>();
        services.AddScoped<WebFormFieldDefinitionService>();
        services.AddScoped<WebFormSubmissionService>();
        services.AddScoped<WhatsAppChannelService>();

        // --- Hosted services --------------------------------------------------------------------
        services.AddHostedService<ErpSyncHostedService>();
        services.AddHostedService<SlaEscalationHostedService>();

        return services;
    }
}
