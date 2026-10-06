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
