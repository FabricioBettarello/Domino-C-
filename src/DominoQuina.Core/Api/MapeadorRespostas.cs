using System.Text.Json;
using DominoQuina.Core.Jogo;
using DominoQuina.Core.Modelos;
using static DominoQuina.Core.Api.LeitorJson;

namespace DominoQuina.Core.Api;

/// <summary>
/// Converte o JSON da API nos modelos do cliente. Cada campo aceita alguns nomes
/// alternativos; quando a API não informa algo (ex.: pontas), o valor é derivado da mesa.
/// </summary>
public static class MapeadorRespostas
{
    private static readonly string[] CamposVersao = ["versao", "versaoEstado", "versaoAtual", "version"];

    public static EstadoPartida MapearEstado(JsonElement raiz, string meuJogadorId)
    {
        var json = raiz.GetRawText();

        // A resposta de uma ação pode trazer o estado embrulhado em { "estado": { ... } }.
        if (Longo(raiz, CamposVersao) is null &&
            Propriedade(raiz, "estado", "visao", "estadoPartida", "partida") is { ValueKind: JsonValueKind.Object } interno)
            raiz = interno;

        var mesaJson = Propriedade(raiz, "mesa", "pedrasMesa", "pedrasNaMesa", "tabuleiro", "pedrasJogadas");
        var mesaObjeto = mesaJson is { ValueKind: JsonValueKind.Object } m ? m : (JsonElement?)null;
        var pedrasMesa = mesaObjeto is { } mo
            ? LerPedras(Propriedade(mo, "pedras", "pecas", "jogadas"))
            : LerPedras(mesaJson);
        var mesaOrientada = RegrasMesa.OrientarCadeia(pedrasMesa);

        var (pontaEsquerda, pontaDireita) = LerPontas(raiz, mesaObjeto);
        if ((pontaEsquerda is null || pontaDireita is null) && mesaOrientada.Count > 0)
        {
            pontaEsquerda ??= mesaOrientada[0].ValorA;
            pontaDireita ??= mesaOrientada[^1].ValorB;
        }

        var placar = LerPlacar(raiz, meuJogadorId);
        var vencedorId = Identificador(raiz, "vencedorId", "vencedor", "jogadorVencedorId", "jogadorVencedor");
        var acoes = LerListaTexto(raiz, "acoesPermitidas", "acoes", "acoesDisponiveis");
        var jogadorDaVez = Identificador(raiz, "jogadorDaVezId", "jogadorDaVez", "jogadorAtualId", "turnoJogadorId",
            "vezDoJogadorId", "jogadorTurnoId", "vezDe");
        var minhaMao = LerPedras(Propriedade(raiz, "minhaMao", "mao", "minhasPedras", "maoJogador", "pedrasMao", "pedras"));
        var pedraAbertura = LerPedra(Propriedade(raiz, "pedraAbertura", "pedraAberturaObrigatoria", "aberturaObrigatoria",
            "pedraObrigatoria", "pedraInicial"));

        var proximaAcao = LerProximaAcao(Propriedade(raiz, "proximaAcao", "acaoEsperada", "proximaAcaoPermitida", "acao"));
        if (proximaAcao == ProximaAcao.Desconhecida)
        {
            var minhaVez = Booleano(raiz, "minhaVez", "ehMinhaVez", "suaVez") ??
                           (jogadorDaVez is not null ? jogadorDaVez == meuJogadorId : (bool?)null);
            proximaAcao = minhaVez switch
            {
                _ when vencedorId is not null => ProximaAcao.Encerrar,
                true when pedrasMesa.Count == 0 => ProximaAcao.JogarAbertura,
                true => ProximaAcao.Jogar,
                false => ProximaAcao.Aguardar,
                null => ProximaAcao.Desconhecida
            };
        }

        return new EstadoPartida
        {
            PartidaId = Identificador(raiz, "partidaId", "id") ?? "",
            Versao = Longo(raiz, CamposVersao) ?? 0,
            Rodada = Inteiro(raiz, "rodada", "rodadaAtual", "numeroRodada") ?? 0,
            Turno = Inteiro(raiz, "turno", "turnoAtual", "numeroTurno"),
            Situacao = Texto(raiz, "situacao", "status", "fase") ?? "",
            PontuacaoAlvo = Inteiro(raiz, "pontuacaoAlvo", "alvo", "pontosAlvo") ?? 0,
            QuantidadeJogadores = LerQuantidade(raiz, "quantidadeJogadores", "jogadores", "participantes", "participacoes") ?? placar.Count,
            JogadorDaVezId = jogadorDaVez,
            ProximaAcao = proximaAcao,
            AcoesPermitidas = acoes,
            MinhaMao = minhaMao,
            Mesa = mesaOrientada,
            PontaEsquerda = pontaEsquerda,
            PontaDireita = pontaDireita,
            PedraAbertura = pedraAbertura,
            PedrasAdversario = LerPedrasAdversario(raiz, meuJogadorId),
            PedrasNaCava = LerQuantidade(raiz, "pedrasNaCava", "quantidadeCava", "quantidadePedrasCava", "pedrasCava", "cava", "tamanhoCava"),
            Placar = placar,
            VencedorId = vencedorId,
            JsonBruto = json
        };
    }

    public static IReadOnlyList<ResumoPartida> MapearPartidas(JsonElement raiz, string? meuJogadorId)
    {
        var lista = raiz.ValueKind == JsonValueKind.Array
            ? raiz
            : Propriedade(raiz, "partidas", "itens", "items", "data", "resultado");
        if (lista is not { ValueKind: JsonValueKind.Array } array) return [];

        return array.EnumerateArray().Select(p => MapearResumo(p, meuJogadorId)).Where(p => p.Id.Length > 0).ToList();
    }

    public static ResumoPartida MapearResumo(JsonElement p, string? meuJogadorId)
    {
        var maximo = Inteiro(p, "maximoJogadores", "capacidade", "totalVagas", "limiteJogadores") ?? 2;
        var quantidade = LerQuantidade(p, "quantidadeJogadores", "jogadores", "participantes", "participacoes", "vagasOcupadas");
        if (quantidade is null && Inteiro(p, "vagas", "vagasDisponiveis", "vagasRestantes") is { } vagas)
            quantidade = maximo - vagas;

        var participantes = Propriedade(p, "jogadores", "participantes", "participacoes");
        var participo = meuJogadorId is not null && participantes is { ValueKind: JsonValueKind.Array } arr &&
                        arr.EnumerateArray().Any(j => IdDoParticipante(j) == meuJogadorId);

        return new ResumoPartida
        {
            Id = Identificador(p, "id", "partidaId") ?? "",
            PontuacaoAlvo = Inteiro(p, "pontuacaoAlvo", "alvo", "pontosAlvo") ?? 0,
            QuantidadeJogadores = quantidade ?? 0,
            MaximoJogadores = maximo,
            Situacao = Texto(p, "situacao", "status", "fase") ?? "",
            Participo = participo
        };
    }

    public static IReadOnlyList<PerfilJogador> MapearPerfis(JsonElement raiz)
    {
        var lista = raiz.ValueKind == JsonValueKind.Array ? raiz : Propriedade(raiz, "jogadores", "perfis", "itens", "items");
        if (lista is not { ValueKind: JsonValueKind.Array } array) return [];

        return array.EnumerateArray()
            .Select(j => new PerfilJogador(Identificador(j, "id", "jogadorId") ?? "", Texto(j, "nome", "apelido", "nick")))
            .Where(j => j.Id.Length > 0)
            .ToList();
    }

    public static ResultadoPartida MapearResultado(JsonElement raiz, string meuJogadorId)
    {
        var vencedor = Identificador(raiz, "vencedorId", "vencedor", "jogadorVencedorId", "jogadorVencedor");
        return new ResultadoPartida
        {
            VencedorId = vencedor,
            Venci = Booleano(raiz, "venceu", "voceVenceu", "vitoria") ?? (vencedor is not null && vencedor == meuJogadorId),
            Situacao = Texto(raiz, "situacao", "status") ?? "",
            Placar = LerPlacar(raiz, meuJogadorId),
            JsonBruto = raiz.GetRawText()
        };
    }

    /// <summary>Aceita pedra como objeto ({valorA, valorB}), array ([6,1]) ou texto ("6-1", "[6|1]").</summary>
    public static Pedra? LerPedra(JsonElement? elemento)
    {
        if (elemento is not { } e) return null;

        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                var a = Inteiro(e, "valorA", "ladoA", "a", "valor1", "lado1", "esquerda", "primeiro");
                var b = Inteiro(e, "valorB", "ladoB", "b", "valor2", "lado2", "direita", "segundo");
                if (a is not null && b is not null) return new Pedra(a.Value, b.Value);
                return LerPedra(Propriedade(e, "pedra", "peca"));

            case JsonValueKind.Array:
                var numeros = e.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetInt32()).ToList();
                return numeros.Count == 2 ? new Pedra(numeros[0], numeros[1]) : null;

            case JsonValueKind.String:
                var digitos = (e.GetString() ?? "").Where(char.IsDigit).Select(c => c - '0').ToList();
                return digitos.Count == 2 ? new Pedra(digitos[0], digitos[1]) : null;

            default:
                return null;
        }
    }

    public static IReadOnlyList<Pedra> LerPedras(JsonElement? elemento)
    {
        if (elemento is not { ValueKind: JsonValueKind.Array } array) return [];
        return array.EnumerateArray().Select(x => LerPedra(x)).OfType<Pedra>().ToList();
    }

    public static ProximaAcao LerProximaAcao(JsonElement? elemento)
    {
        if (elemento is not { } e) return ProximaAcao.Desconhecida;

        if (e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var n))
        {
            // Ordem do slide 15: aguardar, jogar abertura, jogar, comprar, passar, encerrar.
            return n switch
            {
                0 => ProximaAcao.Aguardar,
                1 => ProximaAcao.JogarAbertura,
                2 => ProximaAcao.Jogar,
                3 => ProximaAcao.Comprar,
                4 => ProximaAcao.Passar,
                5 => ProximaAcao.Encerrar,
                _ => ProximaAcao.Desconhecida
            };
        }

        if (e.ValueKind != JsonValueKind.String) return ProximaAcao.Desconhecida;

        var texto = Normalizar(e.GetString() ?? "");
        return texto switch
        {
            "aguardar" or "aguardando" or "esperar" => ProximaAcao.Aguardar,
            "jogarabertura" or "abertura" or "abrir" => ProximaAcao.JogarAbertura,
            "jogar" => ProximaAcao.Jogar,
            "comprar" => ProximaAcao.Comprar,
            "passar" => ProximaAcao.Passar,
            "encerrar" or "encerrada" or "encerrado" or "finalizada" => ProximaAcao.Encerrar,
            _ => ProximaAcao.Desconhecida
        };
    }

    private static (int? Esquerda, int? Direita) LerPontas(JsonElement raiz, JsonElement? mesaObjeto)
    {
        foreach (var origem in mesaObjeto is { } mo ? new[] { mo, raiz } : new[] { raiz })
        {
            var esquerda = Inteiro(origem, "pontaEsquerda", "esquerda", "extremidadeEsquerda");
            var direita = Inteiro(origem, "pontaDireita", "direita", "extremidadeDireita");
            if (esquerda is not null || direita is not null) return (esquerda, direita);

            switch (Propriedade(origem, "pontas", "pontasAbertas", "extremidades"))
            {
                case { ValueKind: JsonValueKind.Object } pontas:
                    return (Inteiro(pontas, "esquerda", "pontaEsquerda"), Inteiro(pontas, "direita", "pontaDireita"));
                case { ValueKind: JsonValueKind.Array } pontas:
                    var valores = pontas.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetInt32()).ToList();
                    if (valores.Count == 2) return (valores[0], valores[1]);
                    break;
            }
        }

        return (null, null);
    }

    private static IReadOnlyList<PontuacaoJogador> LerPlacar(JsonElement raiz, string meuJogadorId)
    {
        var placar = Propriedade(raiz, "placar", "pontuacoes", "pontuacao", "jogadores", "participantes", "participacoes");

        if (placar is { ValueKind: JsonValueKind.Array } array)
        {
            return array.EnumerateArray()
                .Where(j => j.ValueKind == JsonValueKind.Object)
                .Select(j =>
                {
                    var id = IdDoParticipante(j) ?? "";
                    return new PontuacaoJogador(id, Texto(j, "nome", "apelido", "nick"),
                        Inteiro(j, "pontos", "pontuacao", "placar", "total") ?? 0,
                        Booleano(j, "souEu", "ehVoce", "voce") ?? id == meuJogadorId);
                })
                .ToList();
        }

        if (placar is { ValueKind: JsonValueKind.Object } mapa)
        {
            var itens = mapa.EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.Number)
                .Select(p => new PontuacaoJogador(p.Name, null, p.Value.GetInt32(), p.Name == meuJogadorId))
                .ToList();
            if (itens.Count > 0) return itens;
        }

        var meus = Inteiro(raiz, "meusPontos", "minhaPontuacao", "pontosJogador");
        var deles = Inteiro(raiz, "pontosAdversario", "pontuacaoAdversario", "pontosOponente");
        if (meus is null && deles is null) return [];
        return
        [
            new PontuacaoJogador(meuJogadorId, "Você", meus ?? 0, true),
            new PontuacaoJogador("", "Oponente", deles ?? 0, false)
        ];
    }

    private static int LerPedrasAdversario(JsonElement raiz, string meuJogadorId)
    {
        if (LerQuantidade(raiz, "quantidadePedrasAdversario", "pedrasAdversario", "qtdPedrasAdversario",
                "pedrasOponente", "quantidadePedrasOponente") is { } quantidade)
            return quantidade;

        // Alternativa: lista de participantes com a quantidade de pedras de cada um.
        if (Propriedade(raiz, "jogadores", "participantes", "participacoes", "placar") is { ValueKind: JsonValueKind.Array } array)
        {
            foreach (var j in array.EnumerateArray())
            {
                if (IdDoParticipante(j) != meuJogadorId &&
                    LerQuantidade(j, "quantidadePedras", "pedrasNaMao", "qtdPedras", "pedras", "mao") is { } n)
                    return n;
            }
        }

        return 0;
    }

    /// <summary>Lê um número ou, se vier uma lista, o tamanho dela.</summary>
    private static int? LerQuantidade(JsonElement elemento, params string[] nomes) =>
        Propriedade(elemento, nomes) switch
        {
            { ValueKind: JsonValueKind.Number } v when v.TryGetInt32(out var n) => n,
            { ValueKind: JsonValueKind.Array } v => v.GetArrayLength(),
            _ => null
        };

    private static IReadOnlyList<string> LerListaTexto(JsonElement elemento, params string[] nomes)
    {
        if (Propriedade(elemento, nomes) is not { ValueKind: JsonValueKind.Array } array) return [];
        return array.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => Normalizar(x.GetString() ?? ""))
            .ToList();
    }

    private static string? IdDoParticipante(JsonElement j) => j.ValueKind switch
    {
        JsonValueKind.Object => Identificador(j, "jogadorId", "id", "perfilId"),
        JsonValueKind.String => j.GetString(),
        _ => null
    };
}
