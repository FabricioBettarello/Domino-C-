using DominoQuina.Core.Modelos;

namespace DominoQuina.Core.Api;

// Corpos das requisições. São serializados em camelCase (ex.: VersaoEsperada -> versaoEsperada).

public sealed record SolicitacaoToken(string Email);

public sealed record SolicitacaoAtivacao(string ChaveAtivacao);

public sealed record SolicitacaoCriarPartida(int PontuacaoAlvo, string JogadorId);

public sealed record SolicitacaoEntrarPartida(string JogadorId);

/// <summary>Comando de turno sem pedra (iniciar, comprar, passar).</summary>
public sealed record ComandoTurno(string JogadorId, long VersaoEsperada, Guid ChaveIdempotencia);

/// <summary>Comando de jogada: pedra escolhida e ponta (lado 0 = esquerda, 1 = direita).</summary>
public sealed record ComandoJogada(
    string JogadorId,
    long VersaoEsperada,
    Guid ChaveIdempotencia,
    int ValorA,
    int ValorB,
    LadoMesa Lado);
