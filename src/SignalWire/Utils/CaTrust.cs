// CaTrust.cs
//
// Shared TLS trust-bundle validation for the A5 fleet CA-var contract
// (SIGNALWIRE_REST_CA_FILE / SIGNALWIRE_RELAY_CA_FILE). Both transports build a
// server-certificate validation callback that accepts the peer chain when it
// terminates in the caller-supplied CA bundle — the .NET analogue of python's
// `session.verify = ca_file` (REST) and `ssl.create_default_context(cafile=...)`
// (RELAY). Kept transport-neutral so REST/HttpClient.cs and Relay/Client.cs share
// one audited implementation.

using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace SignalWire.Utils;

/// <summary>
/// Validates a server certificate chain against an explicit, caller-supplied CA
/// bundle (the A5 fleet CA-var trust root). Used by both the REST HTTP client
/// (<c>SIGNALWIRE_REST_CA_FILE</c>) and the RELAY WebSocket client
/// (<c>SIGNALWIRE_RELAY_CA_FILE</c>).
/// </summary>
internal static class CaTrust
{
    /// <summary>
    /// Load a certificate (a CA trust bundle) from a PEM/DER file, using the
    /// non-obsolete loader where the target framework provides it. On net9+
    /// <c>X509CertificateLoader</c> is the sanctioned API (the
    /// <c>X509Certificate2(string)</c> ctor is obsolete SYSLIB0057 there); on
    /// net8 that loader does not exist, so the ctor is used. One helper keeps the
    /// two transports' CA-file loading identical across all target frameworks.
    /// </summary>
    internal static X509Certificate2 LoadTrustBundle(string caFile)
    {
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadCertificateFromFile(caFile);
#else
        return new X509Certificate2(caFile);
#endif
    }

    /// <summary>
    /// Return <c>true</c> when the presented certificate chain is trusted given
    /// <paramref name="trustRoot"/> as an ADDITIONAL trust anchor. If the chain
    /// already validated under the OS store (no policy errors) it is accepted; a
    /// chain whose only defect is an untrusted root is re-validated against the
    /// supplied CA, so a private/self-signed SignalWire space cert verifies when
    /// its issuing CA is named by the env var. Any other TLS error (name
    /// mismatch, not-available) still rejects.
    /// </summary>
    internal static bool Validate(
        X509Certificate2? cert,
        X509Chain? chain,
        SslPolicyErrors errors,
        X509Certificate2 trustRoot)
    {
        // Clean chain under the OS trust store — nothing to add.
        if (errors == SslPolicyErrors.None)
        {
            return true;
        }

        // We only rescue the "untrusted root" case; a hostname mismatch or a
        // missing certificate is a genuine failure regardless of the extra CA.
        if ((errors & ~SslPolicyErrors.RemoteCertificateChainErrors) != SslPolicyErrors.None)
        {
            return false;
        }

        if (cert is null)
        {
            return false;
        }

        using var custom = new X509Chain();
        custom.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        custom.ChainPolicy.CustomTrustStore.Add(trustRoot);
        custom.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

        // Carry any intermediates the server presented so the chain can build up
        // to the supplied root.
        if (chain is not null)
        {
            foreach (var element in chain.ChainElements)
            {
                custom.ChainPolicy.ExtraStore.Add(element.Certificate);
            }
        }

        return custom.Build(cert);
    }
}
