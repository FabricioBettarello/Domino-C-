using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DominoQuina.Core.Modelos;
using DominoQuina.Core.Seguranca;

namespace DominoQuina.Core.Api;

/// <summary>
/// Cliente HTTP da API. Envia o Bearer token em todas as rotas protegidas e,
/// nas ações e consultas privadas, a chave do cabeçalho <c>X-Jogador-Ativacao</c>.
/// </summary>
public sealed class DominoApiClient(HttpClient http, IProvedorCredenciais credenciais) : IDominoApiClient
{
    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);

    public async Task SolicitarTokenAsync(string email, CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Post, Rotas.SolicitarToken, new SolicitacaoToken(email), autenticar: false);
        await EnviarAsync(requisicao, ct);
    }

    public async Task<IReadOnlyList<PerfilJogador>> ListarPerfisAsync(CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Get, Rotas.Jogadores);
        return await EnviarAsync(requisicao, ct) is { } json ? MapeadorRespostas.MapearPerfis(json) : [];
    }

    public async Task AtivarClienteAsync(string jogadorId, string chaveAtivacao, CancellationToken ct = default)
    {
        // A chave vai no corpo; o cabeçalho de ativação só é usado depois de ativado.
        using var requisicao = Criar(HttpMethod.Post, Rotas.AtivarCliente(jogadorId), new SolicitacaoAtivacao(chaveAtivacao));
        await EnviarAsync(requisicao, ct);
    }

    public async Task DesativarClienteAsync(string jogadorId, CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Delete, Rotas.DesativarCliente(jogadorId), jogadorId: jogadorId);
        await EnviarAsync(requisicao, ct);
    }

    public async Task<IReadOnlyList<ResumoPartida>> ListarPartidasAsync(string jogadorId, CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Get, Rotas.Partidas, jogadorId: jogadorId);
        return await EnviarAsync(requisicao, ct) is { } json ? MapeadorRespostas.MapearPartidas(json, jogadorId) : [];
    }

    public async Task<string> CriarPartidaAsync(string jogadorId, int pontuacaoAlvo, CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Post, Rotas.Partidas, new SolicitacaoCriarPartida(pontuacaoAlvo, jogadorId), jogadorId: jogadorId);
        var json = await EnviarAsync(requisicao, ct);
        var id = json is { } j ? LeitorJson.Identificador(j, "id", "partidaId") ?? LerTextoSimples(j) : null;
        return id ?? throw new InvalidOperationException("A API criou a partida, mas não informou o id.");
    }

    public async Task EntrarPartidaAsync(string partidaId, string jogadorId, CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Post, Rotas.Entrar(partidaId), new SolicitacaoEntrarPartida(jogadorId), jogadorId: jogadorId);
        await EnviarAsync(requisicao, ct);
    }

    public Task<EstadoPartida?> IniciarPartidaAsync(string partidaId, ComandoTurno comando, CancellationToken ct = default) =>
        EnviarComandoAsync(Rotas.Iniciar(partidaId), comando, comando.JogadorId, ct);

    public async Task<EstadoPartida> ObterEstadoAsync(string partidaId, string jogadorId, CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Get, Rotas.Estado(partidaId, jogadorId), jogadorId: jogadorId);
        var json = await EnviarAsync(requisicao, ct) ?? throw new InvalidOperationException("A API não devolveu o estado da partida.");
        return MapeadorRespostas.MapearEstado(json, jogadorId);
    }

    public async Task<EstadoPartida?> ObterAtualizacoesAsync(string partidaId, string jogadorId, long versaoConhecida, CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Get, Rotas.Atualizacoes(partidaId, jogadorId, versaoConhecida), jogadorId: jogadorId);
        return await EnviarAsync(requisicao, ct) is { } json ? MapeadorRespostas.MapearEstado(json, jogadorId) : null;
    }

    public Task<EstadoPartida?> JogarAsync(string partidaId, ComandoJogada comando, CancellationToken ct = default) =>
        EnviarComandoAsync(Rotas.Jogar(partidaId), comando, comando.JogadorId, ct);

    public Task<EstadoPartida?> ComprarAsync(string partidaId, ComandoTurno comando, CancellationToken ct = default) =>
        EnviarComandoAsync(Rotas.Comprar(partidaId), comando, comando.JogadorId, ct);

    public Task<EstadoPartida?> PassarAsync(string partidaId, ComandoTurno comando, CancellationToken ct = default) =>
        EnviarComandoAsync(Rotas.Passar(partidaId), comando, comando.JogadorId, ct);

    public async Task<ResultadoPartida> ObterResultadoAsync(string partidaId, string jogadorId, CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Get, Rotas.Resultado(partidaId, jogadorId), jogadorId: jogadorId);
        var json = await EnviarAsync(requisicao, ct) ?? throw new InvalidOperationException("A API não devolveu o resultado.");
        return MapeadorRespostas.MapearResultado(json, jogadorId);
    }

    /// <summary>Envia um comando de turno e devolve o estado confirmado que vem na resposta.</summary>
    private async Task<EstadoPartida?> EnviarComandoAsync(string rota, object comando, string jogadorId, CancellationToken ct)
    {
        using var requisicao = Criar(HttpMethod.Post, rota, comando, jogadorId: jogadorId);
        return await EnviarAsync(requisicao, ct) is { } json ? MapeadorRespostas.MapearEstado(json, jogadorId) : null;
    }

    private HttpRequestMessage Criar(HttpMethod metodo, string rota, object? corpo = null, string? jogadorId = null, bool autenticar = true)
    {
        var requisicao = new HttpRequestMessage(metodo, rota);

        if (autenticar && !string.IsNullOrWhiteSpace(credenciais.Token))
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credenciais.Token);

        if (jogadorId is not null && credenciais.ObterChaveAtivacao(jogadorId) is { } chave)
            requisicao.Headers.Add(Rotas.CabecalhoAtivacao, chave);

        if (corpo is not null)
            requisicao.Content = JsonContent.Create(corpo, corpo.GetType(), options: OpcoesJson);

        return requisicao;
    }

    /// <summary>Envia a requisição; devolve o JSON da resposta, ou <c>null</c> se ela vier vazia (ex.: 202, 204).</summary>
    private async Task<JsonElement?> EnviarAsync(HttpRequestMessage requisicao, CancellationToken ct)
    {
        using var resposta = await http.SendAsync(requisicao, ct);
        var corpo = resposta.Content is null ? "" : await resposta.Content.ReadAsStringAsync(ct);

        if (!resposta.IsSuccessStatusCode)
            throw ApiException.De(resposta.StatusCode, corpo, LerRetryAfter(resposta));

        if (resposta.StatusCode == HttpStatusCode.NoContent || string.IsNullOrWhiteSpace(corpo))
            return null;

        try
        {
            using var documento = JsonDocument.Parse(corpo);
            return documento.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonDocument.Parse(JsonSerializer.Serialize(corpo)).RootElement.Clone();
        }
    }

    private static TimeSpan? LerRetryAfter(HttpResponseMessage resposta)
    {
        var retry = resposta.Headers.RetryAfter;
        if (retry?.Delta is { } delta) return delta;
        if (retry?.Date is { } data) return data - DateTimeOffset.UtcNow;
        return null;
    }

    private static string? LerTextoSimples(JsonElement json) =>
        json.ValueKind switch
        {
            JsonValueKind.String => json.GetString(),
            JsonValueKind.Number => json.GetRawText(),
            _ => null
        };
}
