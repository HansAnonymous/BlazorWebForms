using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace BlazorWebForms.SampleApp;

public sealed class EntryFileDownloadTokenService
{
    private readonly byte[] secret;

    public EntryFileDownloadTokenService(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["BlazorWebForms:FileDownloadTokenSecret"];
        if (string.IsNullOrWhiteSpace(configured) && !environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "BlazorWebForms:FileDownloadTokenSecret must be configured in non-development environments.");
        }

        secret = Encoding.UTF8.GetBytes(configured ?? GenerateEphemeralDevSecret());
    }

    private static string GenerateEphemeralDevSecret()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }

    public string Create(Guid entryId, Guid fileId, TimeSpan validFor)
    {
        var expiresUnix = DateTimeOffset.UtcNow.Add(validFor).ToUnixTimeSeconds();
        var payload = $"{entryId:N}.{fileId:N}.{expiresUnix}";
        var signature = ComputeSignature(payload);
        return $"{ToBase64Url(Encoding.UTF8.GetBytes(payload))}.{ToBase64Url(signature)}";
    }

    public bool TryValidate(string token, Guid expectedEntryId, Guid expectedFileId)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var parts = token.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        byte[] payloadBytes;
        byte[] providedSignature;
        try
        {
            payloadBytes = FromBase64Url(parts[0]);
            providedSignature = FromBase64Url(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        var payload = Encoding.UTF8.GetString(payloadBytes);
        var expectedSignature = ComputeSignature(payload);
        if (!CryptographicOperations.FixedTimeEquals(expectedSignature, providedSignature))
        {
            return false;
        }

        var payloadParts = payload.Split('.', 3, StringSplitOptions.RemoveEmptyEntries);
        if (payloadParts.Length != 3)
        {
            return false;
        }

        if (!Guid.TryParseExact(payloadParts[0], "N", out var entryId) ||
            !Guid.TryParseExact(payloadParts[1], "N", out var fileId) ||
            !long.TryParse(payloadParts[2], out var expiresUnix))
        {
            return false;
        }

        if (entryId != expectedEntryId || fileId != expectedFileId)
        {
            return false;
        }

        return DateTimeOffset.UtcNow <= DateTimeOffset.FromUnixTimeSeconds(expiresUnix);
    }

    private byte[] ComputeSignature(string payload)
    {
        using var hmac = new HMACSHA256(secret);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    }

    private static string ToBase64Url(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] FromBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        var padding = 4 - (base64.Length % 4);
        if (padding is > 0 and < 4)
        {
            base64 += new string('=', padding);
        }

        return Convert.FromBase64String(base64);
    }
}
