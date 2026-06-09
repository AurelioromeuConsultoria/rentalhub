namespace RentalHub.API.Services;

public sealed class PasswordPolicyService
{
    private const int MinimumLength = 8;

    private static readonly string[] CommonPasswords =
    [
        "12345678",
        "123456789",
        "1234567890",
        "87654321",
        "qwerty123",
        "password",
        "password1",
        "senha123",
        "senha1234",
        "rentalhub",
        "malach"
    ];

    public string? Validate(string? password, params string?[] relatedTerms)
    {
        var trimmedPassword = password?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedPassword))
        {
            return "Senha é obrigatória.";
        }

        if (trimmedPassword.Length < MinimumLength)
        {
            return $"A senha deve ter pelo menos {MinimumLength} caracteres.";
        }

        var normalizedPassword = Normalize(trimmedPassword);
        if (CommonPasswords.Contains(normalizedPassword, StringComparer.OrdinalIgnoreCase))
        {
            return "Use uma senha menos óbvia.";
        }

        if (IsSimpleSequence(normalizedPassword))
        {
            return "Evite sequências simples na senha.";
        }

        var related = relatedTerms
            .SelectMany(BuildRelatedTerms)
            .Where(term => term.Length >= 4)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        if (related.Any(term => normalizedPassword == term || normalizedPassword.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return "Evite usar nome ou e-mail na senha.";
        }

        return null;
    }

    private static IEnumerable<string> BuildRelatedTerms(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        var normalized = Normalize(value);
        yield return normalized;

        var emailSeparatorIndex = normalized.IndexOf('@', StringComparison.Ordinal);
        if (emailSeparatorIndex > 0)
        {
            yield return normalized[..emailSeparatorIndex];
        }

        foreach (var part in normalized.Split([' ', '.', '-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return part;
        }
    }

    private static bool IsSimpleSequence(string value)
    {
        return value is "abcdefgh" or "abcdefghi" or "abcdefghij" or "qwertyui" or "asdfghjk" ||
            "0123456789".Contains(value, StringComparison.Ordinal) ||
            "9876543210".Contains(value, StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}
