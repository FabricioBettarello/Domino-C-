using DominoQuina.Core.Modelos;

namespace DominoQuina.Core.Jogo;

public enum TipoDecisao
{
    Aguardar,
    Jogar,
    Comprar,
    Passar
}

/// <summary>O que o jogador autônomo decidiu fazer e por quê.</summary>
public sealed record Decisao(TipoDecisao Tipo, Pedra? Pedra, LadoMesa Lado, string Motivo)
{
    public static Decisao Aguardar(string motivo) => new(TipoDecisao.Aguardar, null, LadoMesa.Esquerda, motivo);
    public static Decisao Jogar(Pedra pedra, LadoMesa lado, string motivo) => new(TipoDecisao.Jogar, pedra, lado, motivo);
    public static Decisao Comprar() => new(TipoDecisao.Comprar, null, LadoMesa.Esquerda, "Nenhuma pedra encaixa; comprando da cava.");
    public static Decisao Passar() => new(TipoDecisao.Passar, null, LadoMesa.Esquerda, "Sem encaixe e sem cava; passando a vez.");
}

/// <summary>Estratégia de jogo usada pelo jogador autônomo.</summary>
public interface IEstrategiaJogo
{
    Decisao Decidir(EstadoPartida estado);
}

/// <summary>
/// Estratégia gulosa da Ponta de Quina:
/// 1) joga a pedra que mais pontua agora (soma das pontas múltipla de 5);
/// 2) no empate, prefere duplas (difíceis de encaixar depois);
/// 3) depois, a pedra de maior soma (reduz pontos na mão se a mesa travar).
/// Sem encaixe, compra se o estado permitir; senão, passa.
/// </summary>
public sealed class EstrategiaPontaDeQuina : IEstrategiaJogo
{
    public Decisao Decidir(EstadoPartida estado)
    {
        if (estado.Encerrada) return Decisao.Aguardar("Partida encerrada.");
        if (!estado.MinhaVez) return Decisao.Aguardar("Aguardando a vez do adversário.");

        if (estado.PodeJogar || estado.ProximaAcao == ProximaAcao.Desconhecida)
        {
            var melhor = RegrasMesa.JogadasValidas(estado)
                .OrderByDescending(j => j.Pontos)
                .ThenByDescending(j => j.Pedra.EhDupla)
                .ThenByDescending(j => j.Pedra.Soma)
                .FirstOrDefault();

            if (melhor is not null)
            {
                var motivo = estado.Mesa.Count == 0
                    ? $"Abertura com {melhor.Pedra}."
                    : $"{melhor.Pedra} na {(melhor.Lado == LadoMesa.Esquerda ? "esquerda" : "direita")} ({melhor.Pontos} ponto(s)).";
                return Decisao.Jogar(melhor.Pedra, melhor.Lado, motivo);
            }
        }

        if (estado.PodeComprar) return Decisao.Comprar();
        if (estado.PodePassar) return Decisao.Passar();
        return Decisao.Aguardar("O estado não indica nenhuma ação possível.");
    }
}
