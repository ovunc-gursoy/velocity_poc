using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace Velocity.Mcp.Core;

[McpServerToolType]
public sealed class CurrencyTools
{
    public const string HttpClientName = "frankfurter";

    [McpServerTool(Name = "convert_currency")]
    [Description("""
        Convert an amount between currencies using European Central Bank reference rates.
        Rates are published once per working day, so this is not a live trading rate. The
        response reports the rate date actually used, which may be earlier than the date
        requested because weekends and holidays fall back to the previous working day.
        """)]
    public static async Task<Conversion> ConvertAsync(
        IHttpClientFactory httpClientFactory,
        [Description("Amount to convert. Must be greater than zero.")]
        decimal amount,
        [Description("Currency to convert from, as a 3-letter ISO 4217 code, e.g. 'USD'.")]
        string from,
        [Description("Currency to convert to, as a 3-letter ISO 4217 code, e.g. 'EUR'.")]
        string to,
        [Description("Rate date as 'yyyy-MM-dd'. Omit for the latest published rate. Rates start at 1999-01-04.")]
        string? date = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be greater than zero.");
        }

        from = NormaliseCode(from, nameof(from));
        to = NormaliseCode(to, nameof(to));

        var onDate = ParseDate(date);

        // Frankfurter rejects a base==symbol pair with a 422, so answer it without a call.
        if (from == to)
        {
            return new Conversion(amount, from, amount, to, Rate: 1m, RateDate: onDate ?? DateOnly.FromDateTime(DateTime.UtcNow));
        }

        var segment = onDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "latest";
        var client = httpClientFactory.CreateClient(HttpClientName);

        // amount=1 so we get the bare rate and do the arithmetic in decimal; the API returns
        // a pre-rounded product if you let it multiply, which loses precision on large amounts.
        using var response = await client.GetAsync(
            $"v1/{segment}?amount=1&base={from}&symbols={to}", cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity)
        {
            throw new ArgumentException(
                $"No rate available for {from} to {to}. Both must be currencies the ECB publishes; call with a known pair such as USD to EUR to check the service is reachable.");
        }
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<FrankfurterResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The rates service returned an empty response.");

        if (!payload.Rates.TryGetValue(to, out var rate))
        {
            throw new ArgumentException($"The rates service did not return a {to} rate for {from}.");
        }

        return new Conversion(
            Amount: amount,
            From: from,
            Converted: Math.Round(amount * rate, 2, MidpointRounding.ToEven),
            To: to,
            Rate: rate,
            RateDate: payload.Date);
    }

    private static string NormaliseCode(string code, string parameterName)
    {
        var trimmed = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length != 3 || !trimmed.All(char.IsAsciiLetter))
        {
            throw new ArgumentException($"'{code}' is not a 3-letter ISO 4217 currency code, e.g. 'USD'.", parameterName);
        }
        return trimmed;
    }

    private static DateOnly? ParseDate(string? date)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            return null;
        }
        if (!DateOnly.TryParseExact(date.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            throw new ArgumentException($"'{date}' is not a date in 'yyyy-MM-dd' format.", nameof(date));
        }
        if (parsed > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ArgumentOutOfRangeException(nameof(date), date, "Rate dates cannot be in the future.");
        }
        return parsed;
    }
}

/// <param name="Rate">Units of <paramref name="To"/> per single unit of <paramref name="From"/>.</param>
/// <param name="RateDate">The date the rate was published, which may precede the date requested.</param>
public sealed record Conversion(
    decimal Amount,
    string From,
    decimal Converted,
    string To,
    decimal Rate,
    DateOnly RateDate);

internal sealed record FrankfurterResponse(
    [property: JsonPropertyName("date")] DateOnly Date,
    [property: JsonPropertyName("rates")] Dictionary<string, decimal> Rates);
