# Unity Hub 진단 (읽기만 — 설치 · 삭제 · 설정 변경 없음)

조사 시각 2026-10-27 23:09 (KST). 이메일 · 토큰은 로그 발췌에서 지움. 크래시 덤프(.dmp, 36 MB)는 메모리 내용이 들어 있을 수 있어 **올리지 않음** — 경로만 적음.

## 요약
- **Hub 가 로그 없이 갑자기 죽는 것(네이티브 크래시)** 이 맞음. 마지막 크래시: **23:07:22** — `%APPDATA%\UnityHub\Crashpad\reports\7c36470e-….dmp` 생성. info-log 는 23:06:11 의 평범한 줄로 끝나고 종료 · 오류 줄이 없음(정상 종료면 `ShutdownCoordinator: App closing` 줄이 남는데 없음).
- 직전 동작(sentry breadcrumbs): 23:01:58 `/projects` → **`add-project-button` 클릭** → 메뉴 항목 클릭(= "디스크에서 추가") → 이후 몇 번 클릭 → 23:06:21 마지막 기록 → 23:07:22 덤프. 즉 **"디스크에서 추가" 를 눌러 Windows 폴더 선택 창이 떠 있는 동안 / 고른 직후** 에 꺼짐.
- 오늘 Hub 프로세스 5개(pid 20804 · 1472 · 17344 · 2144 · 1276)가 모두 종료 기록 없이 끝남 — 같은 증상 반복 가능성.
- 에디터 · 프로젝트 버전은 맞음 → **버전 불일치 문제 아님**.
- ⚠ 같은 날 **21:16 에 블루스크린(BugCheck 0x50 · 0xC2 · 0x18) + LiveKernelEvent 0x141(그래픽 드라이버 응답 없음/TDR) 여러 건** 기록. Hub 는 Electron(크로미움) 앱이라 GPU 가속을 씀 → **그래픽 드라이버 불안정이 Hub 크래시 원인일 가능성 높음**(추정). NVIDIA 드라이버가 2022-06 판으로 오래됨.

## 1. Hub 로그 (`%APPDATA%\UnityHub\logs\info-log.json`, 마지막 200줄)
- error · crash · ENOENT · EPERM 없음. 눈에 띄는 것:
  - `warn customInstallLocation: Custom install path could not be retrieved: Error: Custom install path is not a string` (시작 때마다 · 무해할 가능성 높음 — 에디터 설치 위치 설정이 비어 있음)
  - `error AssistantService: Some services urls are not valid undefined` (AI Assistant 관련, 무관)
  - `IdentityProvider: First time user check: { hasEditors: true, hasProjects: false, … firstTimeUser: true }` — **Hub 프로젝트 목록이 비어 있음**(한 번도 추가 성공 못 함)
  - `LocalProject: Found 0 open project(s) in cache`
- 23:01:56 `cli-log`: `Auth broker accepted a shutdown request … self-install replace` — **Hub 가 23:01 에 스스로 업데이트 후 재시작**함(3.22.4 로). 업데이트 직후부터 꺼짐이 시작됐는지는 이 로그로는 구분 불가.

## 2. 설치된 에디터 (`C:\Program Files\Unity\Hub\Editor\`)
- `6000.6.4f1` 하나 — 프로젝트 요구 버전과 **일치** ✅

## 3. Hub 버전
- `C:\Program Files\Unity Hub\Unity Hub.exe` ProductVersion **3.22.4**

## 4 · 5. 프로젝트 경로 (Test-Path 만, 사용자 폴더는 열지 않음)
| 경로 | ProjectVersion.txt | 비고 |
|---|---|---|
| `C:\Users\admin\KUCA\My project` | 있음 (True) | 경로에 **공백 1개**("My project"), 한글 · 특수문자 없음 |
| `C:\Users\admin\KUCA-check\My project` | 있음 (True) — `m_EditorVersion: 6000.6.4f1` | 같음 |
- 공백은 Unity 가 지원하는 경로라 원인일 가능성 낮음(이 PC 에서 KUCA-check 는 같은 경로로 에디터가 문제없이 열림 — 캡처를 수십 번 돌림).

## 6. Windows 이벤트 로그 (응용 프로그램, 최근 12시간)
- 메시지에 "Unity" 가 들어간 응용 프로그램 이벤트: **0건** (Electron 크래시는 Crashpad 로만 남고 WER 에 안 남는 경우가 많음).
- Windows Error Reporting(1001): 21:16 에 **BlueScreen** (P1 0x50, 0xC2, 0x18) 와 **LiveKernelEvent 141** 여러 건 — GPU 드라이버 타임아웃/복구 계열.
- WER 보관함의 Unity 관련 기록: 10-25 17:47 `Unity.ILPP.Trigger` AppCrash 4건(에디터 컴파일 보조 프로세스, 이번 Hub 문제와는 무관해 보임).
- GPU: Intel UHD Graphics (드라이버 30.0.101.1069, 2021-11) + **NVIDIA GeForce RTX 3070 Laptop (31.0.15.1661, 2022-06)**.

## 우회 · 다음 단계 제안 (사용자 판단 — 이 세션은 아무것도 바꾸지 않음)
1. **Hub 를 거치지 않고 에디터로 바로 열기**: `"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" -projectPath "C:\Users\admin\KUCA\My project"` — 이 PC 에서 KUCA-check 를 이 방식으로 계속 열어 왔고 문제 없었음. 빌드는 에디터 메뉴(File › Build Profiles 또는 KUCA › 안드로이드 APK 빌드)로 가능.
2. Hub 를 GPU 가속 없이 띄워 보기: `"C:\Program Files\Unity Hub\Unity Hub.exe" --disable-gpu` 로 실행 후 "디스크에서 추가" 재시도 — 꺼지지 않으면 GPU 쪽 원인 확정.
3. 폴더 선택 창 자체 문제일 수도 있음(셸 확장 · 클라우드 드라이브 아이콘 등): 선택 창에서 바로 `C:\Users\admin\KUCA\My project` 를 주소 칸에 붙여 넣고 "폴더 선택" — 탐색을 줄이면 덜 꺼질 수 있음.
4. 블루스크린 · 0x141 이 반복되면 **NVIDIA 드라이버 업데이트**(2022-06 → 최신 Studio/Game Ready) 권장 — 에디터 렌더링 안정성에도 영향.
5. 원인 확인이 필요하면 덤프(`…\Crashpad\reports\7c36470e-d0ac-436d-a01e-ba83493550ef.dmp`)를 WinDbg 로 열어 실패 모듈 확인 가능 — 원하면 다음에 모듈 이름만 뽑아 드리겠습니다.
