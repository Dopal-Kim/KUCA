# 산책 완료 · 리뷰 · 보상 API — B 초안

> **초안**: B(mungim5556-ui)가 B-V4 화면을 먼저 만들려고 쓴 것입니다. 정식 명세는 C 가 `docs/api.md` 에 씁니다 (C-V0 · C-V4).
> C 명세가 나오면 앱을 그 모양으로 맞추고 이 파일은 지웁니다.
> 배경: [`산책추천_v2_라운드.md`](산책추천_v2_라운드.md) 2-4.
>
> 서버에 이 API 가 없으면 (`404`) 앱이 아래 "기기 계산" 으로 같은 값을 만들어 보여 줍니다 (`LocalWalkService`).

모든 요청에 `Authorization: Bearer {로그인 토큰}`.

## POST /api/walks/complete

산책이 끝났을 때 한 번. 서버가 **완료 판정**과 **보상**을 정하고, 앱은 받은 값만 보여 주고 더합니다.

```json
{
  "kind": "course",
  "matchId": "",
  "meters": 1620,
  "minutes": 27,
  "kcal": 86,
  "plannedMeters": 1850,
  "stopsDone": 3,
  "stopsTotal": 3
}
```

| 칸 | 설명 |
| --- | --- |
| `kind` | `course` (산책 추천 코스) · `mate` (산책메이트와 함께) |
| `matchId` | `mate` 일 때 매칭 ID |
| `meters` · `minutes` · `kcal` | 이번 산책에서 실제로 걸은 거리 · 시간 · 칼로리 (앱이 GPS 로 잼) |
| `plannedMeters` | 코스의 전체 거리 (`course`) |
| `stopsDone` · `stopsTotal` | 도착한 장소 수 / 코스의 장소 수 (`course`) |

`200`

```json
{
  "walkId": "w_8a1…",
  "completed": true,
  "reason": "마지막 장소에 도착했어요",
  "xp": 30,
  "tickets": 1,
  "growth": 10,
  "level": 3,
  "xpInLevel": 40,
  "xpNeeded": 200
}
```

| 칸 | 설명 |
| --- | --- |
| `completed` | 완료 판정. 코스는 **마지막 장소 도착 또는 거리 80% 이상**, 산책메이트는 함께 걸은 거리 · 시간 기준 (서버가 정함) |
| `reason` | 판정 이유 한 줄 (화면에 그대로) |
| `xp` · `tickets` · `growth` | 이번 산책 보상: 경험치 · 티켓 · 파트너 진화 경험치 |
| `level` · `xpInLevel` · `xpNeeded` | 보상을 더한 뒤의 레벨 (서버 값으로 표시) |

## POST /api/walks/{walkId}/review

```json
{ "partnerStars": 5, "courseStars": 4, "comment": "이야기 잘 통했어요" }
```

- `partnerStars`: 산책메이트 별점 1~5 (`course` 면 0). **상대에게 보이지 않고 매칭 품질에만** 씀 (낮은 별점 제외).
- `courseStars`: 코스 별점 1~5. 추천 품질 (AI vs 규칙) 지표에 씀.
- `comment`: 한 줄 60자 이하, `AiModerator` 검사 (`422` 면 다시 쓰기).
- `200 { "xp": 10 }` — 리뷰 보상. 같은 산책에 두 번 보내면 `409`.

## 기기 계산 (서버가 없을 때, v2 경험치 표 제안값)

| 무엇 | 보상 |
| --- | --- |
| 걷기 1km | 경험치 10 (걸을 때마다 앱이 바로 더함, 여기서는 다시 주지 않음) |
| 코스 완료 | 경험치 30 · 티켓 1 · 파트너 진화 경험치 10 |
| 산책메이트와 완료 | 경험치 60 · 티켓 1 · 파트너 진화 경험치 10 |
| 리뷰 | 경험치 10 |

- 산책메이트 완료 (기기 판정): 함께 **500m 이상 또는 10분 이상** 걸음.
