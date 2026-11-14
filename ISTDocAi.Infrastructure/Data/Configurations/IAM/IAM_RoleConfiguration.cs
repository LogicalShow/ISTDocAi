using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ISTDocAi.Infrastructure.IAM.Models;

namespace ISTDocAi.Infrastructure.Data.Configurations.IAM;

public class IAM_RoleConfiguration : IEntityTypeConfiguration<IAM_Role>
{
    public void Configure(EntityTypeBuilder<IAM_Role> builder)
    {
        builder.ToTable("IAM_Role");
    }
}
