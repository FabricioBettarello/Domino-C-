namespace DominoQuina.Core.Api;

/// <summary>Rotas da API do Dominó Ponta de Quina, centralizadas para facilitar ajustes.</summary>
public static class Rotas
{
    /// <summary>Cabeçalho que identifica este cliente para o jogador-perfil ativado.</summary>
    public const string CabecalhoAtivacao = "X-Jogador-Ativacao";

    public const string SolicitarToken = "api/identidade/token";
    public const string Jogadores = "api/jogadores";
    public const string Partidas = "api/partidas";

    public static string AtivarCliente(string jogadorId) => $"api/jogadores/{Esc(jogadorId)}/ativar-cliente";
    public static string DesativarCliente(string jogadorId) => $"api/jogadores/{Esc(jogadorId)}/desativar-cliente";

    public static string Entrar(string partidaId) => $"api/partidas/{Esc(partidaId)}/entrar";
    public static string Iniciar(string partidaId) => $"api/partidas/{Esc(partidaId)}/iniciar";
    public static string Jogar(string partidaId) => $"api/partidas/{Esc(partidaId)}/jogar";
    public static string Comprar(string partidaId) => $"api/partidas/{Esc(partidaId)}/comprar";
    public static string Passar(string partidaId) => $"api/partidas/{Esc(partidaId)}/passar";

    public static string Estado(string partidaId, string jogadorId) =>
        $"api/partidas/{Esc(partidaId)}?jogadorId={Esc(jogadorId)}";

    public static string Atualizacoes(string partidaId, string jogadorId, long versaoConhecida) =>
        $"api/partidas/{Esc(partidaId)}/atualizacoes?jogadorId={Esc(jogadorId)}&versaoConhecida={versaoConhecida}";

    public static string Resultado(string partidaId, string jogadorId) =>
        $"api/partidas/{Esc(partidaId)}/resultado?jogadorId={Esc(jogadorId)}";

    private static string Esc(string valor) => Uri.EscapeDataString(valor);
}
