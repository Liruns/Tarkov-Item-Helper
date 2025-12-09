"""
Tarkov Market 맵 SVG 추출 스크립트
Python Playwright를 사용하여 tarkov-market.com에서 맵 SVG를 자동으로 추출합니다.

사용법:
1. pip install playwright
2. playwright install chromium
3. python extract_tarkov_market_maps.py
"""

import asyncio
import os
from playwright.async_api import async_playwright

# 추출할 맵 목록
MAPS = [
    {"name": "GroundZero", "url": "https://tarkov-market.com/maps/ground-zero"},
    {"name": "Factory", "url": "https://tarkov-market.com/maps/factory"},
    {"name": "Customs", "url": "https://tarkov-market.com/maps/customs"},
    {"name": "Woods", "url": "https://tarkov-market.com/maps/woods"},
    {"name": "Shoreline", "url": "https://tarkov-market.com/maps/shoreline"},
    {"name": "Interchange", "url": "https://tarkov-market.com/maps/interchange"},
    {"name": "Reserve", "url": "https://tarkov-market.com/maps/reserve"},
    {"name": "Lighthouse", "url": "https://tarkov-market.com/maps/lighthouse"},
    {"name": "StreetsOfTarkov", "url": "https://tarkov-market.com/maps/streets"},
    {"name": "Labs", "url": "https://tarkov-market.com/maps/lab"},
    {"name": "Labyrinth", "url": "https://tarkov-market.com/maps/labyrinth"},
]

# 저장 경로
OUTPUT_DIR = os.path.join(os.path.dirname(os.path.dirname(__file__)), "TarkovHelper", "Assets", "Maps")


async def extract_map_svg(page, map_info: dict) -> str:
    """페이지에서 맵 SVG를 추출합니다."""

    print(f"  Loading {map_info['name']}...")
    await page.goto(map_info["url"], wait_until="networkidle")

    # 페이지가 완전히 로드될 때까지 대기
    await page.wait_for_timeout(3000)

    # SVG 요소 찾기 - 여러 선택자 시도
    svg_selectors = [
        "svg.svg-map",
        ".map-container svg",
        ".map-wrapper svg",
        "svg[viewBox]",
        "#map svg",
        ".leaflet-container svg",
        "svg"
    ]

    svg_content = None

    for selector in svg_selectors:
        try:
            # 해당 선택자로 SVG 요소들 찾기
            svg_elements = await page.query_selector_all(selector)

            for svg_element in svg_elements:
                # SVG의 outerHTML 가져오기
                content = await svg_element.evaluate("el => el.outerHTML")

                # 맵 SVG인지 확인 (크기가 크거나 viewBox가 있는지)
                if content and len(content) > 10000:  # 맵 SVG는 보통 큼
                    svg_content = content
                    print(f"    Found SVG with selector '{selector}' (size: {len(content)} chars)")
                    break

            if svg_content:
                break

        except Exception as e:
            continue

    if not svg_content:
        # JavaScript로 직접 추출 시도
        print(f"    Trying JavaScript extraction...")
        svg_content = await page.evaluate("""
            () => {
                // 모든 SVG 요소 찾기
                const svgs = document.querySelectorAll('svg');
                let largestSvg = null;
                let maxSize = 0;

                for (const svg of svgs) {
                    const html = svg.outerHTML;
                    if (html.length > maxSize) {
                        maxSize = html.length;
                        largestSvg = html;
                    }
                }

                return largestSvg;
            }
        """)

    return svg_content


async def main():
    print("=" * 60)
    print("Tarkov Market Map SVG Extractor")
    print("=" * 60)

    # 출력 디렉토리 확인
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    print(f"Output directory: {OUTPUT_DIR}\n")

    async with async_playwright() as p:
        # 브라우저 시작
        print("Starting browser...")
        browser = await p.chromium.launch(headless=False)  # headless=False로 디버깅 용이
        context = await browser.new_context(
            viewport={"width": 1920, "height": 1080},
            user_agent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
        )
        page = await context.new_page()

        extracted_count = 0
        failed_maps = []

        for map_info in MAPS:
            print(f"\n[{MAPS.index(map_info) + 1}/{len(MAPS)}] Extracting {map_info['name']}...")

            try:
                svg_content = await extract_map_svg(page, map_info)

                if svg_content:
                    # 파일로 저장
                    output_path = os.path.join(OUTPUT_DIR, f"{map_info['name']}.svg")

                    # SVG 헤더 추가 (필요한 경우)
                    if not svg_content.startswith('<?xml'):
                        svg_content = '<?xml version="1.0" encoding="UTF-8"?>\n' + svg_content

                    with open(output_path, "w", encoding="utf-8") as f:
                        f.write(svg_content)

                    print(f"  [OK] Saved: {output_path}")
                    extracted_count += 1
                else:
                    print(f"  [FAIL] Failed to extract SVG for {map_info['name']}")
                    failed_maps.append(map_info['name'])

            except Exception as e:
                print(f"  [ERROR] Error extracting {map_info['name']}: {e}")
                failed_maps.append(map_info['name'])

        await browser.close()

    # 결과 요약
    print("\n" + "=" * 60)
    print("Extraction Complete!")
    print("=" * 60)
    print(f"Successfully extracted: {extracted_count}/{len(MAPS)} maps")

    if failed_maps:
        print(f"Failed maps: {', '.join(failed_maps)}")

    print(f"\nMaps saved to: {OUTPUT_DIR}")


if __name__ == "__main__":
    asyncio.run(main())
