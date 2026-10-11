# KUCA 장소 메모 서버

캠퍼스 건물마다 사진과 메모를 남기는 ASP.NET Core(.NET 10) 서버입니다. API 명세는 [`docs/api.md`](../docs/api.md)를 따릅니다.

## 실행

```bash
cd server/KucaMemoServer
dotnet run
```

브라우저에서 http://localhost:5080/swagger 를 열면 API를 직접 눌러 볼 수 있습니다.
http://localhost:5080/ 에서는 메모가 있는 건물과 메모를 웹 페이지로 볼 수 있습니다.

- 메모는 `memos.db`(SQLite 파일), 사진은 `wwwroot/photos/` 에 저장됩니다. 둘 다 깃에 올리지 않습니다.
- 폰에서 붙으려면 PC와 같은 와이파이에서 앱의 서버 주소를 `http://<PC의 IP>:5080` 으로 맞춥니다.

## Azure 배포 (모두가 같은 메모판을 보게)

Azure App Service(Linux, .NET 10, 일본 동부(도쿄) 리전 — Azure for Students 는 허용 리전이 정해져 있어 가장 가까운 곳)에 올립니다. Azure for Students 계정이면 카드 없이 크레딧으로 쓸 수 있습니다.

```bash
brew install azure-cli     # 처음 한 번
az login                   # 브라우저로 로그인
cd server
./deploy-azure.sh          # 처음이면 리소스를 만들고, 다음부터는 코드만 다시 올림
```

- 주소는 `https://kuca-memo.azurewebsites.net` (이름이 이미 쓰이고 있으면 `APP_NAME=다른이름 ./deploy-azure.sh`)
- 메모 DB 와 사진은 앱 폴더가 아닌 `/home/data` 에 둡니다 (`Memos__DatabasePath`, `Photos__Directory` 앱 설정). 재배포해도 지워지지 않습니다.
- 기본 요금제 F1(무료)은 한동안 안 쓰면 잠들어서 첫 요청이 느립니다. 항상 켜 두려면 `SKU=B1` 로 만듭니다(크레딧 차감).
- 앱에서는 설정 › 메모 서버에 위 주소를 넣습니다.

## 산책 추천 AI (챗쿠) 키 넣기

`POST /api/recommendations` 는 챗쿠 AI 로 코스를 짭니다.
- 키가 없으면 가까운 경희스팟부터 고르는 규칙 추천(`source: "fallback"`)으로 동작합니다.
- **키는 저장소(appsettings.json)에 넣지 않습니다.**

| 설정 | 뜻 |
| --- | --- |
| `Recommend:Endpoint` | 채팅 API 전체 주소. 기본값: 챗쿠(BAZE) 게이트웨이 `https://factchat-cloud.mindlogic.ai/v1/gateway/chat/completions` |
| `Recommend:ApiKey` | API 키 (BAZE › API Gateway 에서 발급). **이것만 넣으면 된다** |
| `Recommend:Model` | 모델 이름. 기본값 `gpt-5.4-mini` (게이트웨이 `/models/` 의 다른 ID 로 바꿀 수 있음) |
| `Recommend:AuthHeader` | 키를 보낼 헤더. 기본 `Authorization` |
| `Recommend:AuthScheme` | 헤더 값 앞말. 기본 `Bearer`, 키만 보내려면 빈 값 |

로컬 (`server/KucaMemoServer` 폴더에서):

```bash
dotnet user-secrets init
dotnet user-secrets set "Recommend:ApiKey" "발급받은 키"
```

Azure:

```bash
az webapp config appsettings set -g kuca-rg -n kuca-memo --settings Recommend__ApiKey="발급받은 키"
```

- 챗쿠(BAZE) 게이트웨이는 OpenAI 호환입니다. 요청은 `{ model, messages }`, 답은 `choices[0].message.content` 입니다.
- 키는 `Authorization: Bearer` 로 보냅니다.
- 실측: `gpt-5.4-mini` 는 약 3초, `gemini-3.5-flash` 는 약 11초 걸렸습니다.
- 사용량은 키를 만든 BAZE 계정에 청구됩니다.

## 테스트

```bash
cd server/KucaMemoServer.Tests
dotnet test
```

임시 폴더에 DB와 사진을 만들어 `docs/api.md` 의 규칙(최신순·limit·before, 글자 수, 사진 형식·크기, 기기 ID 삭제 권한, 좋아요 중복 방지·likedByMe, 댓글 순서·삭제 권한, 404/400/403)을 확인합니다.

## 폴더

| 경로 | 내용 |
| --- | --- |
| `Program.cs` | 서버 시작점. 서비스 등록과 API 연결 |
| `Endpoints/BuildingEndpoints.cs` | 건물 API (완성된 예시) |
| `Endpoints/MemoEndpoints.cs` | 메모 API (목록·작성·조회·삭제) |
| `Endpoints/ReactionEndpoints.cs` | 좋아요·댓글 API |
| `Endpoints/WebPages.cs` | 브라우저용 페이지 `/`, `/buildings/{id}` |
| `Models/` | `Building`, `Memo`, `Comment` 데이터 모양 |
| `Services/BuildingStore.cs` | `Data/buildings.json` 을 읽는 건물 목록 |
| `Services/MemoStore.cs` | 메모·좋아요·댓글 저장 (SQLite `memos.db`, 표가 없으면 시작할 때 만든다) |
| `Services/PhotoStore.cs` | 사진 저장·삭제, JPEG/PNG 판별 |
| `Data/buildings.json` | 캠퍼스 건물 632개 (앱과 같은 ID) |
| `wwwroot/photos/` | 업로드된 사진 (깃에 올리지 않음) |
| `KucaMemoServer.http` | VS Code REST Client로 보내 보는 예시 요청 |
| `../KucaMemoServer.Tests/` | API 테스트 (xUnit) |
