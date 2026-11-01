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

    def test_foreign_language_school_without_radius(self):
        result = parse_search("Tìm phòng gần trường ngoại ngữ")

        self.assertEqual("Trường Ngoại ngữ", result["nearPlace"])
        self.assertIsNone(result["maxDistanceKm"])
        self.assertIsNone(result["khuVuc"])

    def test_foreign_language_school_with_price_and_radius(self):
        result = parse_search("Tìm phòng 2 triệu cách khoảng 2km trường ngoại ngữ")

        self.assertEqual(2_000_000, result["maxPrice"])
        self.assertEqual("Trường Ngoại ngữ", result["nearPlace"])
        self.assertEqual(2, result["maxDistanceKm"])
        self.assertIsNone(result["khuVuc"])

    def test_sfl_alias_with_radius(self):
        result = parse_search("Tìm phòng gần SFL trong 3km")

        self.assertEqual("Trường Ngoại ngữ", result["nearPlace"])
        self.assertEqual(3, result["maxDistanceKm"])
        self.assertIsNone(result["khuVuc"])

    def test_area_search_does_not_infer_landmark(self):
        result = parse_search("Tìm phòng ở Tân Thịnh dưới 2 triệu")

        self.assertEqual("Tân Thịnh", result["khuVuc"])
        self.assertIsNone(result["nearPlace"])

    def test_price_without_landmark_does_not_infer_distance(self):
        result = parse_search("Tìm phòng 2 triệu")

        self.assertEqual(2_000_000, result["maxPrice"])
        self.assertIsNone(result["nearPlace"])
        self.assertIsNone(result["maxDistanceKm"])

    def test_foreign_language_school_aliases(self):
        for alias in (
            "trường ngoại ngữ",
            "ngoại ngữ",
            "đại học ngoại ngữ",
            "trường ngoại ngữ thái nguyên",
            "SFL",
            "SFL TNU",
            "truong ngoai ngu",
            "ngoai ngu",
            "dai hoc ngoai ngu",
        ):
            with self.subTest(alias=alias):
                result = parse_search(f"Tìm phòng gần {alias}")

                self.assertEqual("Trường Ngoại ngữ", result["nearPlace"])

    def test_foreign_language_school_full_name_alias(self):
        result = parse_search(
            "Tìm phòng gần Trường Ngoại ngữ - Đại học Thái Nguyên"
        )

        self.assertEqual("Trường Ngoại ngữ", result["nearPlace"])

    def test_distance_phrasings_with_foreign_language_school(self):
        searches = (
            ("Tìm phòng cách 2km trường ngoại ngữ", 2),
            ("Tìm phòng cách 2 km trường ngoại ngữ", 2),
            ("Tìm phòng cách khoảng 2km trường ngoại ngữ", 2),
            ("Tìm phòng cách khoảng 2 km trường ngoại ngữ", 2),
            ("Tìm phòng trong 3km trường ngoại ngữ", 3),
            ("Tìm phòng trong khoảng 3km trường ngoại ngữ", 3),
            ("Tìm phòng bán kính 2km trường ngoại ngữ", 2),
            ("Tìm phòng trong bán kính 2 km trường ngoại ngữ", 2),
            ("Tìm phòng khoảng 2km trường ngoại ngữ", 2),
            ("Tìm phòng cách trường ngoại ngữ 2km", 2),
            ("Tìm phòng trong bán kính 3km quanh trường ngoại ngữ", 3),
        )

        for search, expected_distance in searches:
            with self.subTest(search=search):
                result = parse_search(search)

                self.assertEqual("Trường Ngoại ngữ", result["nearPlace"])
                self.assertEqual(expected_distance, result["maxDistanceKm"])

    def test_minimum_price_from_amount(self):
        result = parse_search("Tìm phòng từ 1 triệu")

        self.assertEqual(1_000_000, result["minPrice"])
        self.assertIsNone(result["maxPrice"])

    def test_minimum_and_maximum_price_with_inequalities(self):
        result = parse_search("Tìm phòng trên 1 triệu dưới 2 triệu")

        self.assertEqual(1_000_000, result["minPrice"])
        self.assertEqual(2_000_000, result["maxPrice"])

    def test_price_range(self):
        result = parse_search("Tìm phòng từ 1,2 đến 1,8 triệu")

        self.assertEqual(1_200_000, result["minPrice"])
        self.assertEqual(1_800_000, result["maxPrice"])

    def test_price_range_with_separate_units(self):
        result = parse_search("Tìm phòng từ 1 triệu đến 2 triệu")

        self.assertEqual(1_000_000, result["minPrice"])
        self.assertEqual(2_000_000, result["maxPrice"])

    def test_minimum_price_and_people_count(self):
        result = parse_search("Tìm phòng từ 1 triệu cho 2 người")

        self.assertEqual(1_000_000, result["minPrice"])
        self.assertIsNone(result["maxPrice"])
        self.assertEqual(2, result["soNguoi"])

    def test_price_does_not_match_distance(self):
        result = parse_search("Tìm phòng trong 2km gần ICTU")

        self.assertIsNone(result["minPrice"])
        self.assertIsNone(result["maxPrice"])
        self.assertEqual(2, result["maxDistanceKm"])

    def test_price_above_and_below_with_mixed_units(self):
        result = parse_search("Tìm phòng trên 1tr dưới 2 triệu")

        self.assertEqual(1_000_000, result["minPrice"])
        self.assertEqual(2_000_000, result["maxPrice"])

    def test_single_amount_keeps_maximum_price_meaning(self):
        result = parse_search("Tìm phòng 2 triệu cho 2 người")

        self.assertIsNone(result["minPrice"])
        self.assertEqual(2_000_000, result["maxPrice"])
        self.assertEqual(2, result["soNguoi"])

    def test_new_amenity_predictions(self):
        examples = (
            ("Tìm phòng có giường", "giường"),
            ("Tìm phòng có tủ quần áo", "tủ quần áo"),
            ("Tìm phòng khép kín", "khép kín"),
        )

        for search, amenity in examples:
            with self.subTest(amenity=amenity):
                result = parse_search(search)

                self.assertIn(amenity, result["tienNghi"])

    def test_combined_new_and_existing_amenities(self):
        result = parse_search("Tìm phòng có giường, wifi và điều hòa")

        self.assertIn("giường", result["tienNghi"])
        self.assertIn("wifi", result["tienNghi"])
        self.assertIn("điều hòa", result["tienNghi"])


if __name__ == "__main__":
    unittest.main()
