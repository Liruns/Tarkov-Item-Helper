// Labs 탈출구 좌표 테스트
// OldMapReferenceData 값
double oldImageWidth = 5500;
double oldImageHeight = 4200;
double[] oldTransform = new[] { 4.39242, 2148.09, 4.12102, 1388.25 };
double[][] oldSvgBounds = new[] { new[] { -80.0, -477.0 }, new[] { -287.0, -193.0 } };
int coordinateRotation = 270;
double newImageWidth = 5500;
double newImageHeight = 4200;

// Labs 탈출구 (x=position.x, z=position.z)
var extracts = new[]
{
    ("Medical Block Elevator", -112.423, -343.986),
    ("Cargo Elevator", -112.152, -408.64),
    ("Main Elevator", -282.304, -334.896),
    ("Ventilation Shaft", -144.94, -396.826),
    ("Sewage Conduit", -122.89, -258.3245),
    ("Parking Gate", -231.73, -434.816)
};

// 회전 적용
(double, double) ApplyRotation(double lng, double lat, int rotationDegrees)
{
    var angleInRadians = rotationDegrees * Math.PI / 180.0;
    var cosAngle = Math.Cos(angleInRadians);
    var sinAngle = Math.Sin(angleInRadians);
    var rotatedLng = lng * cosAngle - lat * sinAngle;
    var rotatedLat = lng * sinAngle + lat * cosAngle;
    return (rotatedLng, rotatedLat);
}

Console.WriteLine("Labs Extract Transform Test");
Console.WriteLine("===========================");

foreach (var (name, gameX, gameZ) in extracts)
{
    // 1. 게임 좌표 → Leaflet (lat=z, lng=x)
    var lat = gameZ;
    var lng = gameX;
    
    // 2. 회전 적용
    var (rotatedLng, rotatedLat) = ApplyRotation(lng, lat, coordinateRotation);
    
    // 3. CRS Transform (Y축 반전 포함)
    var scaleX = oldTransform[0];
    var marginX = oldTransform[1];
    var scaleY = oldTransform[2] * -1;
    var marginY = oldTransform[3];
    
    var markerPixelX = scaleX * rotatedLng + marginX;
    var markerPixelY = scaleY * rotatedLat + marginY;
    
    // 4. SVG bounds → pixel bounds
    var svgLat1 = oldSvgBounds[0][1];
    var svgLng1 = oldSvgBounds[0][0];
    var svgLat2 = oldSvgBounds[1][1];
    var svgLng2 = oldSvgBounds[1][0];
    
    var (svgRotatedLng1, svgRotatedLat1) = ApplyRotation(svgLng1, svgLat1, coordinateRotation);
    var (svgRotatedLng2, svgRotatedLat2) = ApplyRotation(svgLng2, svgLat2, coordinateRotation);
    
    var svgPixelX1 = scaleX * svgRotatedLng1 + marginX;
    var svgPixelY1 = scaleY * svgRotatedLat1 + marginY;
    var svgPixelX2 = scaleX * svgRotatedLng2 + marginX;
    var svgPixelY2 = scaleY * svgRotatedLat2 + marginY;
    
    var svgPixelXMin = Math.Min(svgPixelX1, svgPixelX2);
    var svgPixelXMax = Math.Max(svgPixelX1, svgPixelX2);
    var svgPixelYMin = Math.Min(svgPixelY1, svgPixelY2);
    var svgPixelYMax = Math.Max(svgPixelY1, svgPixelY2);
    
    // 5. ViewBox 정규화
    var normalizedX = (markerPixelX - svgPixelXMin) / (svgPixelXMax - svgPixelXMin);
    var normalizedY = (markerPixelY - svgPixelYMin) / (svgPixelYMax - svgPixelYMin);
    
    var screenX = normalizedX * oldImageWidth;
    var screenY = normalizedY * oldImageHeight;
    
    Console.WriteLine($"{name}:");
    Console.WriteLine($"  Game: ({gameX:F2}, {gameZ:F2})");
    Console.WriteLine($"  Screen: ({screenX:F2}, {screenY:F2})");
    Console.WriteLine();
}
