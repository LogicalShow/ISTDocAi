using ISTDocAi.Domain.Entities.Public;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.Public.Setting;

public class Pub_SettingConfiguration:IEntityTypeConfiguration<Pub_Setting>
{
    public void Configure(EntityTypeBuilder<Pub_Setting> builder)
    {
        builder.Property(x => x.IsEmailEnabled)
               .HasDefaultValue(false);

        builder.Property(x => x.AttachmentFolderPath)
               .HasMaxLength(500);

        builder.Property(x => x.CompanyLogoBinaryData)
       .HasColumnType("varbinary(max)");
        builder.Property(x => x.ReportHeaderBinaryData)
            .HasColumnType("varbinary(max)");
        builder.Property(x => x.ReportFooterBinaryData)
            .HasColumnType("varbinary(max)");

    }   

}
