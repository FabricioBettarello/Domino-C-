using DominoQuina.Core.Api;

namespace DominoQuina.Core.Seguranca;

/// <summary>
/// Ativa e libera jogadores-perfis neste cliente.
/// A mesma chave é reutilizada ao reabrir o app; só uma liberação explícita a descarta.
/// </summary>
public sealed class GerenciadorAtivacao(IDominoApiClient api, IArmazenamentoCredenciais armazenamento)
{
    /// <summary>
    /// Ativa (ou retoma) o perfil com a chave deste cliente. Repetir é seguro: a API
    /// reconhece a ativação existente. Em 409 JOGADOR_JA_ATIVO a chave é mantida.
    /// </summary>
    public async Task AtivarAsync(string jogadorId, CancellationToken ct = default)
    {
        var chave = armazenamento.ObterOuCriarChaveAtivacao(jogadorId);
        await api.AtivarClienteAsync(jogadorId, chave, ct);
    }

    /// <summary>Libera o perfil para ser ativado em outro cliente e descarta a chave local.</summary>
    public async Task LiberarAsync(string jogadorId, CancellationToken ct = default)
    {
        await api.DesativarClienteAsync(jogadorId, ct);
        armazenamento.RemoverChaveAtivacao(jogadorId);
    }
}
