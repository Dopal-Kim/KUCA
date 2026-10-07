# 키아트 지도 생성 도구

컨셉아트(`Art/Concept`)의 바닥 타일과 OpenStreetMap 데이터로 키아트 스타일 지면과 나무 위치를 만듭니다.

```bash
cd tools/keyart
mkdir -p tiles
uvx --with pillow --with numpy python crop_tiles.py          # 타일 시트 → tiles/*.png (8종)
uvx --with pillow --with numpy python render_ground.py \
    ../../"My project"/Assets/Art/KeyArt/CampusGround.jpg \
    ../../"My project"/Assets/Art/KeyArt/CampusTrees.json \
    osm_ground.json ../../"My project"/Assets/Data/CampusBuildings.json
```

그다음 Unity 메뉴 **KUCA → Map Style → Apply Key Art** 를 누르면 지면·건물·나무·하늘이 적용됩니다.
원래 Mapbox 지도로 되돌리려면 **KUCA → Map Style → Apply Mapbox**.

- `osm_ground.json`: 캠퍼스 범위의 도로·녹지·물·주차장 (Overpass API, 2026-10-06)
- 지면 텍스처 3752×4096 (약 2.6 px/m), 나무 3538그루 (침엽수·활엽수·벚나무)

## 건물별 특징 (KeyArtLandmarks)

`Assets/Editor/KeyArtLandmarks.cs` 가 컨셉아트 아이콘을 따라 건물마다 외벽과 형태를 덧붙입니다 (Apply Key Art 에 포함).

| 건물 | 처리 |
| --- | --- |
| 중앙도서관, 예술디자인대학 | 신고전주의 외벽 + 사색의 광장 쪽 열주 현관과 페디먼트, 계단 |
| 체육대학관 | 신고전주의 외벽 + 열주 |
| 선승관 | 열주 + 시계탑 |
| 천문대 | 지붕 위 흰 돔 |
| 도예관 | 박공지붕 |
| 평화노천극장 | 상자 대신 반원 계단석 + 무대와 배경 벽 (연못 쪽) |
| 정문 | 상자 대신 석조 열주 문 |
| 공학관, 공학실험동, 전자정보대학 | 흰 벽 + 가로 띠 창 |
| 멀티미디어교육관, 글로벌관 | 유리 외벽 |
| 우정원 | 붉은 벽돌 외벽 |
| 사색의 광장 | 오벨리스크 2기 + 석재 포장 |
| 대운동장 | 붉은 트랙과 축구장, 둘레 벚꽃 (지면 텍스처) |
