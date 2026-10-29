using DominoQuina.Core.Modelos;

namespace DominoQuina.Core.Jogo;

/// <summary>Uma jogada possível: pedra da mão, ponta escolhida e pontos que renderia.</summary>
public sealed record JogadaPossivel(Pedra Pedra, LadoMesa Lado, int Pontos);

/// <summary>
/// Regras locais usadas para exibir a mesa e sugerir jogadas.
/// A API continua sendo a autoridade: ela valida a jogada e informa a pontuação oficial.
/// </summary>
public static class RegrasMesa
{
    /// <summary>Pontos da Ponta de Quina: soma das pontas múltipla de 5 vale soma ÷ 5.</summary>
    public static int PontosPorSoma(int somaDasPontas) =>
        somaDasPontas > 0 && somaDasPontas % 5 == 0 ? somaDasPontas / 5 : 0;

    /// <summary>Orienta a cadeia para que lados vizinhos se toquem com o mesmo valor.</summary>
    public static IReadOnlyList<Pedra> OrientarCadeia(IReadOnlyList<Pedra> pedras)
    {
        if (pedras.Count < 2) return pedras;

        var resultado = new List<Pedra>(pedras.Count);
        var primeira = pedras[0];
        // Se a primeira só encaixa na segunda pelo lado A, ela está invertida.
        if (!pedras[1].Encaixa(primeira.ValorB) && pedras[1].Encaixa(primeira.ValorA))
            primeira = primeira.Invertida();
        resultado.Add(primeira);

        for (var i = 1; i < pedras.Count; i++)
        {
            var anterior = resultado[^1];
            var atual = pedras[i];
            resultado.Add(atual.ValorA == anterior.ValorB || !atual.Encaixa(anterior.ValorB) ? atual : atual.Invertida());
        }

        return resultado;
    }

    /// <summary>Todas as jogadas válidas da mão nas pontas atuais, com os pontos de cada uma.</summary>
    public static IReadOnlyList<JogadaPossivel> JogadasValidas(IEnumerable<Pedra> mao, int pontaEsquerda, int pontaDireita)
    {
        var jogadas = new List<JogadaPossivel>();
        foreach (var pedra in mao)
        {
            if (pedra.Encaixa(pontaEsquerda))
                jogadas.Add(new JogadaPossivel(pedra, LadoMesa.Esquerda,
                    PontosPorSoma(pedra.ValorExposto(pontaEsquerda) + pontaDireita)));

            if (pedra.Encaixa(pontaDireita))
                jogadas.Add(new JogadaPossivel(pedra, LadoMesa.Direita,
                    PontosPorSoma(pontaEsquerda + pedra.ValorExposto(pontaDireita))));
        }
        return jogadas;
    }

    /// <summary>Jogadas válidas a partir do estado; na abertura, só a pedra obrigatória (ou qualquer uma, se não indicada).</summary>
    public static IReadOnlyList<JogadaPossivel> JogadasValidas(EstadoPartida estado)
    {
        if (estado.Mesa.Count == 0)
        {
            var candidatas = estado.PedraAbertura is { } obrigatoria
                ? estado.MinhaMao.Where(p => p.MesmaPedra(obrigatoria))
                : estado.MinhaMao;
            return candidatas.Select(p => new JogadaPossivel(p, LadoMesa.Esquerda, PontosPorSoma(p.Soma))).ToList();
        }

        if (estado.PontaEsquerda is not { } esquerda || estado.PontaDireita is not { } direita) return [];
        return JogadasValidas(estado.MinhaMao, esquerda, direita);
    }
}

/// <summary>Regra futura do slide 14: desconto por demora para agir depois de receber o turno.</summary>
public static class PenalidadeDemora
{
    public static int CalcularDesconto(TimeSpan tempoNoTurno) => tempoNoTurno.TotalSeconds switch
    {
        <= 10 => 0,
        <= 30 => 1,
        < 60 => 2,
        _ => 3
    };
}
