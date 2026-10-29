namespace DominoQuina.Core.Jogo;

/// <summary>
/// Joga sozinho: quando o estado confirmado indica a vez deste perfil,
/// pede uma decisão à estratégia e envia a ação pela sessão.
/// </summary>
public sealed class JogadorAutonomo(IEstrategiaJogo estrategia)
{
    /// <summary>Última decisão tomada, para exibir na tela.</summary>
    public Decisao? UltimaDecisao { get; private set; }

    /// <summary>Age uma vez se for a vez do jogador. Retorna <c>null</c> quando não havia nada a fazer.</summary>
    public async Task<ResultadoAcao?> JogarVezAsync(SessaoPartida sessao, CancellationToken ct = default)
    {
        if (sessao.Estado is not { MinhaVez: true } estado || sessao.AcaoEmAndamento) return null;

        var decisao = estrategia.Decidir(estado);
        UltimaDecisao = decisao;

        return decisao.Tipo switch
        {
            TipoDecisao.Jogar when decisao.Pedra is { } pedra => await sessao.JogarAsync(pedra, decisao.Lado, ct),
            TipoDecisao.Comprar => await sessao.ComprarAsync(ct),
            TipoDecisao.Passar => await sessao.PassarAsync(ct),
            _ => null
        };
    }
}
