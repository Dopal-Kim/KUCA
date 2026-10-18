# 실시간 산책 매칭 API — B 초안

> **초안**: B(mungim5556-ui)가 B-V3 매칭 화면을 먼저 만들려고 쓴 것입니다. 정식 명세는 C 가 `docs/api.md` 에 씁니다 (C-V0 · C-V3).
> C 명세가 나오면 앱을 그 모양으로 맞추고 이 파일은 지웁니다. 이름 · 모양은 C 가 바꿔도 됩니다.
> 배경: [`산책추천_v2_라운드.md`](산책추천_v2_라운드.md) 2-3.
>
> 앱은 이 API 가 서버에 없으면 (`404`) **기기 안 가상 상대**로 같은 흐름을 보여 줍니다 (`MockMatchService`).

모든 요청에 `Authorization: Bearer {로그인 토큰}` (체험 계정 포함). 응답은 모두 아래 **MatchState** 하나로 통일합니다.

## 흐름

```
조건 → (대기열) waiting → proposed (상대 제안) → accepted (상대 수락 기다림) → matched (만남 스팟 · 인상착의 · 스몰토크)
     → walking (산책 시작) → ended
어느 단계든 → cancelled / expired (message 에 이유)
```

## MatchState

```json
{
  "status": "matched",
  "matchId": "m_3f0c…",
  "waitSecondsLeft": 0,
  "decideSecondsLeft": 0,
  "partner": { "nickname": "산책하는 다람쥐", "commonTastes": ["photo", "quiet"], "talkStyle": "some" },
  "partnerAccepted": true,
  "spot": { "buildingId": "way-474085534", "name": "중앙도서관 정문", "lat": 37.2421, "lon": 127.0796, "species": "library_owl" },
  "smalltalk": ["요즘 공강에 뭐 해요?", "학식 최애 메뉴는?", "캠퍼스에서 제일 좋아하는 곳은?"],
  "myLook": "검은 후드, 흰 운동화",
  "partnerLook": "회색 맨투맨, 남색 백팩",
  "noShowSecondsLeft": 540,
  "minutes": 30,
  "message": ""
}
```

| 칸 | 설명 |
| --- | --- |
| `status` | `idle` · `waiting` · `proposed` · `accepted` · `matched` · `walking` · `ended` · `cancelled` · `expired` |
| `waitSecondsLeft` | `waiting`: 대기열에서 더 기다릴 시간 (0 이 되면 `expired`) |
| `decideSecondsLeft` | `proposed`: 수락/거절할 시간 (지나면 거절로 봄) |
| `partner` | 상대의 **닉네임 · 공통 취향 · 대화 분위기만**. 성별 · 메일 · 위치는 주지 않음 |
| `spot` | 두 사람 사이의 만남 스팟 (A 의 목록) 과 거기 서 있는 경희몬 `species` |
| `smalltalk` | 스몰토크 주제 3개 (공통 취향 + 스팟 + 날씨, AI 실패 시 A 의 예시) |
| `myLook` / `partnerLook` | 인상착의. **상대에게만** 보이고 산책이 끝나면 지움 |
| `noShowSecondsLeft` | `matched` 에서 산책 시작까지 남은 시간. 0 이 되면 `cancelled` (노쇼, 10분) |
| `message` | `cancelled` · `expired` 이유 (화면에 그대로 보여 줌) |

## API

| 요청 | 본문 | 설명 |
| --- | --- | --- |
| `POST /api/match/queue` | `{ "minutes": 30, "sameGender": false, "buildingId": "way-…", "tastes": ["photo"] }` | 대기열에 들어감 → `waiting`. 위치는 **건물 ID 만** (GPS 좌표는 보내지 않음) |
| `DELETE /api/match/queue` | — | 대기 취소 → `idle` |
| `GET /api/match` | — | 지금 상태. 앱은 `waiting` · `accepted` 에서 2초마다, 그 밖에는 5초마다 부름 |
| `POST /api/match/{id}/accept` · `/decline` | — | 제안 수락 / 거절. 둘 다 수락하면 `matched` |
| `PUT /api/match/{id}/look` | `{ "look": "40자 이하" }` | 내 인상착의. `AiModerator` 검사 (`422` 이면 다시 쓰기) |
| `POST /api/match/{id}/topic` | — | 다음 스몰토크 주제 하나 → `{ "topic": "…" }` |
| `POST /api/match/{id}/start` | — | "만났어요" → 두 사람 모두 누르면 `walking` |
| `POST /api/match/{id}/end` | — | 산책 끝내기 → `ended` (완료 판정 · 리뷰 · 보상은 C-V4) |
| `POST /api/match/{id}/cancel` | `{ "reason": "…" }` | 매칭 취소 → `cancelled` (상대에게는 "상대가 취소했어요") |
| `POST /api/match/{id}/report` | `{ "reason": "rude" \| "noshow" \| "personal" \| "other", "detail": "", "block": true }` | 신고 (+ 차단). 차단한 사람과는 다시 매칭하지 않음. 매칭은 취소됨 |

- `409` 이미 다른 매칭 중, `401` 로그인 필요, `403` 운영 시간 아님 (`message` 에 운영 시간).
- `GET /api/match` 는 매칭이 없어도 `200` + `status: "idle"` 입니다 (`404` 는 "API 가 아직 없음" 으로 보고 앱이 가상 상대로 바꿉니다).
- 짝 찾기 · 성별 조건 · 차단 · 낮은 별점 제외 · 운영 시간 · 노쇼 판정은 모두 서버 (C-V3).

## 개인정보 보관

| 항목 | 보관 |
| --- | --- |
| 대기열의 건물 ID | 매칭되거나 대기를 그만두면 삭제 |
| 인상착의 | 산책이 끝나거나 취소되면 삭제 |
| 신고 · 차단 | 운영 검토용으로 보관 (기간은 V0 결정 Q8) |
