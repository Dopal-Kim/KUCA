# KUCA (쿠카)

**공강에 AI 가 걷기 좋은 캠퍼스 코스와 함께 걸을 학생을 이어 주는, 경희대학교 국제캠퍼스 산책 앱**

2026 KHU AI Challenge · AI 문제해결 솔루션 부문 · 팀 쿠카

| | |
| --- | --- |
| 아이폰 설치 | [TestFlight 공개 링크](https://testflight.apple.com/join/C6A1VX4G) (App Store 에서 TestFlight 앱을 먼저 받으세요) |
| 운영 서버 | https://kuca-memo.azurewebsites.net (API 문서 [`/swagger`](https://kuca-memo.azurewebsites.net/swagger)) |
| 체험 안내 | [심사위원 체험 절차](docs/guides/심사위원_체험_절차.md) |
| 개인정보 처리방침 | [docs/개인정보처리방침.md](docs/개인정보처리방침.md) |

---

## 무엇을 하는 앱인가요

국제캠퍼스 학생들은 공강을 건물 사이를 '지나가는 시간'으로 보내고, 학과 밖 학생을 만날 가볍고 안전한 계기가 드뭅니다.
KUCA 는 공강 15~60분을 **캠퍼스를 걷고 사람을 만나는 시간**으로 바꿉니다.

1. **AI 맥락 산책 추천:** "밥 먹고 30분, 오르막은 빼고 도서관은 들르고 싶어"처럼 말로 적으면 AI 가 산책 조건으로 해석합니다.
   - 지금 시각 · 계절 · 기상청 날씨, 경희스팟 25곳의 소개, 학생 쪽지, 학교 공지(통제 · 행사)를 함께 보고 코스 3개와 장소별 이유를 냅니다.
   - 코스마다 거리 · 시간 · 대략 칼로리 · 난이도(하 · 중 · 상)를 보여 주고, 실제 보행로 지도 위에 길을 그립니다.
2. **AI 산책메이트:** 같은 시간에 걷고 싶은 학생을 성별 조건 · 차단 · 신고 같은 안전 규칙으로 먼저 거르고, AI 가 짝과 연결 이유, 첫 대화 주제를 고릅니다.
   - 만남은 공개된 만남 스팟 10곳에서만 합니다.
   - 두 사람이 함께 걸을 코스와 도착지를 정하고, 걷는 동안 지도에 경로가 나옵니다.
3. **계속 걷게 하는 장치:** 1km 마다 티켓을 받아 캠퍼스 랜드마크 동물 **경희펫 24종**을 만나고, 함께 걸은 거리만큼 진화시킵니다. 주간 150분 걷기 목표와 AI 코치 한 줄도 있습니다.
4. **안전한 공유 공간:** 장소마다 쪽지 · 사진을 남기면 AI 가 욕설 · 개인정보 · 광고를 검사합니다. 학교 게시판 공지 12곳을 AI 가 관련 장소에 붙여 요약합니다.

캠퍼스 건물 632개를 3D 로 옮긴 지도 위에서 동작하고, 계절 · 날씨에 따라 지도의 빛과 비 · 눈 · 꽃잎 효과가 바뀝니다.

> 캠퍼스 밖에서도 **로그인 없이 둘러보기 → 가상 산책(조이스틱) → 체험메이트(시연용 상대)** 로 전체 흐름을 체험할 수 있습니다.

---

## 저장소 구성

```
KUCA/
├── My project/          Unity 6 (URP) 앱 — iOS · Android
│   └── Assets/Scripts/
│       ├── Route/       산책 추천 · 코스 안내 · 걸은 기록
│       ├── Match/       산책메이트 매칭 · 함께 걷기
│       ├── Memo/        경희스팟 쪽지 · 사진 · 공지
│       ├── Auth/        로그인 (Apple · 카카오 · 둘러보기) · 경희대 메일 인증
│       ├── Character/   내 캐릭터 꾸미기
│       ├── Profile/     산책 취향 · 건강 정보
│       ├── UI/          공통 UI 부품 (UIKit) · 경희펫 메뉴
│       └── *.cs         지도 · 경희펫 · 진행도 · 날씨 효과 등
├── server/              ASP.NET Core (.NET 10) 서버 — Azure App Service
│   ├── KucaMemoServer/        API · AI 연동 · SQLite
│   └── KucaMemoServer.Tests/  서버 테스트
├── tools/               보행로 그래프 · 아트 생성 · 공모전 서식 스크립트
├── Art/                 컨셉 아트 · 앱 아이콘
└── docs/                기획 · 디자인 · API 문서
```

## 기술 구성

| 부분 | 사용 기술 |
| --- | --- |
| 앱 | Unity 6 (URP), C#, uGUI (코드로 짜는 공통 부품 `UIKit`) |
| 서버 | ASP.NET Core (.NET 10) Minimal API, SQLite, Azure App Service |
| AI | 학교 ChatKHU(FactChat) 게이트웨이 — 산책 조건 해석 · 코스 이유 · 매칭 이유 · 대화 주제 · 공지 요약 · 내용 검사. AI 가 실패하면 규칙 기반으로 대신 동작 |
| 외부 데이터 | 기상청 날씨, 학교 게시판 공지, 캠퍼스 보행로 · 경사 데이터 |
| 배포 | TestFlight (iOS), Firebase App Distribution (Android, GitHub Actions) |

---

## 직접 실행해 보기

### 서버

```bash
cd server/KucaMemoServer
dotnet run
```

http://localhost:5080/swagger 에서 API 를 눌러 볼 수 있습니다. AI 키 · 관리자 키 같은 설정은 [server/README.md](server/README.md) 를 보세요. 키는 저장소에 넣지 않고 user-secrets 나 Azure 앱 설정에만 둡니다.

```bash
cd server
dotnet test KucaMemoServer.Tests
```

### 앱

1. Unity Hub 에서 `My project` 폴더를 Unity **6000.6.4f1** 로 엽니다.
2. 에디터에서 Play 하면 캠퍼스 밖에서도 가상 산책으로 걸어 볼 수 있습니다.
3. iOS: File › Build Profiles › iOS 로 Xcode 프로젝트를 만든 뒤 Xcode 에서 서명하고 설치합니다.
4. 테스트: Window › General › Test Runner › EditMode.

앱은 기본으로 운영 서버(https://kuca-memo.azurewebsites.net)에 붙고, 서버에 닿지 않으면 기기 안의 대체 데이터로 동작합니다.

---

## 문서

| 문서 | 내용 |
| --- | --- |
| [docs/api.md](docs/api.md) | 서버 API 명세 (추천 · 매칭 · 쪽지 · 로그인 · 관리자) |
| [docs/기획서_v3.md](docs/기획서_v3.md) | 기획서 |
| [docs/디자인_v4.md](docs/디자인_v4.md) | 화면 디자인 기준 |
| [docs/인터페이스_기준.md](docs/인터페이스_기준.md) | 화면 연계 규칙 · 확인 기록 |
| [docs/시연_시나리오.md](docs/시연_시나리오.md) | 시연 영상 대본 |
| [docs/guides/심사위원_체험_절차.md](docs/guides/심사위원_체험_절차.md) | 설치 · 체험 순서 · 막혔을 때 |

## 팀

| 역할 | 담당 |
| --- | --- |
| 경희펫 · 아트 · 배포 · 문서 | [Dopal-Kim](https://github.com/Dopal-Kim) |
| 앱 화면 · 흐름 | [mungim5556-ui](https://github.com/mungim5556-ui) |
| 서버 · AI | [ocyu007-cloud](https://github.com/ocyu007-cloud) |
