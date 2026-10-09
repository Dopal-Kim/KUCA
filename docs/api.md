# KUCA 장소 메모 API 명세

앱(Unity)과 메모 서버(`server/`)가 지키는 약속입니다. **이 문서를 바꿀 때는 앱 담당과 서버 담당이 함께 확인합니다.**

- 개발용 주소: `http://localhost:5080` (같은 와이파이의 폰에서는 `http://<맥/PC의 IP>:5080`)
- 요청과 응답 본문은 JSON(UTF-8), 필드 이름은 camelCase
- 시각은 ISO 8601 UTC 문자열 (예: `"2026-10-06T05:12:30Z"`)
- 실패하면 `{"error": "사람이 읽을 수 있는 이유"}` 와 함께 아래 상태 코드를 돌려줍니다

| 코드 | 뜻 |
| --- | --- |
| 200 | 성공 |
| 201 | 만들어짐 (메모 작성) |
| 204 | 성공, 본문 없음 (삭제) |
| 400 | 요청 값이 규칙에 맞지 않음 |
| 403 | 권한 없음 (남의 메모 삭제) |
| 404 | 건물이나 메모가 없음 |

## 건물 ID

- OpenStreetMap 종류와 번호를 이은 문자열입니다: `way-455725718`(공학관), `relation-8269760`(우정원)
- 전체 목록은 `server/KucaMemoServer/Data/buildings.json` (632개, 앱의 3D 건물과 같음)
- 이 목록에 없는 ID로 메모를 쓰거나 읽으면 404

## 데이터 모양

### Building

```json
{ "id": "way-455725718", "name": "공학관", "type": "yes", "lat": 37.2463611, "lon": 127.0805898 }
```

`name`, `type` 은 없으면 `null`.

### Memo

```json
{
  "id": "6f1c2e0a-3b7d-4c55-9a1e-2f8b9d0c7e41",
  "buildingId": "way-455725718",
  "author": "원준",
  "text": "공학관 1층 자판기 고장났어요",
  "photoUrl": "/photos/6f1c2e0a-3b7d-4c55-9a1e-2f8b9d0c7e41.jpg",
  "createdAt": "2026-10-06T05:12:30Z"
}
```

- `id`: 서버가 만드는 Guid 문자열
- `photoUrl`: 서버 기준 경로. 사진이 없으면 `null`. 앱은 `서버 주소 + photoUrl` 로 이미지를 받습니다
- 작성 기기 ID(`deviceId`)는 서버에만 저장하고 **응답에는 넣지 않습니다**

## API

### GET /api/health

서버 동작 확인. `200 {"status":"ok"}`

### GET /api/buildings

건물 목록. `?named=true` 면 이름 있는 건물만. `200 Building[]`

### GET /api/buildings/{buildingId}

건물 하나. `200 Building` / `404`

### GET /api/buildings/{buildingId}/memos

건물의 메모 목록, **최신순**.

| 쿼리 | 기본값 | 설명 |
| --- | --- | --- |
| `limit` | 20 | 1~50. 범위를 벗어나면 가까운 값으로 맞춤 |
| `before` | 없음 | 이 시각보다 이전 메모만 (다음 페이지 불러오기용, 마지막 메모의 `createdAt` 을 넣음) |

`200 Memo[]` (메모가 없으면 `[]`) / `404` 없는 건물

### POST /api/buildings/{buildingId}/memos

메모 작성. 본문은 **multipart/form-data** 입니다 (사진 파일을 같이 보내야 하므로).

| 필드 | 필수 | 규칙 |
| --- | --- | --- |
| `author` | 예 | 앞뒤 공백 제거 후 1~20자 |
| `text` | 예 | 앞뒤 공백 제거 후 1~500자 |
| `deviceId` | 예 | 1~64자. 앱이 기기마다 한 번 만들어 저장해 둔 값 (삭제 권한 확인용) |
| `photo` | 아니오 | JPEG 또는 PNG, 10MB 이하 |

- 성공: `201 Memo`, `Location: /api/memos/{id}` 헤더
- 실패: `400` (규칙 위반, 어떤 필드가 왜 틀렸는지 `error` 에), `404` 없는 건물
- 사진은 `wwwroot/photos/{메모 id}.{jpg|png}` 로 저장하고 `photoUrl` 을 `/photos/{파일 이름}` 으로 채웁니다

### GET /api/memos/{memoId}

메모 하나. `200 Memo` / `404`

### DELETE /api/memos/{memoId}

메모 삭제. 헤더 `X-Device-Id` 가 작성할 때의 `deviceId` 와 같아야 합니다.

- `204` 삭제됨 (사진 파일도 함께 지움)
- `403` 기기 ID가 다름, `404` 없는 메모

### GET /photos/{fileName}

업로드된 사진 파일 (정적 파일).

### GET /api/weather

캠퍼스 지금 날씨. 앱의 캐릭터 출현 조건(`SpawnConditions`)이 씁니다. 서버가 기상청 단기예보 조회서비스
(초단기실황 강수형태 PTY + 초단기예보 하늘상태 SKY, 격자 62·120 = 국제캠퍼스)를 대신 불러 10분 동안 재사용합니다.

```json
{ "condition": "rain", "observedAt": "2026-10-09T05:12:30Z", "source": "KMA" }
```

- `condition`: `clear` 맑음 · `cloudy` 구름많음·흐림 · `rain` 비·빗방울·비/눈 · `snow` 눈·눈날림
- `503` 서버에 기상청 키(`Kma:ServiceKey`)가 없음, `502` 기상청 응답을 못 받았고 이전 값도 없음
- 앱은 실패하면 날씨를 모름으로 두고 날씨 조건 없이 진행합니다

## 웹 페이지 (앱과 무관, 브라우저용)

| 주소 | 내용 |
| --- | --- |
| `/` | 메모가 있는 건물 목록과 건물별 메모 수 |
| `/buildings/{buildingId}` | 그 건물의 메모 목록 (사진, 작성자, 시간) |
