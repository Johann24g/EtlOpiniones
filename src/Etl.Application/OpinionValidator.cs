using Etl.Domain;

namespace Etl.Application;

/// <summary>Reglas de calidad mínimas aplicadas a cada registro antes de llegar a staging.</summary>
public static class OpinionValidator
{
    public const int MaxTextLength = 4000;

    public static bool TryNormalize(Opinion input, out Opinion normalized, out string? reason)
    {
        normalized = input;
        reason = null;

        var text = input.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            reason = "Texto vacío";
            return false;
        }

        if (input.Rating is < 1 or > 5)
        {
            reason = $"Calificación fuera de rango (1-5): {input.Rating}";
            return false;
        }

        if (text.Length > MaxTextLength)
            text = text[..MaxTextLength];

        normalized = input with
        {
            Text = text,
            ExternalId = input.ExternalId?.Trim()
        };
        return true;
    }
}
