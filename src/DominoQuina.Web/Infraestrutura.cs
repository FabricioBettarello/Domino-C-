using DominoQuina.Core.Seguranca;
using Microsoft.AspNetCore.DataProtection;

namespace DominoQuina.Web;

/// <summary>Configuração da seção "DominoApi" do appsettings.json.</summary>
public sealed class OpcoesApi
{
    public const string Secao = "DominoApi";

    public string BaseUrl { get; set; } = "https://domino-quina-atbkg6ckcwbpd5gz.westus3-01.azurewebsites.net/";
    public int TimeoutSegundos { get; set; } = 20;

    /// <summary>Intervalo entre consultas a /atualizacoes enquanto a mesa está aberta.</summary>
    public int IntervaloAtualizacaoMs { get; set; } = 1500;
}

/// <summary>Protege o arquivo de credenciais com ASP.NET Core Data Protection (DPAPI no Windows).</summary>
public sealed class ProtetorDadosAspNet(IDataProtectionProvider provedor) : IProtetorDados
{
    private readonly IDataProtector _protetor = provedor.CreateProtector("DominoQuina.Credenciais.v1");

    public string Proteger(string texto) => _protetor.Protect(texto);

    public string Desproteger(string textoProtegido) => _protetor.Unprotect(textoProtegido);
}
