using DominoQuina.Core.Api;
using DominoQuina.Core.Jogo;
using DominoQuina.Core.Seguranca;
using DominoQuina.Web;
using DominoQuina.Web.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.Configure<OpcoesApi>(builder.Configuration.GetSection(OpcoesApi.Secao));
builder.Services.AddSingleton(TimeProvider.System);

// Token e chaves de ativação ficam em arquivo protegido pelo ASP.NET Data Protection.
builder.Services.AddDataProtection().SetApplicationName("DominoQuina");
builder.Services.AddSingleton<IProtetorDados, ProtetorDadosAspNet>();
builder.Services.AddSingleton<IArmazenamentoCredenciais>(sp =>
{
    var configurado = builder.Configuration["Credenciais:Arquivo"];
    var caminho = string.IsNullOrWhiteSpace(configurado)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DominoQuina", "credenciais.dat")
        : configurado;
    return new ArmazenamentoCredenciaisEmArquivo(caminho, sp.GetRequiredService<IProtetorDados>());
});
builder.Services.AddSingleton<IProvedorCredenciais>(sp => sp.GetRequiredService<IArmazenamentoCredenciais>());

builder.Services.AddHttpClient<IDominoApiClient, DominoApiClient>((sp, http) =>
{
    var opcoes = sp.GetRequiredService<IOptions<OpcoesApi>>().Value;
    http.BaseAddress = new Uri(opcoes.BaseUrl.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(opcoes.TimeoutSegundos);
});

builder.Services.AddTransient<GerenciadorAtivacao>();
builder.Services.AddSingleton<IEstrategiaJogo, EstrategiaPontaDeQuina>();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
