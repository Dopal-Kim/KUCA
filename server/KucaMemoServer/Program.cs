using KucaMemoServer.Endpoints;
using KucaMemoServer.Services;

// KUCA 장소 메모 서버
// 실행: server/KucaMemoServer 폴더에서 `dotnet run` → 브라우저에서 http://localhost:5080/swagger
var builder = WebApplication.CreateBuilder(args);

// API 문서 화면(Swagger)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 캠퍼스 건물 목록 (Data/buildings.json, 앱과 같은 632개)
builder.Services.AddSingleton<BuildingStore>();

// TODO(2단계): 메모 저장소를 여기에 등록하세요.

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// wwwroot 폴더의 파일(업로드된 사진 등)을 그대로 내려준다. 예: /photos/abc.jpg
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
   .WithTags("Health");

app.MapBuildingEndpoints();
app.MapMemoEndpoints();

app.Run();
