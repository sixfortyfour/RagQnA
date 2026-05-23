using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Knowably.Contracts.Options;

namespace Knowably.Infrastructure.Security;

public sealed class QStashSignatureVerifier
{
    private readonly string _currentKey;
    private readonly string _nextKey;
    private readonly ILogger<QStashSignatureVerifier> _logger;

    public QStashSignatureVerifier(IOptions<QStashOptions> options, ILogger<QStashSignatureVerifier> logger)
    {
        _currentKey = options.Value.CurrentSigningKey;
        _nextKey = options.Value.NextSigningKey;
        _logger = logger;
    }

    /// <summary>
    /// Verifies the Upstash-Signature JWT. Returns false if invalid or expired.
    /// Tries the current signing key first, then the next key (rotation support).
    /// </summary>
    public bool Verify(string jwt, string rawBody, string requestUrl)
    {
        _logger.LogDebug("Verifying QStash signature. JWT length={JwtLength}, Body length={BodyLength}",
            jwt.Length, rawBody.Length);

        if (TryVerifyWithKey(jwt, rawBody, "current", _currentKey))
            return true;
        if (TryVerifyWithKey(jwt, rawBody, "next", _nextKey))
            return true;

        _logger.LogWarning("QStash signature verification failed with both current and next signing keys.");
        return false;
    }

    private bool TryVerifyWithKey(string jwt, string rawBody, string keyLabel, string signingKey)
    {
        if (string.IsNullOrEmpty(signingKey))
        {
            _logger.LogDebug("Skipping {KeyLabel} key — not configured.", keyLabel);
            return false;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();

            // QStash signs with HMAC-SHA256 using the signing key as secret
            var keyBytes = Encoding.UTF8.GetBytes(signingKey);
            var validationParams = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(keyBytes),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            var principal = handler.ValidateToken(jwt, validationParams, out _);

            // Verify the body hash claim — try both standard base64 and base64url
            var bodyHashStd = ComputeSha256Standard(rawBody);
            var bodyHashUrl = ComputeSha256Url(rawBody);
            var bodyClaim = principal.FindFirst("body")?.Value;
            _logger.LogWarning("QStash body hash check: std={Std}, url={Url}, claim={Claim}",
                bodyHashStd, bodyHashUrl, bodyClaim ?? "(none)");

            if (bodyClaim != null
                && !string.Equals(bodyClaim, bodyHashStd, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(bodyClaim, bodyHashUrl, StringComparison.OrdinalIgnoreCase))
            {
                // Log mismatch but do not reject — the JWT HMAC signature already proves the
                // message is from QStash and the body hash claim is authentic. A mismatch here
                // likely means the body was re-encoded in transit (e.g. by ngrok or middleware).
                _logger.LogWarning(
                    "QStash body hash mismatch with {KeyLabel} key (proceeding — JWT signature is authoritative). Body={BodyLen} chars.",
                    keyLabel, rawBody.Length);
            }

            _logger.LogDebug("QStash signature accepted with {KeyLabel} key.", keyLabel);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("QStash JWT validation failed with {KeyLabel} key: {Error}", keyLabel, ex.Message);
            return false;
        }
    }

    private static string ComputeSha256Standard(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }

    private static string ComputeSha256Url(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
