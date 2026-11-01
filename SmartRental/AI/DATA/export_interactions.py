"""Export the Recommendation AI interaction dataset from SQL Server.

This utility is intentionally read-only. It uses the project's existing SQL Server
connection string and the installed ``sqlcmd`` client, so it adds no Python package
dependency.
"""

from __future__ import annotations

import argparse
import csv
import json
import os
import shutil
import statistics
import subprocess
import sys
from collections import Counter
from dataclasses import dataclass
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[1]
CSV_PATH = SCRIPT_DIR / "interactions.csv"
REPORT_PATH = SCRIPT_DIR / "interactions_report.md"
DELIMITER = "\t"
@dataclass(frozen=True)
class Interaction:
    user_id: str
    room_id: int
    views: int
    favorite: int
    booking: int
    rating: int

    @property
    def score(self) -> int:
        return self.views + self.favorite * 3 + self.booking * 5 + self.rating


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Export the read-only Recommendation AI interaction dataset."
    )
    parser.add_argument(
        "--environment",
        default=os.environ.get("ASPNETCORE_ENVIRONMENT", "Development"),
        help="ASP.NET Core environment used to select appsettings.<Environment>.json.",
    )
    parser.add_argument(
        "--connection-string",
        help=(
            "Optional connection string override. Prefer configuration or the "
            "ConnectionStrings__DefaultConnection environment variable."
        ),
    )
    parser.add_argument("--output", type=Path, default=CSV_PATH)
    parser.add_argument("--report", type=Path, default=REPORT_PATH)
    return parser.parse_args()


def load_connection_string(args: argparse.Namespace) -> str:
    if args.connection_string:
        return args.connection_string
    environment_value = os.environ.get("ConnectionStrings__DefaultConnection")
    if environment_value:
        return environment_value

    candidates = [
        PROJECT_ROOT / f"appsettings.{args.environment}.json",
        PROJECT_ROOT / "appsettings.json",
    ]
    for path in candidates:
        if not path.exists():
            continue
        with path.open(encoding="utf-8-sig") as handle:
            value = json.load(handle).get("ConnectionStrings", {}).get(
                "DefaultConnection"
            )
        if value:
            return value
    raise RuntimeError(
        "DefaultConnection was not found in configuration or environment variables."
    )


def split_connection_string(value: str) -> dict[str, str]:
    parts: dict[str, str] = {}
    for segment in value.split(";"):
        if not segment.strip() or "=" not in segment:
            continue
        key, item = segment.split("=", 1)
        parts[key.strip().lower()] = item.strip()
    return parts


class SqlCmd:
    def __init__(self, connection_string: str) -> None:
        executable = shutil.which("sqlcmd")
        if not executable:
            raise RuntimeError(
                "sqlcmd is required but was not found on PATH. Install Microsoft "
                "SQL Server command-line tools."
            )
        values = split_connection_string(connection_string)
        server = values.get("server") or values.get("data source")
        database = values.get("database") or values.get("initial catalog")
        if not server or not database:
            raise RuntimeError("DefaultConnection must specify Server and Database.")

        self.database = database
        self.command = [
            executable,
            "-S",
            server,
            "-d",
            database,
            "-b",
            "-r",
            "1",
            "-C",
            "-h",
            "-1",
            "-W",
            "-s",
            DELIMITER,
        ]
        self.environment = os.environ.copy()
        trusted = (values.get("trusted_connection") or "").lower() in {
            "true",
            "yes",
            "sspi",
        }
        integrated = (values.get("integrated security") or "").lower() in {
            "true",
            "yes",
            "sspi",
        }
        if trusted or integrated:
            self.command.append("-E")
        else:
            user = values.get("user id") or values.get("uid")
            password = values.get("password") or values.get("pwd")
            if not user:
                raise RuntimeError(
                    "SQL authentication requires User ID in DefaultConnection."
                )
            self.command.extend(["-U", user])
            if password:
                # Avoid exposing a password in the process command line.
                self.environment["SQLCMDPASSWORD"] = password

    def rows(self, query: str) -> list[list[str]]:
        completed = subprocess.run(
            [*self.command, "-Q", "SET NOCOUNT ON;\n" + query],
            cwd=PROJECT_ROOT,
            env=self.environment,
            text=True,
            encoding="utf-8",
            errors="strict",
            capture_output=True,
            check=False,
        )
        if completed.returncode:
            detail = completed.stderr.strip() or completed.stdout.strip()
            raise RuntimeError(f"SQL query failed: {detail}")
        return [
            line.split(DELIMITER)
            for line in completed.stdout.splitlines()
            if line.strip()
        ]


SIGNALS_CTE = r"""
WITH ValidSignals AS
(
    SELECT ls.UserId, ls.PhongtroId, ls.SoLanXem AS Views,
           0 AS Favorite, 0 AS Booking, 0 AS Rating
    FROM LichSuXems AS ls
    INNER JOIN AspNetUsers AS u ON u.Id = ls.UserId
    INNER JOIN Phongtros AS p ON p.Id = ls.PhongtroId

    UNION ALL

    SELECT yt.UserId, yt.PhongtroId, 0, 1, 0, 0
    FROM YeuThichs AS yt
    INNER JOIN AspNetUsers AS u ON u.Id = yt.UserId
    INNER JOIN Phongtros AS p ON p.Id = yt.PhongtroId

    UNION ALL

    SELECT lx.NguoiThueId, lx.PhongtroId, 0, 0, 1, 0
    FROM LichXemPhongs AS lx
    INNER JOIN AspNetUsers AS u ON u.Id = lx.NguoiThueId
    INNER JOIN Phongtros AS p ON p.Id = lx.PhongtroId
    WHERE lx.TrangThai IN (N'ChoXacNhan', N'DaDongY')
    GROUP BY lx.NguoiThueId, lx.PhongtroId

    UNION ALL

    SELECT dg.UserId, dg.PhongtroId, 0, 0, 0, dg.SoSao
    FROM DanhGias AS dg
    INNER JOIN AspNetUsers AS u ON u.Id = dg.UserId
    INNER JOIN Phongtros AS p ON p.Id = dg.PhongtroId
),
Aggregated AS
(
    SELECT UserId, PhongtroId, SUM(Views) AS Views,
           MAX(Favorite) AS Favorite, MAX(Booking) AS Booking,
           MAX(Rating) AS Rating
    FROM ValidSignals
    GROUP BY UserId, PhongtroId
)
"""


def load_interactions(db: SqlCmd, tenants_only: bool) -> list[Interaction]:
    role_filter = ""
    if tenants_only:
        role_filter = r"""
WHERE EXISTS
(
    SELECT 1
    FROM AspNetUserRoles AS ur
    INNER JOIN AspNetRoles AS r ON r.Id = ur.RoleId
    WHERE ur.UserId = a.UserId AND r.Name = N'NguoiThue'
)
"""
    query = SIGNALS_CTE + f"""
SELECT a.UserId, a.PhongtroId, a.Views, a.Favorite, a.Booking, a.Rating
FROM Aggregated AS a
{role_filter}
ORDER BY a.UserId, a.PhongtroId;
"""
    interactions: list[Interaction] = []
    for row in db.rows(query):
        if len(row) != 6:
            raise RuntimeError(f"Unexpected interaction row returned by SQL: {row!r}")
        interactions.append(
            Interaction(row[0], *(int(value) for value in row[1:]))
        )
    return interactions


def key_value_query(db: SqlCmd, query: str) -> dict[str, int]:
    result: dict[str, int] = {}
    for row in db.rows(query):
        if len(row) != 2:
            raise RuntimeError(f"Unexpected diagnostic row returned by SQL: {row!r}")
        result[row[0]] = int(row[1])
    return result


def database_diagnostics(db: SqlCmd) -> tuple[dict[str, int], dict[str, int]]:
    diagnostics = key_value_query(
        db,
        r"""
SELECT Metric, Value
FROM
(
    SELECT 'total_rooms' AS Metric, COUNT_BIG(*) AS Value FROM Phongtros
    UNION ALL SELECT 'total_users', COUNT_BIG(*) FROM AspNetUsers
    UNION ALL SELECT 'tenant_users', COUNT_BIG(DISTINCT ur.UserId)
      FROM AspNetUserRoles ur INNER JOIN AspNetRoles r ON r.Id=ur.RoleId
      WHERE r.Name=N'NguoiThue'
    UNION ALL SELECT 'view_rows', COUNT_BIG(*) FROM LichSuXems
    UNION ALL SELECT 'favorite_rows', COUNT_BIG(*) FROM YeuThichs
    UNION ALL SELECT 'booking_rows', COUNT_BIG(*) FROM LichXemPhongs
    UNION ALL SELECT 'rating_rows', COUNT_BIG(*) FROM DanhGias
    UNION ALL SELECT 'orphan_views', COUNT_BIG(*) FROM LichSuXems s
      WHERE NOT EXISTS (SELECT 1 FROM AspNetUsers u WHERE u.Id=s.UserId)
         OR NOT EXISTS (SELECT 1 FROM Phongtros p WHERE p.Id=s.PhongtroId)
    UNION ALL SELECT 'orphan_favorites', COUNT_BIG(*) FROM YeuThichs s
      WHERE NOT EXISTS (SELECT 1 FROM AspNetUsers u WHERE u.Id=s.UserId)
         OR NOT EXISTS (SELECT 1 FROM Phongtros p WHERE p.Id=s.PhongtroId)
    UNION ALL SELECT 'orphan_bookings', COUNT_BIG(*) FROM LichXemPhongs s
      WHERE NOT EXISTS (SELECT 1 FROM AspNetUsers u WHERE u.Id=s.NguoiThueId)
         OR NOT EXISTS (SELECT 1 FROM Phongtros p WHERE p.Id=s.PhongtroId)
    UNION ALL SELECT 'orphan_ratings', COUNT_BIG(*) FROM DanhGias s
      WHERE NOT EXISTS (SELECT 1 FROM AspNetUsers u WHERE u.Id=s.UserId)
         OR NOT EXISTS (SELECT 1 FROM Phongtros p WHERE p.Id=s.PhongtroId)
    UNION ALL SELECT 'invalid_views', COUNT_BIG(*) FROM LichSuXems WHERE SoLanXem < 0
    UNION ALL SELECT 'invalid_ratings', COUNT_BIG(*) FROM DanhGias WHERE SoSao NOT BETWEEN 1 AND 5
) AS d
ORDER BY Metric;
""",
    )
    statuses = key_value_query(
        db,
        "SELECT TrangThai, COUNT_BIG(*) FROM LichXemPhongs GROUP BY TrangThai ORDER BY TrangThai;",
    )
    demo_metrics = key_value_query(
        db,
        SIGNALS_CTE
        + r"""
SELECT 'demo_tenant_users', COUNT_BIG(DISTINCT u.Id)
FROM AspNetUsers AS u
INNER JOIN AspNetUserRoles AS ur ON ur.UserId = u.Id
INNER JOIN AspNetRoles AS r ON r.Id = ur.RoleId AND r.Name = N'NguoiThue'
WHERE u.Email LIKE N'nguoithue%@smartrental.local'
UNION ALL
SELECT 'demo_interactions', COUNT_BIG(*)
FROM Aggregated AS a
INNER JOIN AspNetUsers AS u ON u.Id = a.UserId
WHERE u.Email LIKE N'nguoithue%@smartrental.local'
  AND EXISTS
  (
      SELECT 1 FROM AspNetUserRoles AS ur
      INNER JOIN AspNetRoles AS r ON r.Id = ur.RoleId
      WHERE ur.UserId = a.UserId AND r.Name = N'NguoiThue'
  );
""",
    )
    diagnostics.update(demo_metrics)
    return diagnostics, statuses


def percentile_summary(values: list[int]) -> dict[str, float]:
    if not values:
        return {"min": 0, "max": 0, "mean": 0.0, "median": 0.0}
    return {
        "min": min(values),
        "max": max(values),
        "mean": statistics.fmean(values),
        "median": statistics.median(values),
    }


def validate(interactions: list[Interaction]) -> list[str]:
    errors: list[str] = []
    keys = [(item.user_id, item.room_id) for item in interactions]
    duplicate_count = len(keys) - len(set(keys))
    if duplicate_count:
        errors.append(f"duplicate(UserId, PhongtroId) = {duplicate_count}")
    for index, item in enumerate(interactions, start=2):
        if not item.user_id:
            errors.append(f"row {index}: UserId is empty")
        if item.views < 0:
            errors.append(f"row {index}: Views = {item.views}")
        if item.favorite not in (0, 1):
            errors.append(f"row {index}: Favorite = {item.favorite}")
        if item.booking not in (0, 1):
            errors.append(f"row {index}: Booking = {item.booking}")
        if item.rating not in range(0, 6):
            errors.append(f"row {index}: Rating = {item.rating}")
        if item.score <= 0:
            errors.append(f"row {index}: Score = {item.score}")
    return errors


def write_csv(path: Path, interactions: list[Interaction]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerow(
            ["UserId", "PhongtroId", "Views", "Favorite", "Booking", "Rating", "Score"]
        )
        for item in interactions:
            writer.writerow(
                [
                    item.user_id,
                    item.room_id,
                    item.views,
                    item.favorite,
                    item.booking,
                    item.rating,
                    item.score,
                ]
            )


def mask_user_id(user_id: str) -> str:
    if len(user_id) <= 8:
        return user_id[:2] + "..." + user_id[-2:]
    return user_id[:4] + "..." + user_id[-4:]


def fmt_number(value: float) -> str:
    return f"{value:,.2f}"


def build_report(
    db_name: str,
    interactions: list[Interaction],
    all_interactions: list[Interaction],
    diagnostics: dict[str, int],
    statuses: dict[str, int],
    errors: list[str],
) -> str:
    users = Counter(item.user_id for item in interactions)
    rooms = Counter(item.room_id for item in interactions)
    per_user = list(users.values())
    scores = [item.score for item in interactions]
    views = [item.views for item in interactions]
    user_stats = percentile_summary(per_user)
    score_stats = percentile_summary(scores)
    possible = len(users) * len(rooms)
    density = len(interactions) / possible * 100 if possible else 0.0
    sparsity = 100.0 - density if possible else 100.0
    distribution = {
        "1 interaction": sum(value == 1 for value in per_user),
        "2-4 interactions": sum(2 <= value <= 4 for value in per_user),
        "5-9 interactions": sum(5 <= value <= 9 for value in per_user),
        "10-19 interactions": sum(10 <= value <= 19 for value in per_user),
        ">=20 interactions": sum(value >= 20 for value in per_user),
    }
    signal_counts = {
        "Views > 0": sum(item.views > 0 for item in interactions),
        "Favorite = 1": sum(item.favorite == 1 for item in interactions),
        "Booking = 1": sum(item.booking == 1 for item in interactions),
        "Rating > 0": sum(item.rating > 0 for item in interactions),
    }
    orphan_total = sum(
        diagnostics[key]
        for key in (
            "orphan_views",
            "orphan_favorites",
            "orphan_bookings",
            "orphan_ratings",
        )
    )
    invalid_source_total = diagnostics["invalid_views"] + diagnostics["invalid_ratings"]
    zero_rooms = diagnostics["total_rooms"] - len(rooms)
    top_rooms = rooms.most_common(10)
    cold_users = distribution["1 interaction"] + distribution["2-4 interactions"]
    demo_share = diagnostics["demo_interactions"] / max(len(interactions), 1)
    cf_ready = (
        len(users) >= 50
        and density >= 1.0
        and cold_users / max(len(users), 1) < 0.5
        and demo_share < 0.5
    )
    recommendation = (
        "Dữ liệu đạt ngưỡng thăm dò ban đầu cho Collaborative Filtering; vẫn nên dùng "
        "hybrid để xử lý cold-start."
        if cf_ready
        else "Ưu tiên Content-Based (sau đó hybrid); dữ liệu hiện tại còn thưa hoặc có "
        "quá nhiều user ít interaction để chỉ dựa vào Collaborative Filtering."
    )
    status_text = ", ".join(f"`{key}`: {value}" for key, value in statuses.items()) or "Không có"
    validation_text = "Đạt" if not errors and not invalid_source_total else "Không đạt"

    lines = [
        "# Recommendation interaction dataset report",
        "",
        "## 1. Nguồn dữ liệu và schema đã audit",
        "",
        f"Database: `{db_name}`. Export chỉ đọc dữ liệu và lọc user có role `NguoiThue`.",
        "",
        "| Signal | Bảng | Khóa chính | User | Phòng | Giá trị | Unique user-phòng |",
        "|---|---|---|---|---|---|---|",
        "| Views | `LichSuXems` | (`UserId`, `PhongtroId`) | `UserId` | `PhongtroId` | `SoLanXem` | Có (PK) |",
        "| Favorite | `YeuThichs` | (`UserId`, `PhongtroId`) | `UserId` | `PhongtroId` | sự tồn tại của record | Có (PK) |",
        "| Booking | `LichXemPhongs` | `Id` | `NguoiThueId` | `PhongtroId` | `TrangThai` | Không |",
        "| Rating | `DanhGias` | (`UserId`, `PhongtroId`) | `UserId` | `PhongtroId` | `SoSao` | Có (PK) |",
        "| Room | `Phongtros` | `Id` | `OwnerId` (owner) | `Id` | `TrangThai`, `IsVisible` | Không áp dụng |",
        "| User/role | `AspNetUsers` / `AspNetUserRoles` / `AspNetRoles` | `Id` / khóa ghép / `Id` | `Id` | — | `AspNetRoles.Name` | Theo Identity |",
        "",
        "Booking được tính binary nếu có ít nhất một record `ChoXacNhan` hoặc `DaDongY`. "
        "`BaoBan` và `DaHuy` không được tính. Phân bố trạng thái trong DB: " + status_text + ".",
        "Phòng ẩn (`IsVisible = false`) không bị loại khỏi lịch sử.",
        "",
        "## 2. Công thức Score",
        "",
        "`Score = Views + Favorite * 3 + Booking * 5 + Rating`.",
        "",
        "## 3. Tổng quan",
        "",
        f"- Interaction hợp lệ trước lọc role: **{len(all_interactions):,}**",
        f"- Interaction trong dataset `NguoiThue`: **{len(interactions):,}**",
        f"- Unique users: **{len(users):,}** (DB có {diagnostics['tenant_users']:,} user mang role `NguoiThue`)",
        f"- Unique rooms có interaction: **{len(rooms):,}** / {diagnostics['total_rooms']:,}",
        f"- Interaction/user: min {user_stats['min']:.0f}, max {user_stats['max']:.0f}, mean {fmt_number(user_stats['mean'])}, median {fmt_number(user_stats['median'])}",
        f"- Tài khoản demo nhận diện bởi `nguoithue*@smartrental.local`: **{diagnostics['demo_tenant_users']:,}** / {len(users):,} users; **{diagnostics['demo_interactions']:,}** / {len(interactions):,} interactions ({demo_share * 100:.2f}%)",
        "",
        "## 4. Density và sparsity",
        "",
        f"- Possible interactions trong ma trận quan sát: {possible:,} = {len(users):,} users × {len(rooms):,} rooms",
        f"- Density: **{density:.4f}%**",
        f"- Sparsity: **{sparsity:.4f}%**",
        "",
        "## 5. Phân bố signal",
        "",
        *[f"- {key}: **{value:,}** dòng" for key, value in signal_counts.items()],
        f"- Tổng Views: **{sum(views):,}**; mean Views/dòng: **{statistics.fmean(views) if views else 0:.2f}**",
        "",
        "## 6. Phân bố interaction theo user",
        "",
        *[f"- {key}: **{value:,}** users" for key, value in distribution.items()],
        f"- User có 1-4 interaction (cold-start rõ): **{cold_users:,}** / {len(users):,}",
        "",
        "## 7. Score",
        "",
        f"- Min: **{score_stats['min']:.0f}**",
        f"- Max: **{score_stats['max']:.0f}**",
        f"- Mean: **{fmt_number(score_stats['mean'])}**",
        f"- Median: **{fmt_number(score_stats['median'])}**",
        "",
        "## 8. Phân bố room",
        "",
        f"- Phòng không có interaction trong dataset: **{zero_rooms:,}**",
        f"- Phòng có nhiều user interaction nhất: **{top_rooms[0][0] if top_rooms else 'N/A'}** ({top_rooms[0][1] if top_rooms else 0:,} users)",
        "",
        "| Rank | PhongtroId | Số user interaction |",
        "|---:|---:|---:|",
        *[f"| {index} | {room_id} | {count} |" for index, (room_id, count) in enumerate(top_rooms, 1)],
        "",
        "## 9. Validation",
        "",
        f"- Kết quả: **{validation_text}**",
        f"- Duplicate `(UserId, PhongtroId)`: **{len(interactions) - len(set((x.user_id, x.room_id) for x in interactions))}**",
        f"- Orphan source records: **{orphan_total}** (views {diagnostics['orphan_views']}, favorites {diagnostics['orphan_favorites']}, bookings {diagnostics['orphan_bookings']}, ratings {diagnostics['orphan_ratings']})",
        f"- Invalid source ranges: **{invalid_source_total}** (negative views {diagnostics['invalid_views']}, ratings ngoài 1-5 {diagnostics['invalid_ratings']})",
        "- Range yêu cầu: `Views >= 0`; `Favorite` và `Booking` thuộc `{0,1}`; `Rating` thuộc `{0..5}`; `Score > 0`.",
        *(f"- Lỗi: {error}" for error in errors),
        "",
        "## 10. Giới hạn và hướng tiếp theo",
        "",
        "- Đây là implicit-feedback heuristic, không phải nhãn đã học; các trọng số chưa được đánh giá offline.",
        "- Booking chỉ biểu diễn đã từng tạo/được đồng ý, không phân biệt số lần và chưa có trạng thái hoàn thành.",
        "- User không có signal và room chưa từng được tương tác không xuất hiện trong ma trận; runtime vẫn phải lọc phòng không visible.",
        "- Role filter đã loại interaction owner/admin khỏi dataset. Các tài khoản demo vẫn mang role tenant và dữ liệu của chúng được sinh có chủ đích để mô phỏng hành vi, nên được giữ lại; tuy nhiên chúng không phải hành vi production.",
        f"- Sau khi tách phần demo có thể nhận diện bằng email, chỉ còn **{len(interactions) - diagnostics['demo_interactions']:,}** interaction từ **{len(users) - diagnostics['demo_tenant_users']:,}** tenant khác; không đủ để đánh giá hoặc train Collaborative Filtering production.",
        f"- Đánh giá: **{recommendation}**",
        "",
        "## 11. 10 dòng đầu (UserId đã mask)",
        "",
        "```csv",
        "UserId,PhongtroId,Views,Favorite,Booking,Rating,Score",
        *[
            f"{mask_user_id(item.user_id)},{item.room_id},{item.views},{item.favorite},{item.booking},{item.rating},{item.score}"
            for item in interactions[:10]
        ],
        "```",
        "",
    ]
    return "\n".join(lines)


def main() -> int:
    args = parse_args()
    db = SqlCmd(load_connection_string(args))
    all_interactions = load_interactions(db, tenants_only=False)
    interactions = load_interactions(db, tenants_only=True)
    diagnostics, statuses = database_diagnostics(db)
    errors = validate(interactions)
    if diagnostics["invalid_views"] or diagnostics["invalid_ratings"]:
        errors.append("Source database contains an invalid signal range; export aborted.")
    if errors:
        print("Validation failed:", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    write_csv(args.output.resolve(), interactions)
    report = build_report(
        db.database,
        interactions,
        all_interactions,
        diagnostics,
        statuses,
        errors,
    )
    report_path = args.report.resolve()
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(report, encoding="utf-8", newline="\n")

    print(f"Wrote {len(interactions):,} interactions to {args.output.resolve()}")
    print(f"Wrote report to {report_path}")
    print(f"Duplicate keys: 0; validation: passed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
