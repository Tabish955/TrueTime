using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace NetTime.Tray;

public static class LogoProvider
{
    private static readonly Dictionary<string, Image> ImageCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Assembly CurrentAssembly = typeof(LogoProvider).Assembly;

    public static Image? GetLogo(string? hostname)
    {
        string key = DetermineLogoKey(hostname);
        if (ImageCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        try
        {
            string resourceName = $"NetTime.Tray.Resources.Logos.{key}.png";
            using var stream = CurrentAssembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                var img = Image.FromStream(stream);
                ImageCache[key] = img;
                return img;
            }
        }
        catch { }

        return null;
    }

    public static Image? GetAppLogo()
    {
        return GetResourceImage("logo");
    }

    private static Image? GetResourceImage(string name)
    {
        if (ImageCache.TryGetValue(name, out var cached))
        {
            return cached;
        }

        try
        {
            string resourceName = $"NetTime.Tray.Resources.Logos.{name}.png";
            using var stream = CurrentAssembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                var img = Image.FromStream(stream);
                ImageCache[name] = img;
                return img;
            }
        }
        catch { }

        return null;
    }

    public static (string BrandName, string Hostname) GetServerDisplayInfo(string? rawHostname)
    {
        if (string.IsNullOrWhiteSpace(rawHostname))
        {
            return ("Unknown", "—");
        }

        string hostLower = rawHostname.Trim().ToLowerInvariant();

        if (hostLower.Contains("cloudflare"))
            return ("Cloudflare", rawHostname);
        if (hostLower.Contains("google"))
            return ("Google", rawHostname);
        if (hostLower.Contains("facebook") || hostLower.Contains("fb.com") || hostLower.Contains("meta"))
            return ("Facebook", rawHostname);
        if (hostLower.Contains("apple"))
            return ("Apple", rawHostname);
        if (hostLower.Contains("nist"))
            return ("NIST", rawHostname);
        if (hostLower.Contains("windows") || hostLower.Contains("microsoft"))
            return ("Microsoft", rawHostname);
        if (hostLower.Contains("pool.ntp.org"))
            return ("NTP Pool", rawHostname);

        string brand = rawHostname;
        int dot = brand.IndexOf('.');
        if (dot > 0)
        {
            brand = brand.Substring(0, dot);
        }
        if (brand.Length > 0)
        {
            brand = char.ToUpper(brand[0]) + brand.Substring(1);
        }

        return (brand, rawHostname);
    }

    private static string DetermineLogoKey(string? hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname)) return "ntppool";
        string h = hostname.ToLowerInvariant();

        if (h.Contains("cloudflare")) return "cloudflare";
        if (h.Contains("google")) return "google";
        if (h.Contains("facebook") || h.Contains("fb.com") || h.Contains("meta")) return "facebook";
        if (h.Contains("apple")) return "apple";
        if (h.Contains("nist")) return "nist";
        if (h.Contains("windows") || h.Contains("microsoft")) return "microsoft";

        return "ntppool";
    }
}
