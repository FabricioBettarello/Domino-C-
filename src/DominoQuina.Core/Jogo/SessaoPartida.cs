using System.Net;
using DominoQuina.Core.Api;
using DominoQuina.Core.Modelos;

namespace DominoQuina.Core.Jogo;

/// <summary>Resultado de uma ação enviada à API. Falhas nunca são mostradas como sucesso.</summary>
public sealed record ResultadoAcao(bool Sucesso, string Mensagem, ApiException? Erro = null)
{
    public static ResultadoAcao Ok(string mensagem) => new(true, mensagem);
    public static ResultadoAcao Falha(ApiException erro) => new(false, erro.Orientacao, erro);
    public static ResultadoAcao Falha(string mensagem) => new(false, mensagem);
}

/// <summary>
/// Acompanha uma partida para um jogador-perfil:
/// guarda o último estado confirmado, aplica apenas versões mais novas,
/// envia comandos com versão esperada + chave de idempotência e mede o tempo no turno.
/// </summary>
public sealed class SessaoPartida(IDominoApiClient api, string partidaId, string jogadorId, TimeProvider? relogio = null)
{
    /// <summary>Tentativas da mesma intenção quando a resposta se perde (rede/timeout).</summary>
    public const int MaximoTentativas = 3;

    private readonly TimeProvider _relogio = relogio ?? TimeProvider.System;
    private readonly SemaphoreSlim _acaoEmAndamento = new(1, 1);

    public string PartidaId { get; } = partidaId;
    public string JogadorId { get; } = jogadorId;
    public EstadoPartida? Estado { get; private set; }

    /// <summary>Momento em que este cliente recebeu o turno (base da regra de desconto por demora).</summary>
    public DateTimeOffset? InicioDoTurno { get; private set; }

    public TimeSpan TempoNoTurno => InicioDoTurno is { } inicio ? _relogio.GetUtcNow() - inicio : TimeSpan.Zero;

    public int DescontoPrevisto => PenalidadeDemora.CalcularDesconto(TempoNoTurno);

    public bool AcaoEmAndamento => _acaoEmAndamento.CurrentCount == 0;

    /// <summary>Busca a visão completa atual (entrada na mesa ou reconexão).</summary>
    public async Task<EstadoPartida> CarregarAsync(CancellationToken ct = default)
    {
        var estado = await api.ObterEstadoAsync(PartidaId, JogadorId, ct);
        Aplicar(estado, aceitarMesmaVersao: true);
        return Estado!;
    }

    /// <summary>Consulta atualizações. Retorna <c>true</c> se chegou estado mais novo (200); <c>false</c> em 204.</summary>
    public async Task<bool> SincronizarAsync(CancellationToken ct = default)
    {
        if (Estado is null)
        {
            await CarregarAsync(ct);
            return true;
        }

        var novo = await api.ObterAtualizacoesAsync(PartidaId, JogadorId, Estado.Versao, ct);
        return novo is not null && Aplicar(novo);
    }

    /// <summary>Aplica um estado somente se a versão for mais nova que a conhecida.</summary>
    public bool Aplicar(EstadoPartida novo, bool aceitarMesmaVersao = false)
    {
        var atual = Estado;
        if (atual is not null && (novo.Versao < atual.Versao || (novo.Versao == atual.Versao && !aceitarMesmaVersao)))
            return false;

        Estado = novo;

        if (!novo.MinhaVez) InicioDoTurno = null;
        else if (atual is null || !atual.MinhaVez || InicioDoTurno is null) InicioDoTurno = _relogio.GetUtcNow();

        return atual is null || novo.Versao > atual.Versao;
    }

    public Task<ResultadoAcao> IniciarAsync(CancellationToken ct = default) =>
        ExecutarAsync("Partida iniciada.", (versao, chave, token) =>
            api.IniciarPartidaAsync(PartidaId, new ComandoTurno(JogadorId, versao, chave), token), ct);

    public Task<ResultadoAcao> JogarAsync(Pedra pedra, LadoMesa lado, CancellationToken ct = default) =>
        ExecutarAsync($"Jogada {pedra} confirmada.", (versao, chave, token) =>
            api.JogarAsync(PartidaId, new ComandoJogada(JogadorId, versao, chave, pedra.ValorA, pedra.ValorB, lado), token), ct);

    public Task<ResultadoAcao> ComprarAsync(CancellationToken ct = default) =>
        ExecutarAsync("Compra confirmada.", (versao, chave, token) =>
            api.ComprarAsync(PartidaId, new ComandoTurno(JogadorId, versao, chave), token), ct);

    public Task<ResultadoAcao> PassarAsync(CancellationToken ct = default) =>
        ExecutarAsync("Passagem confirmada.", (versao, chave, token) =>
            api.PassarAsync(PartidaId, new ComandoTurno(JogadorId, versao, chave), token), ct);

    public Task<ResultadoPartida> ObterResultadoAsync(CancellationToken ct = default) =>
        api.ObterResultadoAsync(PartidaId, JogadorId, ct);

    /// <summary>
    /// Envia uma intenção de ação. A chave de idempotência é gerada uma vez por intenção
    /// e repetida nas novas tentativas, então a API não duplica a jogada.
    /// </summary>
    private async Task<ResultadoAcao> ExecutarAsync(
        string mensagemSucesso,
        Func<long, Guid, CancellationToken, Task<EstadoPartida?>> enviar,
        CancellationToken ct)
    {
        if (!await _acaoEmAndamento.WaitAsync(0, ct))
            return ResultadoAcao.Falha("Já existe uma ação em andamento. Aguarde a confirmação.");

        try
        {
            var versao = Estado?.Versao ?? 0;
            var chaveIdempotencia = Guid.NewGuid();

            for (var tentativa = 1; ; tentativa++)
            {
                try
                {
                    var confirmado = await enviar(versao, chaveIdempotencia, ct);
                    if (confirmado is not null) Aplicar(confirmado);
                    else await CarregarAsync(ct);
                    return ResultadoAcao.Ok(mensagemSucesso);
                }
                catch (ApiException erro)
                {
                    // 409 de versão: a mesa mudou; traz o estado oficial antes de o jogador decidir de novo.
                    if (erro.ConflitoDeVersao || erro.StatusCode == HttpStatusCode.UnprocessableEntity)
                        await TentarRecarregarAsync(ct);
                    return ResultadoAcao.Falha(erro);
                }
                catch (Exception erro) when (EhFalhaTransitoria(erro, ct))
                {
                    if (tentativa >= MaximoTentativas)
                    {
                        await TentarRecarregarAsync(ct);
                        return ResultadoAcao.Falha("Sem resposta da API. A ação pode não ter sido registrada; a mesa foi recarregada.");
                    }
                    await Task.Delay(TimeSpan.FromMilliseconds(400 * tentativa), _relogio, ct);
                }
            }
        }
        finally
        {
            _acaoEmAndamento.Release();
        }
    }

    private async Task TentarRecarregarAsync(CancellationToken ct)
    {
        try { await CarregarAsync(ct); }
        catch (Exception erro) when (erro is ApiException || EhFalhaTransitoria(erro, ct)) { /* mantém o último estado confirmado */ }
    }

    private static bool EhFalhaTransitoria(Exception erro, CancellationToken ct) =>
        erro is HttpRequestException || (erro is TaskCanceledException && !ct.IsCancellationRequested);
}
