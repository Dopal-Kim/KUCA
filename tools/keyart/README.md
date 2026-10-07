# 키아트 지도 생성 도구

컨셉아트(`Art/Concept`)의 바닥 타일·건물 아이콘과 OpenStreetMap 데이터로 캠퍼스 지도를 키아트 스타일로 만듭니다.
지오메트리·지면은 전부 Python 한 곳(`build_art.py`)에서 만들고, Unity 와 웹 미리보기는 그 결과를 읽기만 합니다.

```bash
cd tools/keyart
uv run --with numpy --with pillow --with scipy python build_art.py     # 약 2분
```

결과 (`My project/Assets/Art/KeyArt`):

| 파일 | 내용 |
| --- | --- |
| `CampusGround.jpg` | 지면 텍스처 3752×4096 (≈2.6 px/m): 잔디·포장·도로(중앙선·횡단보도)·물(석재 테)·운동장·화단, 건물·나무 둘레 그늘 |
| `GrassDetail.png` | 가까이서 겹쳐 쓰는 잔디 결 |
| `KeyArtGeometry.bytes` | 버텍스 색 로우폴리 메시 (gzip, 2 cm 정수 좌표): 나무·관목·산울타리, 건물 기단·코니스·옥상 난간·실외기·입구 캐노피, 랜드마크 |
| `KeyArtManifest.json` | 건물별 외벽 스타일, 랜드마크로 대신해 숨길 상자 건물 |
| `KeyArtLook.json` | **직접 고치는 파일**: 해·환경광·그림자 세기·안개·카메라·외벽 스타일 색과 창문 크기 |

그다음 Unity 메뉴 **KUCA → Map Style → Apply Key Art** 를 누르면 지면·건물 재질·지오메트리·조명·카메라·URP 그림자 설정이 적용됩니다.
원래 Mapbox 지도로 되돌리려면 **KUCA → Map Style → Apply Mapbox**.

- 지오메트리는 `KeyArtGeometry` 컴포넌트가 켜질 때 `.bytes` 에서 메시를 만듭니다 (씬·저장소에 큰 메시 에셋을 두지 않음).
- 셰이더: `KUCA/StylizedGround`, `KUCA/StylizedBuilding`, `KUCA/VertexColorLit` — 공통 조명은 `Shaders/KUCAStylizedLighting.hlsl`.
- `osm_ground.json`: 캠퍼스 범위의 도로·녹지·물·주차장 (Overpass API, 2026-10-06)

## 웹 미리보기 (Unity 없이 확인)

Unity 셰이더와 같은 조명 모델로 같은 데이터를 렌더합니다. `KeyArtLook.json` 을 고친 뒤 바로 확인할 때 씁니다.

```bash
cd tools/keyart/preview
npm install
node render.mjs                        # shots/*.png (사색의 광장, 중앙도서관, 노천극장, 대운동장, 전경)
node render.mjs mine=120,-400,300,30   # 이름=x,z,거리,요 (월드 m, 도)
```

## 코드 구성 (`kuca_art/`)

| 모듈 | 하는 일 |
| --- | --- |
| `geo.py` | 위경도 ↔ 월드 좌표, OSM 건물 읽기 (`CampusBuildings.cs` 와 같은 규칙), 최소 사각형 |
| `mesh.py` | 각진 로우폴리 메시 조립기, 다각형 띠(기단·난간), 청크, `.bytes` 쓰기 |
| `ground.py` | 지면 마스크·텍스처, 나무 배치 가능 영역 |
| `buildings.py` | 모든 건물의 공통 디테일, 입구, 산울타리 |
| `landmarks.py` | 컨셉아트 랜드마크 (아래 표) |
| `nature.py` | 나무·관목 모델과 배치 |

## 건물별 특징 (`landmarks.py`)

| 건물 | 처리 |
| --- | --- |
| 중앙도서관 | 신고전주의 외벽 + 사색의 광장 쪽 열주 현관·페디먼트, 넓은 대계단과 난간벽 |
| 예술디자인대학 | 신고전주의 외벽 + 열주 현관과 페디먼트 |
| 체육대학관 | 신고전주의 외벽 + 열주 |
| 선승관 | 1층 아치 회랑 + 시계탑 (금색 테 시계 네 면, 아치 창, 뾰족 지붕) |
| 천문대 | 지붕 위 흰 돔, 관측 창, 둥근 발코니 |
| 도예관 | 박공지붕과 지붕창 |
| 평화노천극장 | 상자 대신 반원 계단석 + 무대·아치 배경 벽 (연못 쪽) |
| 정문 | 상자 대신 석조 열주 문 (금색 문장) |
| 공학관, 공학실험동, 전자정보대학 | 흰 벽 + 가로 띠 창 |
| 멀티미디어교육관, 글로벌관 | 유리 외벽 |
| 우정원 | 붉은 벽돌 외벽 + 크림 띠 |
| 사색의 광장 | 오벨리스크 2기 + 석재 포장 |
| 대운동장 | 붉은 트랙·레인, 축구장, 둘레 벚꽃길, 조명탑 4기, 관중석 |
