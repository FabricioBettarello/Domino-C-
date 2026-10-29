namespace DominoQuina.Core.Modelos;

/// <summary>
/// Visão da partida confirmada pela API para um jogador.
/// Contém a mão própria, mas nunca a mão do adversário (apenas a quantidade de pedras).
/// </summary>
public sealed record EstadoPartida
{
    public string PartidaId { get; init; } = "";

    /// <summary>Versão do estado. Comandos enviam esta versão como <c>versaoEsperada</c>.</summary>
    public long Versao { get; init; }

    public int Rodada { get; init; }
    public int? Turno { get; init; }
    public string Situacao { get; init; } = "";
    public int PontuacaoAlvo { get; init; }
    public int QuantidadeJogadores { get; init; }
    public string? JogadorDaVezId { get; init; }
    public ProximaAcao ProximaAcao { get; init; }
    public IReadOnlyList<string> AcoesPermitidas { get; init; } = [];

    public IReadOnlyList<Pedra> MinhaMao { get; init; } = [];

    /// <summary>Pedras jogadas, da ponta esquerda para a direita.</summary>
    public IReadOnlyList<Pedra> Mesa { get; init; } = [];

    public int? PontaEsquerda { get; init; }
    public int? PontaDireita { get; init; }

    /// <summary>Pedra obrigatória de abertura, quando a API indica.</summary>
    public Pedra? PedraAbertura { get; init; }

    public int PedrasAdversario { get; init; }
    public int? PedrasNaCava { get; init; }
    public IReadOnlyList<PontuacaoJogador> Placar { get; init; } = [];
    public string? VencedorId { get; init; }

    /// <summary>JSON recebido, mantido para conferência na tela.</summary>
    public string JsonBruto { get; init; } = "";

    public bool MinhaVez => ProximaAcao is ProximaAcao.JogarAbertura or ProximaAcao.Jogar
        or ProximaAcao.Comprar or ProximaAcao.Passar;

    public bool Encerrada => ProximaAcao == ProximaAcao.Encerrar || VencedorId is not null;

    /// <summary>Sala de espera: ainda não houve distribuição de pedras.</summary>
    public bool EmEspera => !Encerrada && MinhaMao.Count == 0 && Mesa.Count == 0;

    public bool PodeJogar => ProximaAcao is ProximaAcao.Jogar or ProximaAcao.JogarAbertura || Permite("jogar");
    public bool PodeComprar => ProximaAcao == ProximaAcao.Comprar || Permite("comprar");
    public bool PodePassar => ProximaAcao == ProximaAcao.Passar || Permite("passar");

    public int MeusPontos => Placar.FirstOrDefault(p => p.SouEu)?.Pontos ?? 0;
    public int PontosAdversario => Placar.FirstOrDefault(p => !p.SouEu)?.Pontos ?? 0;

    private bool Permite(string acao) =>
        AcoesPermitidas.Any(a => string.Equals(a, acao, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Pontuação acumulada de um participante.</summary>
public sealed record PontuacaoJogador(string JogadorId, string? Nome, int Pontos, bool SouEu);

/// <summary>Linha da lista de partidas (sala de partidas).</summary>
public sealed record ResumoPartida
{
    public string Id { get; init; } = "";
    public int PontuacaoAlvo { get; init; }
    public int QuantidadeJogadores { get; init; }
    public int MaximoJogadores { get; init; } = 2;
    public string Situacao { get; init; } = "";

    /// <summary>Indica se o jogador consultado já participa da partida.</summary>
    public bool Participo { get; init; }

    public bool TemVaga => QuantidadeJogadores < MaximoJogadores;
}

/// <summary>Jogador-perfil pertencente à conta autenticada.</summary>
public sealed record PerfilJogador(string Id, string? Nome);

/// <summary>Resultado final informado pela API.</summary>
public sealed record ResultadoPartida
{
    public string? VencedorId { get; init; }
    public bool Venci { get; init; }
    public string Situacao { get; init; } = "";
    public IReadOnlyList<PontuacaoJogador> Placar { get; init; } = [];
    public string JsonBruto { get; init; } = "";
}
