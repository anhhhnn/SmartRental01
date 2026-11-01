namespace SmartRental.Data;

public sealed record GeoPoint(double Latitude, double Longitude);

public static class GeoAreaCenters
{
    public static readonly IReadOnlyDictionary<string, GeoPoint?> ByArea =
        new Dictionary<string, GeoPoint?>(StringComparer.OrdinalIgnoreCase)
        {
            // Representative points for demo coordinate generation, not exact room GPS coordinates.
            // Administrative boundary values use Nominatim's representative point.
            // OSM data: https://www.openstreetmap.org (ODbL contributors).
            ["Đồng Quang"] = new(21.5860319, 105.8264721), // node 8295998597
            ["Gia Sàng"] = new(21.579028, 105.8502201), // relation 19428377
            ["Phú Xá"] = new(21.5552904, 105.8544880), // node 7884264070, community centre
            ["Tích Lương"] = new(21.547768, 105.8477875), // relation 19428376
            ["Trưng Vương"] = new(21.5974275, 105.8439159), // node 7885513441
            ["Thịnh Đán"] = new(21.5752103, 105.8105539), // node 7884264068
            ["Tân Thịnh"] = new(21.5795544, 105.8148891), // node 8295998598
            ["Hoàng Văn Thụ"] = new(21.5962439, 105.8309698), // node 7796092045
            ["Tân Long"] = new(21.6178734, 105.7949898), // node 8295998600
            ["Túc Duyên"] = new(21.5920406, 105.8521557), // node 7885513440
            ["Trung Thành"] = null,
            ["Phan Đình Phùng"] = new(21.587376, 105.8405390), // relation 19428379
            ["Chùa Hang"] = new(21.6242154, 105.8436651), // node 369503516
            ["Quang Vinh"] = new(21.6084345, 105.8257939), // node 8295998601
            ["Quang Trung"] = new(21.5922531, 105.8223032), // node 7796105332
            ["Cam Giá"] = new(21.5510285, 105.8787468), // node 7884229429
            ["Sơn Cẩm"] = new(21.6327768, 105.7847999), // node 4964032717
            ["Quan Triều"] = new(21.610543, 105.8077114), // relation 19428374
            ["Hương Sơn"] = new(21.5474197, 105.8809802), // node 7884229428
            ["Quyết Thắng"] = new(21.587595, 105.7979509), // relation 19428375
            ["Đồng Bẩm"] = new(21.6112894, 105.8400924) // node 7885513457
        };
}