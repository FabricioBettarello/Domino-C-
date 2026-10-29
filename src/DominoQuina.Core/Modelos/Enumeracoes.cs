namespace DominoQuina.Core.Modelos;

/// <summary>Ponta da mesa onde a pedra será encaixada. O valor numérico é o enviado à API.</summary>
public enum LadoMesa
{
    Esquerda = 0,
    Direita = 1
}

/// <summary>Próxima ação que o estado da partida indica para este jogador.</summary>
public enum ProximaAcao
{
    Desconhecida,
    Aguardar,
    JogarAbertura,
    Jogar,
    Comprar,
    Passar,
    Encerrar
}
