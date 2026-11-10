using JaCrm.Api.Domain;

namespace JaCrm.UnitTests.Domain;

public sealed class CompanyMatchingTests
{
    [Theory]
    [InlineData("Acme", "acme")]
    [InlineData("ACME, Inc.", "acme")]
    [InlineData("  Acme   Ltd ", "acme")]
    [InlineData("Acme LLC", "acme")]
    [InlineData("Alpenwerk Software AG", "alpenwerksoftware")]
    [InlineData("Kanal Logistics B.V.", "kanallogistics")]
    [InlineData("Dunav Code d.o.o.", "dunavcode")]
    [InlineData("Dunav-Code DOO", "dunavcode")]
    [InlineData("Vela Payments GmbH & Co. KG", "velapayments")]
    [InlineData("Société Générale SA", "societegenerale")]
    public void Names_ignore_case_whitespace_punctuation_and_legal_suffixes(string name, string expected) =>
        Assert.Equal(expected, CompanyMatching.NormalizeName(name));

    [Fact]
    public void A_name_that_is_only_a_suffix_is_kept() =>
        Assert.Equal("ag", CompanyMatching.NormalizeName("AG"));

    [Fact]
    public void Suffixes_are_only_removed_at_the_end() =>
        Assert.Equal("incredibleapps", CompanyMatching.NormalizeName("Inc Redible Apps"));

    [Theory]
    [InlineData("https://www.acme.com/careers", "acme.com")]
    [InlineData("http://acme.com", "acme.com")]
    [InlineData("acme.com", "acme.com")]
    [InlineData("WWW.Acme.COM", "acme.com")]
    [InlineData("https://jobs.acme.com", "jobs.acme.com")]
    public void Domains_ignore_protocol_www_and_path(string website, string expected) =>
        Assert.Equal(expected, CompanyMatching.NormalizeDomain(website));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_websites_have_no_domain(string? website) =>
        Assert.Null(CompanyMatching.NormalizeDomain(website));
}
