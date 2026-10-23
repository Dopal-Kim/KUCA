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

## 내용 검사 (AI 필터) 설정

쪽지·댓글을 저장하기 전에 AI 로 욕설·개인정보·광고를 검사합니다 (`docs/api.md` 의 "내용 검사"). AI 는 학교 **FactChat 게이트웨이**(OpenAI Chat Completions 형식)를 씁니다.

| 설정 | 기본값 | 설명 |
| --- | --- | --- |
| `Ai:ApiKey` | `Recommend:ApiKey` | FactChat API 키. **따로 넣지 않으면 아래 "산책 추천 AI" 의 `Recommend:ApiKey` 를 같이 씁니다** (키 하나로 둘 다). 둘 다 없으면 AI 대신 금지어·패턴 검사만 합니다. 저장소에 넣지 않습니다 |
| `Ai:FilterModel` | `claude-haiku-5-5` | 검사에 쓸 모델 |
| `Ai:Endpoint` | `Recommend:Endpoint` → FactChat 게이트웨이 | 다른 게이트웨이로 바꿀 때 |
| `Ai:TimeoutSeconds` | `15` | 이 안에 답이 없으면 금지어 검사로 대신 판단 (작성은 막히지 않음) |
| `Admin:Key` | 없음 | 관리자 API(`/api/admin/...`, 검토 대기 처리) 키. 없으면 관리자 API 가 꺼짐 |

**내 PC** (값은 `%APPDATA%\Microsoft\UserSecrets\kuca-memo-server\` 에 저장되고 깃에 안 올라감):

```bash
cd server/KucaMemoServer
# AI 키는 산책 추천과 같은 Recommend:ApiKey 하나면 충분 (아래 "산책 추천 AI" 참고)
dotnet user-secrets set "Admin:Key" "아무도 모를 긴 문자열"
```

**Azure** (콜론 대신 밑줄 두 개):

```bash
az webapp config appsettings set -g kuca-rg -n kuca-memo --settings Admin__Key="긴 문자열"
# AI 키는 Recommend__ApiKey 하나로 산책 추천과 내용 검사가 같이 씁니다
```

**검토 대기 처리** — 애매하다고 판단된 글은 `pending` 으로 저장되고 작성자에게만 보입니다. 관리자가 공개하거나 지웁니다 (예시는 `KucaMemoServer.http`).

```bash
curl -H "X-Admin-Key: <관리자 키>" https://kuca-memo.azurewebsites.net/api/admin/pending
curl -X POST -H "X-Admin-Key: <관리자 키>" https://kuca-memo.azurewebsites.net/api/admin/memos/<id>/approve
curl -X DELETE -H "X-Admin-Key: <관리자 키>" https://kuca-memo.azurewebsites.net/api/admin/memos/<id>
```

## 장소 공지 넣기 (C-4)

운영자가 학교 공지를 붙여 넣으면 AI 가 **관련 경희스팟(최대 3곳)**과 **짧은 요약**을 붙여 저장하고, 앱은 `GET /api/notices` 로 장소별 공지를 받아 갑니다. 관련 장소가 없는 공지는 저장하지 않습니다. 원문 링크(`url`)는 필수입니다.

```bash
curl -X POST https://kuca-memo.azurewebsites.net/api/admin/notices \
  -H "X-Admin-Key: <관리자 키>" -H "Content-Type: application/json" \
  -d '{"title":"중간고사 기간 열람실 연장","body":"공지 본문 전체","url":"https://www.khu.ac.kr/...","postedAt":"2026-10-08"}'
```

- AI 는 내용 검사와 같은 게이트웨이·키를 씁니다 (`Recommend:ApiKey`). 모델은 `Ai:NoticeModel` (기본 `claude-haiku-5-5`)
- AI 가 안 되면 제목·본문에 경희스팟 이름(또는 `spots.json` 의 짧은 이름)이 나오는지로 연결하고, 요약은 본문 앞부분입니다
- 경희스팟 목록은 `Data/spots.json` — **앱의 `KyungHeeSpots` 목록과 같게 유지**합니다
- 잘못 넣은 공지는 `DELETE /api/admin/notices/{id}` 로 지웁니다

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

## 날씨 (기상청 API 키)

`GET /api/weather` 는 기상청 단기예보 조회서비스를 씁니다. 공공데이터포털에서 받은 키를 **저장소가 아니라 설정에** 넣습니다 (Decoding·Encoding 키 둘 다 됨).

```bash
# 로컬
cd server/KucaMemoServer
dotnet user-secrets set "Kma:ServiceKey" "<공공데이터포털 키>"

# Azure: 배포하면서 넣기
KMA_KEY="<공공데이터포털 키>" ./deploy-azure.sh
# 또는 직접
az webapp config appsettings set --name kuca-memo --resource-group <리소스 그룹> --settings Kma__ServiceKey="<공공데이터포털 키>"
```

키가 없으면 `/api/weather` 가 503 을 돌려주고, 앱은 날씨 조건 없이 동작합니다.

## 산책 추천 AI (챗쿠) 키 넣기

`POST /api/recommendations` 는 챗쿠 AI 로 코스를 짭니다.
- **추천 v2 (C-V2):** 서버가 시간대·계절·날씨(기상청)를 보고 경희스팟마다 점수(취향 × 3 + 계절·시간 × 2 + 가까움 × 1)를 매깁니다.
  - AI 는 점수 상위 10곳 안에서만 고릅니다.
  - 이유가 소개글(`Data/spots.json` 의 `intro`)과 상관없으면 규칙 문구로 바꿉니다.
  - 숫자는 모두 `Services/RouteRecommender.cs` 의 `RecommendRules` 에 있습니다.
- 키가 없으면 같은 점수로 고르는 규칙 추천(`source: "fallback"`)으로 동작합니다.
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

## 실시간 산책 매칭 (C-V3)

`/api/match/...` · `/api/reports` · `/api/blocks` (명세는 `docs/api.md` 의 "매칭").

- 대기열 · 진행 중인 매칭 · 인상착의는 **메모리에만** 둡니다. 매칭 기록(30일) · 차단 · 신고(90일)만 SQLite 에 남깁니다.
  - 서버가 재시작되면 진행 중인 매칭은 사라지고, 앱은 `idle` 을 받습니다.
- 타이머가 없습니다. 앱이 3초마다 부르는 `GET /api/match` 때 만료 · 노쇼를 처리하고 짝을 찾습니다.
- 스몰토크 AI 는 산책 추천과 같은 `Recommend:*` 설정(챗쿠)을 씁니다. 키가 없으면 `Data/smalltalk.json` 예시에서 고릅니다.
- 숫자는 모두 `Services/MatchService.cs` 의 `MatchRules` 에 있습니다. 운영 시간은 없습니다 (24시간).
- **시연용 가상 상대:** 시연 기간에만 켭니다.

```bash
az webapp config appsettings set -g kuca-rg -n kuca-memo --settings Match__DemoPartner=true
# 시연이 끝나면 false 로
```

## 산책 완료 · 리뷰 · 보상 (C-V4)

`/api/walks` · `/api/walks/{id}/finish` · `/api/walks/{id}/review` · `/api/me/progress` (명세는 `docs/api.md`).

- 완료 판정 · 경험치 · 레벨은 **서버만** 계산합니다. 숫자는 `Services/WalkService.cs` 의 `WalkRules` 에 있습니다 (Q6 제안값).
- 매칭 산책은 둘 다 도착하면 서버가 만들고, 두 사람이 모두 끝내면 매칭이 `completed` 가 됩니다.
- 상대 별점은 보여 주지 않고, 평균이 낮은 사람을 짝 찾기에서 빼는 데만 씁니다.

## 테스트

```bash
cd server/KucaMemoServer.Tests
dotnet test
```

임시 폴더에 DB와 사진을 만들어 `docs/api.md` 의 규칙(최신순·limit·before, 글자 수, 사진 형식·크기, 기기 ID 삭제 권한, 좋아요 중복 방지·likedByMe, 댓글 순서·삭제 권한, 내용 검사(거절·검토 대기·관리자·AI 장애 대체), 장소 공지(장소 연결·버림·중복 링크·이름 찾기), 404/400/403)을 확인합니다.

## 폴더

| 경로 | 내용 |
| --- | --- |
| `Program.cs` | 서버 시작점. 서비스 등록과 API 연결 |
| `Endpoints/BuildingEndpoints.cs` | 건물 API (완성된 예시) |
| `Endpoints/MemoEndpoints.cs` | 메모 API (목록·작성·조회·삭제) |
| `Endpoints/ReactionEndpoints.cs` | 좋아요·댓글 API |
| `Endpoints/AdminEndpoints.cs` | 검토 대기 처리 (관리자 API) |
| `Endpoints/NoticeEndpoints.cs` | 장소 공지 (앱용 목록, 관리자용 넣기·지우기) |
| `Endpoints/WebPages.cs` | 브라우저용 페이지 `/`, `/buildings/{id}` |
| `Models/` | `Building`, `Memo`, `Comment` 데이터 모양 |
| `Services/BuildingStore.cs` | `Data/buildings.json` 을 읽는 건물 목록 |
| `Services/AiModerator.cs` | AI 내용 검사 (FactChat), 장애 시 `KeywordModerator`(금지어·패턴) 로 대체 |
| `Services/NoticeClassifier.cs` | AI 공지 판별 (경희스팟 연결·요약), 장애 시 이름 찾기로 대체 |
| `Services/NoticeStore.cs`, `Services/SpotStore.cs` | 공지 저장, 경희스팟 목록(`Data/spots.json`) |
| `Services/MemoStore.cs` | 메모·좋아요·댓글 저장 (SQLite `memos.db`, 표가 없으면 시작할 때 만든다) |
| `Services/PhotoStore.cs` | 사진 저장·삭제, JPEG/PNG 판별 |
| `Data/buildings.json` | 캠퍼스 건물 632개 (앱과 같은 ID) |
| `wwwroot/photos/` | 업로드된 사진 (깃에 올리지 않음) |
| `KucaMemoServer.http` | VS Code REST Client로 보내 보는 예시 요청 |
| `../KucaMemoServer.Tests/` | API 테스트 (xUnit) |
