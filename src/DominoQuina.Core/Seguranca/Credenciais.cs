using System.Security.Cryptography;
using System.Text.Json;

namespace DominoQuina.Core.Seguranca;

/// <summary>Fornece ao cliente HTTP o token e a chave de ativação de cada perfil.</summary>
public interface IProvedorCredenciais
{
    string? Token { get; }
    string? ObterChaveAtivacao(string jogadorId);
}

/// <summary>Guarda token, e-mail e chaves de ativação entre execuções do aplicativo.</summary>
public interface IArmazenamentoCredenciais : IProvedorCredenciais
{
    string? Email { get; }
    IReadOnlyCollection<string> PerfisConhecidos { get; }
    void SalvarToken(string email, string token);
    void RemoverToken();

    /// <summary>Devolve a chave já guardada do perfil ou gera e guarda uma nova.</summary>
    string ObterOuCriarChaveAtivacao(string jogadorId);

    void RemoverChaveAtivacao(string jogadorId);
}

/// <summary>Criptografa e descriptografa o conteúdo gravado em disco.</summary>
public interface IProtetorDados
{
    string Proteger(string texto);
    string Desproteger(string textoProtegido);
}

/// <summary>Gera a chave aleatória de ativação: 32 bytes criptograficamente seguros = 64 caracteres hexadecimais.</summary>
public static class GeradorChaveAtivacao
{
    public const int TamanhoEmBytes = 32;

    public static string Gerar() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(TamanhoEmBytes)).ToLowerInvariant();

    public static bool EhValida(string? chave) =>
        chave is { Length: TamanhoEmBytes * 2 } && chave.All(Uri.IsHexDigit);
}

/// <summary>
/// Armazenamento em arquivo. O conteúdo é protegido por <see cref="IProtetorDados"/>
/// (na aplicação web, ASP.NET Data Protection), então token e chaves não ficam em texto puro.
/// </summary>
public sealed class ArmazenamentoCredenciaisEmArquivo : IArmazenamentoCredenciais
{
    private readonly string _caminho;
    private readonly IProtetorDados _protetor;
    private readonly object _trava = new();
    private DadosCredenciais _dados;

    public ArmazenamentoCredenciaisEmArquivo(string caminho, IProtetorDados protetor)
    {
        _caminho = caminho;
        _protetor = protetor;
        _dados = Carregar();
    }

    public string? Token { get { lock (_trava) return _dados.Token; } }
    public string? Email { get { lock (_trava) return _dados.Email; } }

    public IReadOnlyCollection<string> PerfisConhecidos
    {
        get { lock (_trava) return _dados.ChavesAtivacao.Keys.ToList(); }
    }

    public string? ObterChaveAtivacao(string jogadorId)
    {
        lock (_trava) return _dados.ChavesAtivacao.GetValueOrDefault(jogadorId);
    }

    public void SalvarToken(string email, string token)
    {
        lock (_trava)
        {
            _dados = _dados with { Email = email.Trim(), Token = token.Trim() };
            Gravar();
        }
    }

    public void RemoverToken()
    {
        lock (_trava)
        {
            _dados = _dados with { Token = null };
            Gravar();
        }
    }

    public string ObterOuCriarChaveAtivacao(string jogadorId)
    {
        lock (_trava)
        {
            if (_dados.ChavesAtivacao.TryGetValue(jogadorId, out var existente)) return existente;

            var chave = GeradorChaveAtivacao.Gerar();
            _dados.ChavesAtivacao[jogadorId] = chave;
            Gravar();
            return chave;
        }
    }

    public void RemoverChaveAtivacao(string jogadorId)
    {
        lock (_trava)
        {
            if (_dados.ChavesAtivacao.Remove(jogadorId)) Gravar();
        }
    }

    private DadosCredenciais Carregar()
    {
        if (!File.Exists(_caminho)) return new DadosCredenciais();

        try
        {
            var json = _protetor.Desproteger(File.ReadAllText(_caminho));
            return JsonSerializer.Deserialize<DadosCredenciais>(json) ?? new DadosCredenciais();
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
        {
            // Arquivo de outra máquina ou corrompido: começa do zero em vez de travar o app.
            return new DadosCredenciais();
        }
    }

    private void Gravar()
    {
        var pasta = Path.GetDirectoryName(_caminho);
        if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);
        File.WriteAllText(_caminho, _protetor.Proteger(JsonSerializer.Serialize(_dados)));
    }

    private sealed record DadosCredenciais
    {
        public string? Email { get; init; }
        public string? Token { get; init; }
        public Dictionary<string, string> ChavesAtivacao { get; init; } = new();
    }
}
