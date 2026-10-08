using KucaMemoServer.Endpoints;
using KucaMemoServer.Services;
using Microsoft.AspNetCore.Http.Features;

// KUCA 장소 메모 서버
// 실행: server/KucaMemoServer 폴더에서 `dotnet run` → 브라우저에서 http://localhost:5080/swagger
var builder = WebApplication.CreateBuilder(args);

// API 문서 화면(Swagger)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 캠퍼스 건물 목록 (Data/buildings.json, 앱과 같은 632개)
builder.Services.AddSingleton<BuildingStore>();

// 메모 저장소 (SQLite 파일 memos.db) 와 사진 저장소 (wwwroot/photos)
builder.Services.AddSingleton<MemoStore>();
builder.Services.AddSingleton<PhotoStore>();

// 메모 작성 본문 크기 제한: 사진 10MB + 글자 여유분
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = PhotoStore.MaxBytes + 1024 * 1024);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// wwwroot 폴더의 파일(업로드된 사진 등)을 그대로 내려준다. 예: /photos/abc.jpg
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
   .WithTags("Health");

app.MapBuildingEndpoints();
app.MapMemoEndpoints();
app.MapWebPages();

app.Run();

// 테스트(WebApplicationFactory)에서 이 서버를 띄울 수 있게 공개한다.
public partial class Program;
