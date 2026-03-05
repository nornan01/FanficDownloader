using System.Net;
using System;
using System.Linq;

namespace FanficDownloader.Application.Security;

public static class UrlValidator
{
    private static readonly string[] AllowedHosts =
    {
        "ficbook.net",
        "fanfiction.net",
        "snapetales.com",
        "walkingtheplank.org"
    };

    public static void Validate(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("Invalid URL format");

        if (uri.Scheme != Uri.UriSchemeHttp &&
            uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Only HTTP/HTTPS allowed");

        if (IsLocalAddress(uri))
            throw new ArgumentException("Local or internal addresses are not allowed");

        if (!IsWhitelistedHost(uri.Host))
            throw new ArgumentException("Host is not allowed");
    }

    private static bool IsWhitelistedHost(string host)
    {
        return AllowedHosts.Any(h =>
            host.Equals(h, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsLocalAddress(Uri uri)
    {
        if (uri.IsLoopback)
            return true;

        if (IPAddress.TryParse(uri.Host, out var ip))
        {
            var bytes = ip.GetAddressBytes();

            // 127.x.x.x
            if (bytes[0] == 127)
                return true;

            // 10.x.x.x
            if (bytes[0] == 10)
                return true;

            // 192.168.x.x
            if (bytes[0] == 192 && bytes[1] == 168)
                return true;

            // 172.16.x.x - 172.31.x.x
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                return true;
        }

        return false;
    }
}