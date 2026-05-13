using Aurora.Web.Components;
using Aurora.Web.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.AddOptions<PhaseZeroSessionOptions>()
    .Bind(builder.Configuration.GetSection(PhaseZeroSessionOptions.SectionName));
builder.Services.AddSingleton<BaselineContentCatalogService>();
builder.Services.AddSingleton<WebCharacterEngineService>();
builder.Services.AddScoped<PhaseZeroSessionWorkspaceService>();
builder.Services.AddScoped<WebContentCatalogService>();
builder.Services.AddScoped<WebCharacterSessionService>();
builder.Services.AddHostedService<PhaseZeroSessionCleanupService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
