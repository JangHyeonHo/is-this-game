// Guildwright.Web(Blazor WASM)의 발행 결과를 서빙하는 초소형 정적 서버.
// .NET SDK가 없는 환경에서 Docker 한 장으로 브라우저 플레이를 가능하게 하는 것이 전부다.
// 게임 로직은 없다 — 그건 코어와 Guildwright.Web의 일이다.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();
