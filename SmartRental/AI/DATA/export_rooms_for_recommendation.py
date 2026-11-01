"""Export the room catalogue used by content-based recommendation.

The command is read-only and follows the same sqlcmd/configuration approach as
``export_interactions.py``. Hidden rooms are intentionally retained; the
recommendation engine filters them when producing candidates.
"""

from __future__ import annotations

import argparse
import csv
import json
import os
import shutil
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[1]
DEFAULT_OUTPUT = SCRIPT_DIR / "rooms_recommendation.csv"
DELIMITER = "\x1f"


@dataclass(frozen=True)
class Room:
    room_id: int
    area: str
    price: float
    size: float
    capacity: int
    amenities: str
    is_visible: bool


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--connection-string")
    return parser.parse_args()


def connection_values(connection_string: str) -> dict[str, str]:
    result: dict[str, str] = {}
    for part in connection_string.split(";"):
        if "=" in part:
            key, value = part.split("=", 1)
            result[key.strip().lower()] = value.strip()
    return result


def load_connection_string(explicit: str | None) -> str:
    if explicit:
        return explicit
    environment_value = os.environ.get("ConnectionStrings__DefaultConnection")
    if environment_value:
        return environment_value
    for name in ("appsettings.Development.json", "appsettings.json"):
        path = PROJECT_ROOT / name
        if path.exists():
            data = json.loads(path.read_text(encoding="utf-8-sig"))
            value = data.get("ConnectionStrings", {}).get("DefaultConnection")
            if value:
                return value
    raise RuntimeError("DefaultConnection was not found in arguments, environment, or appsettings.")


class SqlCmd:
    def __init__(self, connection_string: str) -> None:
        executable = shutil.which("sqlcmd")
        if not executable:
            raise RuntimeError("sqlcmd is required but was not found on PATH.")
        values = connection_values(connection_string)
        server = values.get("server") or values.get("data source")
        database = values.get("database") or values.get("initial catalog")
        if not server or not database:
            raise RuntimeError("DefaultConnection must contain Server and Database.")
        self.command = [
            executable, "-S", server, "-d", database, "-C", "-h", "-1",
            "-W", "-w", "65535", "-s", DELIMITER,
        ]
        self.environment = os.environ.copy()
        trusted = (values.get("trusted_connection") or "").lower() in {"true", "yes", "sspi"}
        integrated = (values.get("integrated security") or "").lower() in {"true", "yes", "sspi"}
        if trusted or integrated:
            self.command.append("-E")
        else:
            user = values.get("user id") or values.get("uid")
            password = values.get("password") or values.get("pwd")
            if not user:
                raise RuntimeError("SQL authentication requires User ID.")
            self.command.extend(["-U", user])
            if password:
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
        return [line.split(DELIMITER) for line in completed.stdout.splitlines() if line.strip()]


def load_rooms(db: SqlCmd) -> list[Room]:
    query = r"""
SELECT p.Id, p.KhuVuc,
       CONVERT(varchar(40), p.Gia), CONVERT(varchar(40), p.DienTich),
       p.SoNguoiToiDa, COALESCE(a.TienNghi, N''),
       CASE WHEN p.IsVisible = 1 THEN 1 ELSE 0 END
FROM Phongtros AS p
OUTER APPLY
(
    SELECT STRING_AGG(x.TenTienNghi, N'|') AS TienNghi
    FROM
    (
        SELECT DISTINCT REPLACE(t.TenTienNghi, N'|', N'/') AS TenTienNghi
        FROM PhongTienNghis AS pt
        INNER JOIN TienNghis AS t ON t.Id = pt.TienNghiId
        WHERE pt.PhongtroId = p.Id
    ) AS x
) AS a
ORDER BY p.Id;
"""
    rooms: list[Room] = []
    for row in db.rows(query):
        if len(row) != 7:
            raise RuntimeError(f"Unexpected room row returned by SQL: {row!r}")
        rooms.append(
            Room(
                room_id=int(row[0]),
                area=row[1].strip(),
                price=float(row[2]),
                size=float(row[3]),
                capacity=int(row[4]),
                amenities=row[5].strip(),
                is_visible=row[6] == "1",
            )
        )
    return rooms


def validate(rooms: list[Room]) -> list[str]:
    errors: list[str] = []
    ids = [room.room_id for room in rooms]
    if len(ids) != len(set(ids)):
        errors.append(f"duplicate PhongtroId: {len(ids) - len(set(ids))}")
    for room in rooms:
        if not room.area:
            errors.append(f"room {room.room_id}: KhuVuc is empty")
        if room.price < 0 or room.size < 0 or room.capacity < 1:
            errors.append(f"room {room.room_id}: invalid numeric feature")
    return errors


def write_csv(path: Path, rooms: list[Room]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerow(["PhongtroId", "KhuVuc", "Gia", "DienTich", "SoNguoiToiDa", "TienNghi", "IsVisible"])
        for room in rooms:
            writer.writerow([
                room.room_id, room.area, f"{room.price:.2f}", f"{room.size:g}",
                room.capacity, room.amenities, int(room.is_visible),
            ])


def main() -> int:
    args = parse_args()
    rooms = load_rooms(SqlCmd(load_connection_string(args.connection_string)))
    errors = validate(rooms)
    if errors:
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1
    write_csv(args.output.resolve(), rooms)
    visible = sum(room.is_visible for room in rooms)
    print(f"Wrote {len(rooms):,} rooms ({visible:,} visible) to {args.output.resolve()}")
    print("Duplicate rooms: 0; orphan amenities are excluded by the audited INNER JOIN")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
