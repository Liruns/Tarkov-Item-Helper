// Test transform calculation
var rotation = 180.0;
var scaleX = 0.1855;
var marginX = 113.1;
var scaleY = -0.1855;  // multiplied by -1 in tarkov.dev
var marginY = 167.8;

// svgBounds: [[maxLat, minLng], [minLat, maxLng]]
var maxLat = 650.0;
var minLng = -945.0;
var minLat = -695.0;
var maxLng = 470.0;

var angleRad = rotation * Math.PI / 180.0;
var cos = Math.Cos(angleRad);
var sin = Math.Sin(angleRad);

Console.WriteLine($"cos={cos:F4}, sin={sin:F4}");

(double x, double y) Transform(double lng, double lat)
{
    var rotLng = lng * cos - lat * sin;
    var rotLat = lng * sin + lat * cos;
    var x = scaleX * rotLng + marginX;
    var y = scaleY * rotLat + marginY;
    Console.WriteLine($"  ({lng}, {lat}) -> rot({rotLng:F2}, {rotLat:F2}) -> screen({x:F2}, {y:F2})");
    return (x, y);
}

Console.WriteLine("\nCorner 1 (minLng, maxLat) = top-left in lat/lng:");
var c1 = Transform(minLng, maxLat);

Console.WriteLine("\nCorner 2 (maxLng, maxLat) = top-right:");
var c2 = Transform(maxLng, maxLat);

Console.WriteLine("\nCorner 3 (minLng, minLat) = bottom-left:");
var c3 = Transform(minLng, minLat);

Console.WriteLine("\nCorner 4 (maxLng, minLat) = bottom-right:");
var c4 = Transform(maxLng, minLat);

var minX = Math.Min(Math.Min(c1.x, c2.x), Math.Min(c3.x, c4.x));
var maxX = Math.Max(Math.Max(c1.x, c2.x), Math.Max(c3.x, c4.x));
var minY = Math.Min(Math.Min(c1.y, c2.y), Math.Min(c3.y, c4.y));
var maxY = Math.Max(Math.Max(c1.y, c2.y), Math.Max(c3.y, c4.y));

Console.WriteLine($"\nSVG Screen Bounds: X={minX:F2}, Y={minY:F2}, Width={maxX - minX:F2}, Height={maxY - minY:F2}");
Console.WriteLine($"SVG viewBox: 0 0 1401.87 1420.60");
Console.WriteLine($"\nScale factor: Width/{1401.87:F2} = {(maxX - minX)/1401.87:F4}");
