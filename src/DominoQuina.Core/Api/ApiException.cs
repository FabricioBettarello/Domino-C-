using System.Net;
using System.Text.Json;

namespace DominoQuina.Core.Api;

/// <summary>
/// Rejeição devolvida pela API. Carrega o código HTTP, o código de negócio
/// (ex.: <c>JOGADOR_JA_ATIVO</c>) e a orientação que o cliente deve mostrar.
/// </summary>
public sealed class ApiException : Exception
{
    public const string CodigoJogadorJaAtivo = "JOGADOR_JA_ATIVO";

    public ApiException(HttpStatusCode statusCode, string? codigo, string? mensagemServidor, TimeSpan? aguardarPor = null)
        : base(MontarMensagem(statusCode, codigo, mensagemServidor))
    {
        StatusCode = statusCode;
        Codigo = codigo;
        MensagemServidor = mensagemServidor;
        AguardarPor = aguardarPor;
    }

    public HttpStatusCode StatusCode { get; }
    public string? Codigo { get; }
    public string? MensagemServidor { get; }

    /// <summary>Janela informada pela API (Retry-After) nos casos de limite de solicitações.</summary>
    public TimeSpan? AguardarPor { get; }

    public bool PerfilAtivoEmOutroCliente =>
        StatusCode == HttpStatusCode.Conflict &&
        (string.Equals(Codigo, CodigoJogadorJaAtivo, StringComparison.OrdinalIgnoreCase) ||
         (MensagemServidor?.Contains(CodigoJogadorJaAtivo, StringComparison.OrdinalIgnoreCase) ?? false));

    public bool ConflitoDeVersao => StatusCode == HttpStatusCode.Conflict && !PerfilAtivoEmOutroCliente;

    public bool ExigeNovaAutenticacao => StatusCode == HttpStatusCode.Unauthorized;

    /// <summary>O que o cliente deve fazer diante desta rejeição.</summary>
    public string Orientacao => (int)StatusCode switch
    {
        400 => "Requisição inválida. Confira os dados enviados.",
        401 => "Token ausente, inválido ou revogado. Solicite uma nova autenticação.",
        403 => "O usuário ou o perfil não tem permissão para esta ação.",
        404 => "Recurso não encontrado na API.",
        409 when PerfilAtivoEmOutroCliente =>
            "Este perfil está ativo em outro cliente. Libere-o lá (desativar-cliente) antes de ativá-lo aqui.",
        409 => "Conflito de versão: a mesa mudou. O estado oficial foi recarregado; revise a jogada.",
        422 => "Jogada, encaixe, turno ou passagem inválida. A partida não avançou.",
        429 => AguardarPor is { } espera
            ? $"Limite de solicitações atingido. Aguarde {Math.Ceiling(espera.TotalSeconds)} s e tente de novo."
            : "Limite de solicitações atingido. Aguarde a janela indicada e tente de novo.",
        >= 500 => "Falha no servidor da API. Tente novamente em instantes.",
        _ => "A API rejeitou a solicitação."
    };

    /// <summary>Cria a exceção a partir do status e do corpo de erro (ProblemDetails ou JSON próprio).</summary>
    public static ApiException De(HttpStatusCode status, string? corpo, TimeSpan? retryAfter)
    {
        string? codigo = null;
        string? mensagem = null;
        TimeSpan? aguardar = retryAfter;

        if (!string.IsNullOrWhiteSpace(corpo))
        {
            try
            {
                using var doc = JsonDocument.Parse(corpo);
                var raiz = doc.RootElement;
                codigo = LeitorJson.Texto(raiz, "codigo", "code", "erro", "error", "codigoErro");
                mensagem = LeitorJson.Texto(raiz, "mensagem", "message", "detail", "detalhe", "title");
                if (aguardar is null && LeitorJson.Inteiro(raiz, "retryAfter", "aguardarSegundos", "segundos", "retryAfterSegundos") is { } s)
                    aguardar = TimeSpan.FromSeconds(s);

                // Códigos no padrão CONSTANTE às vezes chegam no "title" do ProblemDetails.
                var titulo = LeitorJson.Texto(raiz, "title");
                if (codigo is null && titulo is not null && titulo.All(c => char.IsUpper(c) || c == '_'))
                    codigo = titulo;
            }
            catch (JsonException)
            {
                mensagem = corpo.Length > 300 ? corpo[..300] : corpo;
            }
        }

        return new ApiException(status, codigo, mensagem, aguardar);
    }

    private static string MontarMensagem(HttpStatusCode status, string? codigo, string? mensagem)
    {
        var partes = new List<string> { $"HTTP {(int)status}" };
        if (!string.IsNullOrWhiteSpace(codigo)) partes.Add(codigo);
        if (!string.IsNullOrWhiteSpace(mensagem)) partes.Add(mensagem);
        return string.Join(" - ", partes);
    }
}
