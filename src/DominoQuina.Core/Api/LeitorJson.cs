using System.Globalization;
using System.Text;
using System.Text.Json;

namespace DominoQuina.Core.Api;

/// <summary>
/// Leitura tolerante de JSON: procura propriedades por vários nomes possíveis,
/// ignorando maiúsculas, underscores e hífens. Assim pequenas diferenças de nome
/// no contrato não quebram o cliente.
/// </summary>
public static class LeitorJson
{
    public static JsonElement? Propriedade(JsonElement elemento, params string[] nomes)
    {
        if (elemento.ValueKind != JsonValueKind.Object) return null;

        foreach (var nome in nomes)
        {
            var alvo = Normalizar(nome);
            foreach (var propriedade in elemento.EnumerateObject())
            {
                if (Normalizar(propriedade.Name) == alvo &&
                    propriedade.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
                    return propriedade.Value;
            }
        }

        return null;
    }

    public static string? Texto(JsonElement elemento, params string[] nomes) =>
        Propriedade(elemento, nomes) switch
        {
            { ValueKind: JsonValueKind.String } v => v.GetString(),
            { ValueKind: JsonValueKind.Number } v => v.GetRawText(),
            { ValueKind: JsonValueKind.True } => "true",
            { ValueKind: JsonValueKind.False } => "false",
            _ => null
        };

    public static long? Longo(JsonElement elemento, params string[] nomes) =>
        Propriedade(elemento, nomes) switch
        {
            { ValueKind: JsonValueKind.Number } v when v.TryGetInt64(out var n) => n,
            { ValueKind: JsonValueKind.String } v when long.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
            _ => null
        };

    public static int? Inteiro(JsonElement elemento, params string[] nomes) =>
        Longo(elemento, nomes) is { } n && n is >= int.MinValue and <= int.MaxValue ? (int)n : null;

    public static bool? Booleano(JsonElement elemento, params string[] nomes) =>
        Propriedade(elemento, nomes) switch
        {
            { ValueKind: JsonValueKind.True } => true,
            { ValueKind: JsonValueKind.False } => false,
            { ValueKind: JsonValueKind.String } v when bool.TryParse(v.GetString(), out var b) => b,
            _ => null
        };

    /// <summary>Lê um identificador que pode vir como texto, número ou objeto com "id".</summary>
    public static string? Identificador(JsonElement elemento, params string[] nomes) =>
        Propriedade(elemento, nomes) switch
        {
            { ValueKind: JsonValueKind.Object } objeto => Texto(objeto, "id", "jogadorId", "partidaId"),
            { } valor => valor.ValueKind is JsonValueKind.String or JsonValueKind.Number
                ? (valor.ValueKind == JsonValueKind.String ? valor.GetString() : valor.GetRawText())
                : null,
            null => null
        };

    /// <summary>Minúsculas, sem acentos e somente letras e dígitos ("Jogar_Abertura" vira "jogarabertura").</summary>
    public static string Normalizar(string texto)
    {
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
