# KUCA — 작업 규칙

경희대 국제캠퍼스 산책 앱 (Unity 6 URP 앱 `My project/` · ASP.NET 서버 `server/` · 문서 `docs/`).

## 화면(UI)을 만들거나 고칠 때
- 먼저 `docs/디자인_v4.md` (참조 화면 · 토큰 · 화면 짜는 법) 와 `docs/인터페이스_기준.md` (연계 규칙 · 체크리스트) 를 읽는다.
- 새 화면 · 고친 화면은 `UIKit.CreatePage` 틀 + `PrimaryButton` · `CardBox` · `ListGroup` 등 v4 부품으로 짠다. 굵은 글자는 `UIKit.Bold`.
- 색 · 글자 크기 · 간격은 `My project/Assets/Scripts/KucaTheme.cs` 만 쓴다 (화면 코드에 `new Color(...)` · 숫자 글자 크기 금지).
- 버튼 · 칩 · 카드 · 시트 · 빈 상태 · 불러오기는 `UI/UIKit.cs` 의 공통 부품을 쓰고, 없으면 UIKit 에 먼저 추가한다.
- 바꾼 뒤 `KUCA › UI 화면 캡처` (`-executeMethod UiCapture.Run`) 로 찍어 확인하고, 새 화면이면 `Assets/Editor/UiCapture.cs` 의 `Script()` 에 한 줄 넣는다.
- 확인한 결과는 `docs/인터페이스_기준.md` 의 "사이클 기록"에 한 줄 남긴다.

## 파일 주인 (충돌 방지)
- A (Dopal-Kim): 경희몬 · 출현 · 액세서리 · 아트 · 배포 · 문서, `KucaTheme`, `UiCapture`
- B (mungim5556-ui): 앱 화면 · 흐름 (`GameHUD`, `UI/*`, `Route/*`, `Match/*`, `Memo/*`, `Auth/*`, `Character/*`)
- C (ocyu007-cloud): `server/`
- 다른 사람 파일을 고쳤으면 PR 설명에 적고 그 사람의 확인을 받는다.

## 확인
- 서버: `cd server && dotnet test KucaMemoServer.Tests`
- 앱: Unity 배치 모드 `-runTests -testPlatform EditMode` (Unity 가 있는 PC 에서)
