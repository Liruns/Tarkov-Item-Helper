# SVG 렌더링 문제 해결 계획

## 현재 문제점

### 1. CSS 클래스 스타일 미적용
tarkov.dev SVG 파일들은 `<style>` 태그 내에 CSS 클래스를 정의하고 사용합니다:
```xml
<style id="style_common">
  .trees { fill:#144043 }
  .land { fill:#1f5054 }
  .road_tarmac { fill:none;stroke:#888 }
  .road_small { stroke-width:5 }
  ...
</style>
<g id="Trees" class="trees">
  <path d="..."/>
</g>
```

**문제**: SharpVectors.Wpf가 CSS 클래스 기반 스타일을 제대로 파싱하지 못함

### 2. 복합 클래스 사용
일부 요소는 여러 클래스를 함께 사용:
```xml
<g id="Roads" class="road_tarmac road_medium">
```
이 경우 `road_tarmac`의 stroke 색상과 `road_medium`의 stroke-width를 모두 적용해야 함

### 3. 맵 위치/크기 문제
PNG 변환 시 viewBox 좌표계와 렌더링 좌표계 불일치로 맵이 잘리거나 위치가 어긋남

---

## 해결 방안

### 방안 1: SVG 전처리 - CSS 클래스를 인라인 스타일로 변환 (권장)
SVG 파일을 로드할 때 CSS 클래스를 파싱하고, 각 요소의 인라인 `style` 속성으로 변환

**장점**:
- 원본 SVG의 모든 스타일 완벽 지원
- 줌/패닝 시 벡터 품질 유지 가능
- 메모리 효율적

**단계**:
1. SVG 파일 로드
2. `<style>` 태그 파싱하여 CSS 규칙 추출
3. 각 요소의 `class` 속성을 읽어 해당 스타일을 `style` 속성으로 변환
4. 변환된 SVG를 SharpVectors로 렌더링

### 방안 2: 외부 SVG → PNG 변환 도구 사용
빌드 시점에 Inkscape, Chrome Headless 등으로 SVG를 PNG로 미리 변환

**단점**:
- 빌드 의존성 추가
- 줌인 시 픽셀화
- 파일 크기 증가

### 방안 3: WebView2로 SVG 렌더링
WPF WebView2 컨트롤에서 브라우저 엔진으로 SVG 렌더링

**단점**:
- 무거움
- 마커 오버레이 복잡

---

## 선택: 방안 1 - SVG 전처리

### 구현 계획

#### Step 1: SvgStylePreprocessor 클래스 생성
```csharp
public class SvgStylePreprocessor
{
    // CSS 클래스 정의를 파싱
    Dictionary<string, Dictionary<string, string>> ParseCssStyles(string styleContent);

    // 클래스를 인라인 스타일로 변환
    string ConvertClassesToInlineStyles(string svgContent);
}
```

#### Step 2: CSS 파싱 로직
```
입력: ".trees { fill:#144043 }"
출력: { "trees": { "fill": "#144043" } }

입력: ".road_tarmac { fill:none;stroke:#888 }"
출력: { "road_tarmac": { "fill": "none", "stroke": "#888" } }
```

#### Step 3: 복합 클래스 병합
```
입력: class="road_tarmac road_medium"
처리:
  1. road_tarmac → { fill:none, stroke:#888 }
  2. road_medium → { stroke-width:8 }
  3. 병합 → style="fill:none;stroke:#888;stroke-width:8"
```

#### Step 4: MapTrackerPage에서 사용
```csharp
private void LoadMapImage(string mapKey)
{
    // SVG 파일인 경우 전처리
    var preprocessor = new SvgStylePreprocessor();
    var processedSvg = preprocessor.ConvertClassesToInlineStyles(svgContent);

    // 처리된 SVG를 SharpVectors로 렌더링
    // ...
}
```

---

## 테스트 케이스

1. **기본 클래스**: `class="trees"` → `style="fill:#144043"`
2. **복합 클래스**: `class="road_tarmac road_medium"` → `style="fill:none;stroke:#888;stroke-width:8"`
3. **기존 style 보존**: `class="trees" style="opacity:0.5"` → `style="fill:#144043;opacity:0.5"`
4. **중첩 그룹**: 부모 그룹의 클래스가 자식에게 상속되는 경우 처리

---

## 파일 구조

```
TarkovHelper/Services/MapTracker/
├── SvgStylePreprocessor.cs   # CSS→인라인 변환
├── MapTrackerPage.xaml.cs    # 수정: 전처리 후 렌더링
└── SVG_RENDERING_PLAN.md     # 이 문서
```
