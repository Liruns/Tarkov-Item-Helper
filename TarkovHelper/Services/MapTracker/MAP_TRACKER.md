# Map Tracker 좌표 변환 분석

## tarkov.dev 좌표 변환 방식

### 1. 기본 좌표 매핑
```javascript
function pos(position) {
    return [position.z, position.x];  // lat = z, lng = x
}
```

### 2. 회전 적용 (applyRotation)
```javascript
function applyRotation(latLng, rotation) {
    const angleInRadians = (rotation * Math.PI) / 180;
    const cosAngle = Math.cos(angleInRadians);
    const sinAngle = Math.sin(angleInRadians);

    const {lng: x, lat: y} = latLng;
    const rotatedX = x * cosAngle - y * sinAngle;
    const rotatedY = x * sinAngle + y * cosAngle;
    return L.latLng(rotatedY, rotatedX);  // lat = rotatedY, lng = rotatedX
}
```

### 3. CRS (Coordinate Reference System)
```javascript
function getCRS(mapData) {
    let scaleX = mapData.transform[0];
    let scaleY = mapData.transform[2] * -1;  // Y축 반전!
    let marginX = mapData.transform[1];
    let marginY = mapData.transform[3];

    return L.extend({}, L.CRS.Simple, {
        transformation: new L.Transformation(scaleX, marginX, scaleY, marginY),
        projection: {
            project: latLng => L.Projection.LonLat.project(applyRotation(latLng, rotation)),
            unproject: point => applyRotation(L.Projection.LonLat.unproject(point), -rotation),
        },
    });
}
```

### 4. SVG 오버레이 배치
```javascript
const svgBounds = mapData.svgBounds ? getBounds(mapData.svgBounds) : bounds;
svgLayer = L.svgOverlay(svgElement, svgBounds, options);

function getBounds(bounds) {
    // bounds: [[z1, x1], [z2, x2]] → [[lat1, lng1], [lat2, lng2]] (swapped!)
    return L.latLngBounds(
        [bounds[0][1], bounds[0][0]],  // [lat1, lng1]
        [bounds[1][1], bounds[1][0]]   // [lat2, lng2]
    );
}
```

## Woods 맵 설정

```json
{
    "Key": "Woods",
    "ImageWidth": 1402,
    "ImageHeight": 1421,
    "Transform": [0.1855, 113.1, 0.1855, 167.8],
    "CoordinateRotation": 180,
    "SvgBounds": [[650, -945], [-695, 470]]
}
```

## 좌표 변환 공식 (WPF용)

### 올바른 변환 순서:

1. **pos(position)**: `lat = z, lng = x`

2. **applyRotation(rotation)**:
   ```
   rotatedLng = lng * cos(angle) - lat * sin(angle)
   rotatedLat = lng * sin(angle) + lat * cos(angle)
   ```

3. **CRS Transform → Pixel 좌표**:
   ```
   markerPixelX = scaleX * rotatedLng + marginX
   markerPixelY = scaleY * (-1) * rotatedLat + marginY
   ```

4. **SVG bounds → Pixel 좌표**:
   ```
   svgBounds 각 코너를 같은 CRS Transform으로 변환
   → svgPixelXMin, svgPixelXMax, svgPixelYMin, svgPixelYMax
   ```

5. **Pixel → ViewBox 정규화**:
   ```
   normalizedX = (markerPixelX - svgPixelXMin) / (svgPixelXMax - svgPixelXMin)
   normalizedY = (markerPixelY - svgPixelYMin) / (svgPixelYMax - svgPixelYMin)
   viewBoxX = normalizedX * ImageWidth
   viewBoxY = normalizedY * ImageHeight
   ```

## 테스트 좌표

### Jaeger's Camp (지도 중앙-오른쪽)
- API 좌표: `x = -256, y = 9.58, z = 9.7`
- 예상 viewBox: **(991, 482)** ✓

### Search Mission (지도 상단, Friendship Bridge 근처)
- API 좌표: `x = 195.56, y = 12.21, z = -595.82`
- 예상 viewBox: **(520, -127)** ⚠️ Y가 음수 (svgBounds 밖)

## 중요 발견

**일부 퀘스트 위치가 svgBounds 밖에 있음!**

Search Mission 퀘스트는 tarkov.dev의 svgBounds 범위 밖(위쪽)에 위치합니다.
Leaflet은 이를 처리하지만, WPF에서는 마커가 SVG 위쪽에 표시됩니다.

해결 방법:
1. 마커를 클리핑하지 않고 Canvas 밖에도 표시 허용
2. 또는 bounds 값을 확장하여 모든 퀘스트 포함

## SVG viewBox 정보

Woods SVG: `viewBox="0 0 1401.8693 1420.5972"`
- ImageWidth/Height: 1402 x 1421과 거의 일치
