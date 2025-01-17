// Copyright Henrik Widlund
// GNU General Public License v3.0

using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace HostsParser.Tests;

public sealed class SettingsTests
{
    [Fact]
    public async Task Settings_Should_Be_Deserialized_From_AppSettings()
    {
        // Arrange
        await using var fileStream = File.OpenRead("appsettings.json");

        // Act
        var settings = await JsonSerializer.DeserializeAsync<Settings>(fileStream);

        // Assert
        settings.Should().NotBeNull();
        settings.ExtraFiltering.Should().BeTrue();
        settings.HeaderLines.Should().ContainSingle();
        settings.Filters.Should().NotBeNull();
        settings.Filters.SkipLines.Should().NotBeNull();
        settings.Filters.SkipLines.Should().ContainSingle();
        settings.Filters.SkipLinesBytes.Should().NotBeNull();
        settings.Filters.SkipLinesBytes.Should().ContainSingle();
        settings.Filters.Sources.Should().NotBeNullOrEmpty();
        settings.Filters.Sources.Should().HaveCount(2);

        var hostsSource = settings.Filters.Sources.Should()
            .ContainSingle(static item => item.Format == SourceFormat.Hosts).Subject;
        hostsSource.Uri.Should().Be("https://hosts-based.uri");
        hostsSource.Prefix.Should().Be("0.0.0.0 ");
        hostsSource.SourceAction.Should().Be(SourceAction.Combine);
        hostsSource.SourcePrefix.PrefixBytes.Should().NotBeNull();
        hostsSource.SourcePrefix.PrefixBytes.Should().BeEquivalentTo(Encoding.UTF8.GetBytes(hostsSource.Prefix));
        hostsSource.SourcePrefix.WwwPrefixBytes.Should()
            .BeEquivalentTo(Encoding.UTF8.GetBytes(hostsSource.Prefix + "www."));

        var adBlockSource = settings.Filters.Sources.Should()
            .ContainSingle(static item => item.Format == SourceFormat.AdBlock).Subject;
        adBlockSource.Uri.Should().Be("https://adblock-based.uri");
        adBlockSource.Prefix.Should().BeEmpty();
        adBlockSource.SourceAction.Should().Be(SourceAction.ExternalCoverage);
        adBlockSource.SourcePrefix.PrefixBytes.Should().BeNull();
        adBlockSource.SourcePrefix.PrefixBytes.Should().BeNull();

        settings.KnownBadHosts.Should().NotBeNull();
        settings.KnownBadHosts.Should().ContainSingle();
    }
}
