using System;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    public class Level5BackendV2ConfigTests
    {
        [TearDown]
        public void TearDown()
        {
            BackendV2ApiConfigProvider.Reset();
        }

        [Test]
        public void ARelativeBaseUriIsRejected()
        {
            Assert.Throws<ArgumentException>(() =>
                new BackendV2ApiConfig(new Uri("api/v2", UriKind.Relative), BackendV2Environment.Production));
        }

        [Test]
        public void ANonHttpsBaseUriIsRejectedUnlessInsecureLocalhostIsAllowed()
        {
            Assert.Throws<ArgumentException>(() =>
                new BackendV2ApiConfig(new Uri("http://example.com/"), BackendV2Environment.Production));

            Assert.DoesNotThrow(() =>
                new BackendV2ApiConfig(
                    new Uri("http://localhost:5053/"),
                    BackendV2Environment.Development,
                    allowInsecureLocalhost: true));
        }

        [Test]
        public void InsecureLocalhostIsNotGrantedToANonLocalHost()
        {
            Assert.Throws<ArgumentException>(() =>
                new BackendV2ApiConfig(
                    new Uri("http://example.com/"),
                    BackendV2Environment.Production,
                    allowInsecureLocalhost: true));
        }

        [Test]
        public void ANonPositiveTimeoutIsRejected()
        {
            Assert.Throws<ArgumentException>(() =>
                new BackendV2ApiConfig(new Uri("https://example.com/"), BackendV2Environment.Production, 0));
        }

        [Test]
        public void DevelopmentDefaultsToTheLocalBackendDevPort()
        {
            BackendV2ApiConfig config = BackendV2ApiConfig.Development();

            Assert.That(config.Environment, Is.EqualTo(BackendV2Environment.Development));
            Assert.That(config.BaseUri.Host, Is.EqualTo("localhost"));
            Assert.That(config.BaseUri.Scheme, Is.EqualTo(Uri.UriSchemeHttps));
        }

        [Test]
        public void ABaseUriWithoutATrailingSlashIsNormalized()
        {
            // new Uri(new Uri("https://host/prefix"), "api/v2/x") drops "/prefix" per RFC 3986
            // merge semantics - a base URI behind a path-prefixed gateway, configured without a
            // trailing slash, would otherwise silently misroute every request.
            BackendV2ApiConfig config = BackendV2ApiConfig.Custom(
                new Uri("https://gateway.example.com/level5backend"), BackendV2Environment.Staging);

            Assert.That(config.BaseUri.AbsoluteUri, Is.EqualTo("https://gateway.example.com/level5backend/"));

            Uri resolved = new Uri(config.BaseUri, "api/v2/auth/login");
            Assert.That(resolved.AbsoluteUri, Is.EqualTo("https://gateway.example.com/level5backend/api/v2/auth/login"));
        }

        [Test]
        public void ABaseUriThatAlreadyHasATrailingSlashIsUnchanged()
        {
            Uri baseUri = new Uri("https://staging.level5.game/");
            BackendV2ApiConfig config = BackendV2ApiConfig.Custom(baseUri, BackendV2Environment.Staging);

            Assert.That(config.BaseUri, Is.EqualTo(baseUri));
        }

        [Test]
        public void TheProviderDefaultsToDevelopmentAndHonorsOverride()
        {
            BackendV2ApiConfigProvider.Reset();
            Assert.That(BackendV2ApiConfigProvider.Current.Environment, Is.EqualTo(BackendV2Environment.Development));

            BackendV2ApiConfig custom = BackendV2ApiConfig.Custom(
                new Uri("https://staging.level5.game/"), BackendV2Environment.Staging);
            BackendV2ApiConfigProvider.Override(custom);

            Assert.That(BackendV2ApiConfigProvider.Current, Is.SameAs(custom));
        }
    }
}
