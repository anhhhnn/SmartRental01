import unittest

from main import parse_search


class SearchParserTests(unittest.TestCase):
    def test_landmark_with_radius_does_not_infer_area(self):
        result = parse_search("Tìm phòng gần Đại học Khoa học trong 3km")

        self.assertEqual("Đại học Khoa học", result["nearPlace"])
        self.assertEqual(3, result["maxDistanceKm"])
        self.assertIsNone(result["khuVuc"])

    def test_combined_filters(self):
        result = parse_search(
            "Tìm phòng dưới 2 triệu cho 2 người có wifi "
            "gần Đại học Nông Lâm trong 2km"
        )

        self.assertEqual(2_000_000, result["maxPrice"])
        self.assertEqual(2, result["soNguoi"])
        self.assertIn("wifi", result["tienNghi"])
        self.assertEqual("Đại học Nông Lâm", result["nearPlace"])
        self.assertEqual(2, result["maxDistanceKm"])
        self.assertIsNone(result["khuVuc"])

    def test_landmark_without_radius(self):
        result = parse_search("Tìm phòng gần ICTU")

        self.assertEqual(
            "Đại học Công nghệ Thông tin và Truyền thông",
            result["nearPlace"],
        )
        self.assertIsNone(result["maxDistanceKm"])
        self.assertIsNone(result["khuVuc"])

    def test_existing_area_search(self):
        result = parse_search("Tìm phòng ở Tân Thịnh")

        self.assertEqual("Tân Thịnh", result["khuVuc"])
        self.assertIsNone(result["nearPlace"])

    def test_alias_with_punctuation(self):
        result = parse_search("Có phòng nào quanh ICTU?")

        self.assertEqual(
            "Đại học Công nghệ Thông tin và Truyền thông",
            result["nearPlace"],
        )

    def test_distance_before_landmark(self):
        result = parse_search("Cách Đại học Khoa học dưới 3km")

        self.assertEqual("Đại học Khoa học", result["nearPlace"])
        self.assertEqual(3, result["maxDistanceKm"])
        self.assertIsNone(result["khuVuc"])

    def test_radius_around_landmark(self):
        result = parse_search("Trong bán kính 2km quanh ICTU")

        self.assertEqual(
            "Đại học Công nghệ Thông tin và Truyền thông",
            result["nearPlace"],
        )
        self.assertEqual(2, result["maxDistanceKm"])

    def test_school_prefix_alias(self):
        result = parse_search("Cách trường Sư phạm khoảng 2 km")

        self.assertEqual("Đại học Sư phạm", result["nearPlace"])
        self.assertEqual(2, result["maxDistanceKm"])


if __name__ == "__main__":
    unittest.main()
