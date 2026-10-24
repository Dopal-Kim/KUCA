# iOS TestFlight 제출 (공모전 심사위원용 공개 링크)

심사위원이 아이폰에 설치하는 길: **TestFlight 공개 링크** (유료 Apple Developer 계정 필요, B 가 10/24 가입).
기기 등록 없이 링크만 누르면 TestFlight 앱으로 설치된다. 빌드는 업로드하고 90일 동안 유효하다.

## 준비 상태

| 항목 | 상태 |
| --- | --- |
| Apple Developer 유료 멤버십 | 결제 완료 (10/24). 활성화 메일("Welcome to the Apple Developer Program") 기다리는 중 (최대 48시간) |
| 앱 버전 | `1.0.0`, iOS 빌드 번호 `1` (다시 올릴 때마다 1 씩 올림) |
| 번들 ID | `com.mungim.kuca` |
| 수출 규정 (암호화) | `ITSAppUsesNonExemptEncryption = false` 를 빌드 때 넣음 (`Assets/Editor/IosBuildSettings.cs`) |
| 카메라 · 위치 권한 문구 | 있음 (Player Settings) |
| 앱 아이콘 | 1024×1024, 투명 없음 (`Assets/Art/AppIcon/AppIcon.png`) |
| 개인정보 처리방침 URL | `https://github.com/mungim5556-ui/KUCA/blob/main/docs/개인정보처리방침.md` (문의 메일 칸 채울 것) |
| 서명 팀 | 지금은 무료 개인 팀 `HAYUF8Y8PV` → 멤버십이 켜지면 유료 팀 ID 로 바꿔야 함 |

## 순서

1. 활성화 메일이 오면 Xcode › Settings › Apple Accounts 에서 Sign Out → Sign In. 팀 목록에 유료 팀(Personal Team 이 아닌 이름)이 보이는지 확인
2. 유료 팀 ID 로 서명 설정 바꾸기 (Player Settings `appleDeveloperTeamID`)
3. [App Store Connect](https://appstoreconnect.apple.com) › 앱 › **+ › 신규 앱**
   - 플랫폼 iOS · 이름 (스토어 전체에서 겹치면 안 됨, 예: "KUCA 경희 산책") · 기본 언어 한국어 · 번들 ID `com.mungim.kuca` · SKU `kuca-2026`
4. Unity iOS 빌드 → Xcode Archive (Release) → App Store Connect 업로드. 처리 10~30분
5. TestFlight › **테스트 정보** 입력 (아래 문구 붙여 넣기)
6. TestFlight › 외부 테스팅 › **그룹 추가** ("심사위원") › 빌드 추가 › **베타 앱 검토 제출** (보통 하루 안)
7. 통과하면 그룹에서 **공개 링크 활성화** → 링크를 공모전 제출서 · 설명서에 적기

심사 전에 같이 할 것 (`docs/v2_연동_결정_알림.md`): 시연용 가상 상대 켜기(`Match__DemoPartner=true`), Azure F1 → B1.

## TestFlight 입력 문구 (붙여 넣기용)

**베타 앱 설명**

> KUCA 는 경희대학교 국제캠퍼스를 걸으며 경희몬을 모으고, AI 가 오늘 날씨 · 시간 · 취향에 맞는 산책 코스를 추천하고, 같은 시간에 걷고 싶은 학생과 산책메이트로 이어 주는 캠퍼스 산책 앱입니다.

**테스트할 내용 (What to Test)**

> 1. 첫 화면에서 "로그인 없이 둘러보기"로 시작해 주세요 (체험 계정, 7일 뒤 자동 삭제).
> 2. 캠퍼스 밖이라면 설정 › 가상 산책을 켜면 조이스틱으로 캠퍼스를 걸을 수 있어요.
> 3. 산책 추천 버튼으로 AI 코스를 받아 걸어 보고, 산책을 끝내면 완료 · 보상 · 리뷰 화면이 나와요.
> 4. 산책메이트 버튼으로 매칭을 시작하면 시연용 가상 상대 "체험메이트"가 나타나 함께 걷기까지 체험할 수 있어요.
> 5. 걸으면 티켓이 쌓이고, 뽑기로 경희몬을 모아 도감과 진화를 볼 수 있어요.

**베타 앱 검토 정보 › 메모 (심사 담당자에게)**

> 로그인 없이 체험할 수 있습니다: 첫 화면의 "로그인 없이 둘러보기"를 누르면 체험 계정이 만들어지고 모든 기능을 쓸 수 있습니다. 위치 권한을 허용하면 지도에 위치가 표시되며, 캠퍼스(경기도 용인시 경희대학교 국제캠퍼스) 밖에서는 설정 › 가상 산책으로 걸어 볼 수 있습니다. 경희대 메일 로그인은 재학생용이라 체험 계정으로 확인해 주세요.

- 로그인 필요 여부: 필요 없음 (체험 계정으로 모든 기능 사용 가능)
- 연락처: 이름 · 전화 · 이메일 (팀 대표)
- 피드백 이메일: (팀 대표 메일)
- 개인정보 처리방침 URL: 위 "준비 상태" 표의 주소
