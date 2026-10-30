using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AislingExchange.Broker;
using Xunit;
namespace AislingExchange.Broker.Tests;

public sealed class BrokerHostTests
{
    [Fact]
    public async Task ConfiguredHttpsListenerStartsAndValidatesItsCertificateAuthority()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ae-https-host-" + Guid.NewGuid().ToString("N"));
        string[] names = ["ASPNETCORE_URLS", "ASPNETCORE_Kestrel__Certificates__Default__Path", "ASPNETCORE_Kestrel__Certificates__Default__KeyPath"];
        string?[] previous = names.Select(Environment.GetEnvironmentVariable).ToArray();
        try
        {
            Directory.CreateDirectory(directory);
            var now = DateTimeOffset.UtcNow;
            using var authorityKey = RSA.Create(2048);
            var authorityRequest = new CertificateRequest("CN=Synthetic AislingExchange Test Authority", authorityKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            authorityRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            authorityRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
            using var authority = authorityRequest.CreateSelfSigned(now.AddMinutes(-1), now.AddHours(1));
            using var serverKey = RSA.Create(2048);
            var serverRequest = new CertificateRequest("CN=localhost", serverKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var namesExtension = new SubjectAlternativeNameBuilder();
            namesExtension.AddIpAddress(IPAddress.Loopback);
            namesExtension.AddDnsName("localhost");
            serverRequest.CertificateExtensions.Add(namesExtension.Build());
            serverRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            serverRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
            using var publicCertificate = serverRequest.Create(authority, now.AddMinutes(-1), now.AddMinutes(30), RandomNumberGenerator.GetBytes(16));
            using var serverCertificate = publicCertificate.CopyWithPrivateKey(serverKey);
            string certificate = Path.Combine(directory, "server.crt");
            string key = Path.Combine(directory, "server.key");
            await File.WriteAllTextAsync(certificate, serverCertificate.ExportCertificatePem());
            await File.WriteAllTextAsync(key, serverKey.ExportPkcs8PrivateKeyPem());
            Environment.SetEnvironmentVariable(names[0], "https://127.0.0.1:0");
            Environment.SetEnvironmentVariable(names[1], certificate);
            Environment.SetEnvironmentVariable(names[2], key);
            var options = new BrokerOptions { GameServer = "synthetic-profile", WorkerTokenHash = new string('A', 64) };
            await using var app = BrokerHost.Create(options, Path.Combine(directory, "state"));
            try { await app.StartAsync(); }
            catch (Exception error)
            {
                Assert.Fail("Configured HTTPS listener startup failed: " + error.GetType().Name + " (" + error.HResult + ").");
            }

            bool authorityValidated = false;
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, presented, _, errors) =>
                {
                    if (presented is null || (errors & (SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateNotAvailable)) != 0)
                        return false;
                    using var chain = new X509Chain();
                    chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                    chain.ChainPolicy.CustomTrustStore.Add(authority);
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));
                    authorityValidated = chain.Build(presented);
                    return authorityValidated;
                }
            };
            using var client = new HttpClient(handler);
            var response = await client.GetAsync(app.Urls.Single() + "/api/quest-supplies/v1/capabilities");
            Assert.True(authorityValidated);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.False(response.Headers.Contains("Server"));
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            Assert.Equal("{\"code\":\"UNAUTHORIZED\"}", await response.Content.ReadAsStringAsync());
            await app.StopAsync();
        }
        finally
        {
            for (int i = 0; i < names.Length; i++) Environment.SetEnvironmentVariable(names[i], previous[i]);
            string absolute = Path.GetFullPath(directory);
            if (!absolute.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Synthetic HTTPS fixture path is invalid.");
            if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
        }
    }

    [Fact]
    public async Task ProductionListenerRejectsPlainHttpWithoutServerMetadata()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ae-host-" + Guid.NewGuid().ToString("N"));
        try
        {
            var options = new BrokerOptions { GameServer = "synthetic-profile", WorkerTokenHash = new string('A', 64) };
            await using var app = BrokerHost.Create(options, directory);
            app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();
            using var client = new HttpClient();
            var response = await client.GetAsync(app.Urls.Single());
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(response.Headers.Contains("Server"));
            Assert.Equal("{\"code\":\"HTTPS_REQUIRED\"}", await response.Content.ReadAsStringAsync());
            await app.StopAsync();
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
