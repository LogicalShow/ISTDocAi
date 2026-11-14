using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ISTDocAi.Domain.Entities.IAM;
using ISTDocAi.Domain.Entities.Public;
using ISTDocAi.Infrastructure.Data.Configurations.Common;
using ISTDocAi.Infrastructure.IAM.Models;

namespace ISTDocAi.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<IAM_User, IAM_Role, string>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<IAM_Application> IAM_Applications { get; set; }
    public DbSet<IAM_Module> IAM_Modules { get; set; }
    public DbSet<IAM_Page> IAM_Pages { get; set; }
    public DbSet<IAM_Action> IAM_Actions { get; set; }
    public DbSet<IAM_PageAction> IAM_PageActions { get; set; }
    public DbSet<IAM_PermissionAudit> IAM_PermissionAudits { get; set; }

    #region Public

    public DbSet<Pub_ActionCategory> Pub_ActionCategory { get; set; }
    public DbSet<Pub_ActionType> Pub_ActionType { get; set; }
    public DbSet<Pub_ActionLog> Pub_ActionLog { get; set; }
    public DbSet<Pub_Attachment> Pub_Attachments { get; set; }
    public DbSet<Pub_ErrorLog> Pub_ErrorLog { get; set; }
    public DbSet<Pub_Notification> Pub_Notifications { get; set; }
    public DbSet<Pub_NotificationUser> Pub_NotificationUsers { get; set; }
    public DbSet<Pub_NotificationUserDetail> Pub_NotificationUserDetails { get; set; }
    public DbSet<Pub_RequestLog> Pub_RequestLog { get; set; }
    public DbSet<Pub_ScheduledTask> Pub_ScheduledTasks { get; set; }
    public DbSet<Pub_ScheduledTaskLog> Pub_ScheduledTaskLogs { get; set; }
    public DbSet<Pub_Setting> Pub_Setting { get; set; }

    #endregion

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureDomainEntityConventions();
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly,
            type => type.Namespace != "ISTDocAi.Infrastructure.Data.Configurations.DocAi");

        builder.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys())
            .ToList()
            .ForEach(fk => fk.DeleteBehavior = DeleteBehavior.Restrict);
    }

}
