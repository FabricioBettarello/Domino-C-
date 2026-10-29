namespace DominoQuina.Core.Modelos;

/// <summary>
/// Uma pedra de dominó com dois valores de 0 a 6.
/// A ordem dos valores só importa para exibir a pedra orientada na mesa.
/// </summary>
public readonly record struct Pedra(int ValorA, int ValorB)
{
    /// <summary>Pedra com os dois lados iguais (ex.: [6|6]).</summary>
    public bool EhDupla => ValorA == ValorB;

    /// <summary>Soma dos dois lados, usada para desempatar escolhas.</summary>
    public int Soma => ValorA + ValorB;

    /// <summary>Indica se algum lado da pedra combina com o valor da ponta.</summary>
    public bool Encaixa(int ponta) => ValorA == ponta || ValorB == ponta;

    /// <summary>Valor que fica exposto depois de encaixar a pedra na ponta informada.</summary>
    public int ValorExposto(int ponta) => ValorA == ponta ? ValorB : ValorA;

    /// <summary>A mesma pedra com os lados trocados.</summary>
    public Pedra Invertida() => new(ValorB, ValorA);

    /// <summary>Compara duas pedras ignorando a orientação ([6|1] é a mesma pedra que [1|6]).</summary>
    public bool MesmaPedra(Pedra outra) =>
        (ValorA == outra.ValorA && ValorB == outra.ValorB) ||
        (ValorA == outra.ValorB && ValorB == outra.ValorA);

    public override string ToString() => $"[{ValorA}|{ValorB}]";
}
