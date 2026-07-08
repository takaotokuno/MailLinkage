namespace MailBatch.Console.Options;

internal static class OptionValidation
{
    public static void Require(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} is required.");
        }
    }

    public static void RequireRange(int value, int min, int max, string key)
    {
        if (value < min || value > max)
        {
            throw new InvalidOperationException($"{key} must be between {min} and {max}.");
        }
    }

    public static void RequirePositive(int value, string key)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException($"{key} must be greater than 0.");
        }
    }
}
