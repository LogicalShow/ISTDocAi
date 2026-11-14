using ISTDocAi.Application.Features.Public.Attachment.Queries.AttachmentGetById;
using ISTDocAi.Application.Features.Public.NotificationUser.Commands.NotificationUserUpsert.Contracts;
using ISTDocAi.Application.Services.Public.SettingService.Contracts;
using ISTDocAi.Domain.Common.Enums;
using ISTDocAi.Domain.Entities.Public;
using Mapster;
using MapsterMapper;

namespace ISTDocAi.Application.Tests.TestCases.Mappings;

[TestFixture]
public sealed class MapsterConfigurationTests
{
    private TypeAdapterConfig _config = null!;
    private IMapper _mapper = null!;

    [SetUp]
    public void SetUp()
    {
        _config = new TypeAdapterConfig();
        _config.Scan(typeof(DependencyInjection).Assembly);
        _mapper = new Mapper(_config);
    }

    [Test]
    public void Configuration_ShouldCompile()
    {
        Assert.DoesNotThrow(() => _config.Compile());
    }

    [Test]
    public void SettingUpdate_ShouldPreserveIgnoredFileProperties()
    {
        var originalLogo = new byte[] { 1, 2, 3 };
        var destination = new Pub_Setting
        {
            CompanyNameEn = "Before",
            CompanyAddress = "Address",
            CompanyPhoneOne = "1",
            DefaultCurrencyId = "USD",
            CompanyLogoPath = "logo.png",
            CompanyLogoHash = "logo-hash",
            CompanyLogoBinaryData = originalLogo,
            CompanyLogoContentType = "image/png"
        };
        var source = new UpdateSetting
        {
            CompanyNameEn = "After",
            CompanyAddress = "New address",
            CompanyPhoneOne = "2",
            DefaultCurrencyId = "EUR",
            CompanyLogoPath = "replacement.png",
            CompanyLogoHash = "replacement-hash",
            CompanyLogoBinaryData = new byte[] { 9 },
            CompanyLogoContentType = "image/jpeg"
        };

        _mapper.Map(source, destination);

        Assert.Multiple(() =>
        {
            Assert.That(destination.CompanyNameEn, Is.EqualTo("After"));
            Assert.That(destination.CompanyLogoPath, Is.EqualTo("logo.png"));
            Assert.That(destination.CompanyLogoHash, Is.EqualTo("logo-hash"));
            Assert.That(destination.CompanyLogoBinaryData, Is.SameAs(originalLogo));
            Assert.That(destination.CompanyLogoContentType, Is.EqualTo("image/png"));
        });
    }

    [Test]
    public void NotificationUserUpdate_ShouldMapOntoExistingEntity()
    {
        var destination = new Pub_NotificationUser
        {
            Id = "row-id",
            NotificationId = "notification-id",
            UserId = "old-user",
            SendingWay = SendingWay.System
        };
        var source = new UpdatedNotificationUser
        {
            Id = "row-id",
            UserId = "new-user",
            SendingWayId = (int)SendingWay.Both
        };

        _mapper.Map(source, destination);

        Assert.Multiple(() =>
        {
            Assert.That(destination, Has.Property(nameof(Pub_NotificationUser.Id)).EqualTo("row-id"));
            Assert.That(destination.NotificationId, Is.EqualTo("notification-id"));
            Assert.That(destination.UserId, Is.EqualTo("new-user"));
            Assert.That(destination.SendingWay, Is.EqualTo(SendingWay.Both));
        });
    }

    [Test]
    public void AttachmentResponse_ShouldPopulateConstructorValues()
    {
        var source = new Pub_Attachment
        {
            Id = "attachment-id",
            RowVersion = new byte[] { 1 },
            StoredFileName = "stored.pdf",
            OriginalFileName = "original.pdf",
            FilePath = "/files/stored.pdf",
            FileSizeInBytes = 42,
            ReferenceType = "Document",
            ReferenceId = "reference-id",
            ReferenceSerial = "DOC-42"
        };

        var destination = _mapper.Map<AttachmentGetByIdQueryResponse>(source);

        Assert.Multiple(() =>
        {
            Assert.That(destination.Id, Is.EqualTo("attachment-id"));
            Assert.That(destination.ReferenceSerial, Is.EqualTo("DOC-42"));
        });
    }

    [Test]
    public void DateTime_ShouldConvertBetweenLocalAndUtc_WhenConfiguredExplicitly()
    {
        var config = new TypeAdapterConfig();
        config.NewConfig<LocalDateDummy, UtcDateDummy>()
            .Map(destination => destination.Value, source => source.Value.ToUniversalTime());
        config.NewConfig<UtcDateDummy, LocalDateDummy>()
            .Map(destination => destination.Value, source => source.Value.ToLocalTime());
        config.Compile();

        var mapper = new Mapper(config);
        var localValue = new DateTime(2026, 9, 17, 10, 30, 0, DateTimeKind.Local);

        var utcResult = mapper.Map<UtcDateDummy>(new LocalDateDummy { Value = localValue });
        var localResult = mapper.Map<LocalDateDummy>(utcResult);

        Assert.Multiple(() =>
        {
            Assert.That(utcResult.Value, Is.EqualTo(localValue.ToUniversalTime()));
            Assert.That(utcResult.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(localResult.Value, Is.EqualTo(utcResult.Value.ToLocalTime()));
            Assert.That(localResult.Value.Kind, Is.EqualTo(DateTimeKind.Local));
        });
    }

    private sealed class LocalDateDummy
    {
        public DateTime Value { get; init; }
    }

    private sealed class UtcDateDummy
    {
        public DateTime Value { get; init; }
    }
}
