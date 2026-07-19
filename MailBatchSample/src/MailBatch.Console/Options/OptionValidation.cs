namespace MailBatch.Console.Options;

/// <summary>
/// アプリケーション設定値の共通検証処理を提供します。
/// </summary>
internal static class OptionValidation
{
    public const int MINIMUM_NETWORK_PORT = 1;
    public const int MAXIMUM_NETWORK_PORT = 65_535;

    /// <summary>
    /// 文字列が未設定または空白のみでないことを検証します。
    /// </summary>
    public static void Require(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} is required.");
        }
    }

    /// <summary>
    /// コレクションに1件以上の要素が含まれることを検証します。
    /// </summary>
    public static void RequireNotEmpty<T>(IReadOnlyCollection<T> values, string key)
    {
        if (values.Count == 0)
        {
            throw new InvalidOperationException($"{key} is required.");
        }
    }

    /// <summary>
    /// 整数値が指定範囲内であることを検証します。
    /// </summary>
    public static void RequireRange(int value, int min, int max, string key)
    {
        if (value < min || value > max)
        {
            throw new InvalidOperationException($"{key} must be between {min} and {max}.");
        }
    }

    /// <summary>
    /// 整数値が正の値であることを検証します。
    /// </summary>
    public static void RequirePositive(int value, string key)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException($"{key} must be greater than 0.");
        }
    }

    /// <summary>
    /// 整数値が0以上であることを検証します。
    /// </summary>
    public static void RequireNonNegative(int value, string key)
    {
        if (value < 0)
        {
            throw new InvalidOperationException($"{key} must be greater than or equal to 0.");
        }
    }

    /// <summary>
    /// 列挙値が定義済みの値であることを検証します。
    /// </summary>
    public static void RequireDefined<TEnum>(TEnum value, string key)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new InvalidOperationException($"{key} must be a valid {typeof(TEnum).Name} value.");
        }
    }
}
