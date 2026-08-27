namespace SupportCrm.Application.Security;

using System.Security.Cryptography;
using System.Text;

// RFC 6238 TOTP — hand-rolled using HMACSHA1 (built into .NET). Genuinely functional with any
// standard authenticator app (Google Authenticator, Authy, ...); no new NuGet dependency.
public class TotpService
{
    private const int StepSeconds = 30;
    private const int Digits = 6;
    private const int ToleranceSteps = 1; // accept the previous/next 30s window too, for clock drift

    public string GenerateSecret() => Base32Encode(RandomNumberGenerator.GetBytes(20)); // 160-bit

    public string GetProvisioningUri(string email, string secret, string issuer = "SupportCrm") =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&digits={Digits}&period={StepSeconds}";

    public bool ValidateCode(string secret, string code, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != Digits) return false;
        var key = Base32Decode(secret);
        var currentStep = now.ToUnixTimeSeconds() / StepSeconds;
        for (var offset = -ToleranceSteps; offset <= ToleranceSteps; offset++)
            if (ComputeCode(key, currentStep + offset) == code) return true;
        return false;
    }

    private static string ComputeCode(byte[] key, long step)
    {
        var stepBytes = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian) Array.Reverse(stepBytes);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(stepBytes);
        var offset = hash[^1] & 0x0F;
        var binaryCode = ((hash[offset] & 0x7F) << 24) | ((hash[offset + 1] & 0xFF) << 16) | ((hash[offset + 2] & 0xFF) << 8) | (hash[offset + 3] & 0xFF);
        return (binaryCode % (int)Math.Pow(10, Digits)).ToString(new string('0', Digits));
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var sb = new StringBuilder();
        int bits = 0, value = 0;
        foreach (var b in data)
        {
            value = (value << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(alphabet[(value >> (bits - 5)) & 0x1F]);
                bits -= 5;
            }
        }
        if (bits > 0) sb.Append(alphabet[(value << (5 - bits)) & 0x1F]);
        return sb.ToString();
    }

    private static byte[] Base32Decode(string base32)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        int bits = 0, value = 0;
        var output = new List<byte>();
        foreach (var c in base32.ToUpperInvariant())
        {
            var index = alphabet.IndexOf(c);
            if (index < 0) continue;
            value = (value << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((value >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }
        return output.ToArray();
    }
}
