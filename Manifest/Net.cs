using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace Manifest;

public static class Net
{
    public static string LanIp()
    {
        try
        {
            using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.Connect("10.255.255.255", 1);
            return ((IPEndPoint)s.LocalEndPoint!).Address.ToString();
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    /// <summary>Every name this machine legitimately answers to, for AllowedHosts.</summary>
    public static HashSet<string> LocalNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { LanIp() };
        try
        {
            var host = Dns.GetHostName();
            names.Add(host);
            foreach (var address in Dns.GetHostAddresses(host))
                names.Add(address.ToString());
        }
        catch
        {
            // no DNS, no hostname - localhost is still in the set
        }
        names.RemoveWhere(string.IsNullOrEmpty);
        return names;
    }

    /// <summary>
    /// True if ip is in the certificate's subjectAltName. If the machine's LAN address
    /// has changed since ./make_cert.sh last ran (new router, new DHCP lease), the
    /// phone's TLS handshake fails with no useful error on screen — this lets the
    /// startup banner say why instead of leaving it a mystery.
    ///
    /// The Python shelled out to openssl for this and passed when openssl was missing.
    /// .NET can read the extension directly, so there is no external tool to be
    /// missing and no reason to guess.
    /// </summary>
    public static bool CertCoversIp(string certPath, string ip)
    {
        try
        {
            // cert.pem holds a certificate and no private key, so this is the
            // certificate-only load - CreateFromPemFile would demand a key.
            using var cert = new X509Certificate2(certPath);
            foreach (var extension in cert.Extensions)
            {
                if (extension is X509SubjectAlternativeNameExtension san)
                    return san.EnumerateIPAddresses().Any(a => a.ToString() == ip);
            }
            return false;
        }
        catch
        {
            return true;  // can't check - don't block startup over it
        }
    }
}
