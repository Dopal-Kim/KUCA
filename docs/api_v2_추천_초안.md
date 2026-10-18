# 산책 추천 v2 API — B 초안

> **초안**: B(mungim5556-ui)가 B-V2 화면을 먼저 만들려고 쓴 것입니다. 정식 명세는 C 가 `docs/api.md` 에 씁니다 (C-V0).
> C 의 명세가 나오면 앱을 그 모양으로 맞추고 이 파일은 지웁니다. 다르게 하고 싶은 곳은 C 가 마음대로 바꿔도 됩니다.
> 배경: [`산책추천_v2_라운드.md`](산책추천_v2_라운드.md) 2-2.

## 원칙

- **주소는 그대로** `POST /api/recommendations`. v1 칸은 모두 그대로 두고 **칸만 더합니다.**
  지금 서버(v1)는 새 칸을 무시하고, 앱은 응답에 새 칸이 없으면 기기에서 계산해 채웁니다. 그래서 순서와 상관없이 붙일 수 있습니다.
- 날씨 · 계절은 서버가 정합니다 (기상청, 서버 시간). 앱은 지금 시각만 보냅니다.

## 요청 (v1 + 새 칸)

```json
{
  "minutes": 30,
  "tastes": ["photo", "quiet"],
  "lat": 37.2440, "lon": 127.0800,
  "visitedBuildingIds": ["way-474085534"],
  "candidateBuildingIds": ["way-473963422", "way-474085534"],

  "pace": "normal",
  "afterMeal": true,
  "weightKg": 0,
  "localTime": "2026-10-18T12:40:00+09:00"
}
```

| 새 칸 | 설명 |
| --- | --- |
| `pace` | 걷는 속도 `slow` · `normal` · `fast` (취향 체크리스트 3번). 걸리는 시간 · 칼로리 계산에 씀. 없으면 `normal` |
| `afterMeal` | 식후 걷기 모드. `true` 면 소화에 좋은 평지 · 가까운 곳 위주, 이유에 식후 걷기 문구 |
| `weightKg` | 칼로리 계산용 체중 (선택, V0 결정 Q5). `0` 이면 60kg 로 계산 |
| `localTime` | 기기의 지금 시각 (ISO 8601). 시간대 (아침 · 낮 · 저녁 · 밤) 판단용 |

`minutes` 는 v2 에서도 `15` · `30` · `60` (결정 D7).

## 응답 `200` (v1 + 새 칸)

```json
{
  "title": "점심 먹고 가볍게 30분",
  "source": "ai",
  "totalMeters": 1850,
  "totalMinutes": 28,
  "kcal": 98,
  "weather": "맑음 18°C",
  "stops": [
    {
      "buildingId": "way-474085534", "name": "중앙도서관",
      "reason": "평지라 식후에 걷기 좋고, 창가에서 햇볕을 쬘 수 있어요",
      "walkMinutes": 6, "lat": 37.2421, "lon": 127.0796,
      "distanceMeters": 420
    }
  ]
}
```

| 새 칸 | 설명 |
| --- | --- |
| `totalMeters` | 지금 위치 → 모든 장소를 차례로 들르는 거리 (m) |
| `totalMinutes` | 그 거리를 `pace` 로 걸리는 시간 (분) |
| `kcal` | 아래 식으로 계산한 소모 칼로리 (정수) |
| `weather` | 이유에 쓴 날씨 한 줄 (없으면 생략) |
| `stops[].distanceMeters` | 앞 장소 (첫 곳은 지금 위치) 에서 이 장소까지 (m) |

- `reason` 은 소개글 · 태그 · 날씨에 있는 내용만 (C-V2 응답 검증).
- 실패 · AI 장애는 v1 과 같음 (`400` 필드 오류, AI 실패는 `200` + `source: "fallback"`).

## 칼로리 · 시간 계산 (앱 · 서버 같은 식)

- 걷는 속도: `slow` 3.2 km/h · `normal` 4.5 km/h · `fast` 5.6 km/h
- MET: `slow` 2.8 · `normal` 3.5 · `fast` 4.3
- `kcal = MET × 체중(kg) × 걸린 시간(h)` (체중이 없으면 60kg)
- 화면에는 "일반 건강 정보이며 의료 조언이 아님" 을 함께 보여 줍니다.
