using DominoQuina.Core.Modelos;

namespace DominoQuina.Core.Api;

/// <summary>
/// Operações da API do Dominó Ponta de Quina. Toda rejeição vira <see cref="ApiException"/>;
/// o cliente nunca trata uma ação rejeitada como sucesso.
/// </summary>
public interface IDominoApiClient
{
    /// <summary>Pede o token. A API responde 202 e envia o token por e-mail.</summary>
    Task SolicitarTokenAsync(string email, CancellationToken ct = default);

    Task<IReadOnlyList<PerfilJogador>> ListarPerfisAsync(CancellationToken ct = default);

    Task AtivarClienteAsync(string jogadorId, string chaveAtivacao, CancellationToken ct = default);

    Task DesativarClienteAsync(string jogadorId, CancellationToken ct = default);

    Task<IReadOnlyList<ResumoPartida>> ListarPartidasAsync(string jogadorId, CancellationToken ct = default);

    /// <summary>Cria a mesa e devolve o id da partida.</summary>
    Task<string> CriarPartidaAsync(string jogadorId, int pontuacaoAlvo, CancellationToken ct = default);

    Task EntrarPartidaAsync(string partidaId, string jogadorId, CancellationToken ct = default);

    Task<EstadoPartida?> IniciarPartidaAsync(string partidaId, ComandoTurno comando, CancellationToken ct = default);

    Task<EstadoPartida> ObterEstadoAsync(string partidaId, string jogadorId, CancellationToken ct = default);

    /// <summary>Devolve o estado novo (200) ou <c>null</c> quando nada mudou (204).</summary>
    Task<EstadoPartida?> ObterAtualizacoesAsync(string partidaId, string jogadorId, long versaoConhecida, CancellationToken ct = default);

    Task<EstadoPartida?> JogarAsync(string partidaId, ComandoJogada comando, CancellationToken ct = default);

    Task<EstadoPartida?> ComprarAsync(string partidaId, ComandoTurno comando, CancellationToken ct = default);

    Task<EstadoPartida?> PassarAsync(string partidaId, ComandoTurno comando, CancellationToken ct = default);

    Task<ResultadoPartida> ObterResultadoAsync(string partidaId, string jogadorId, CancellationToken ct = default);
}
