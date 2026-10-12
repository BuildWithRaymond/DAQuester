"""Export only blocked/open tile metadata from a locally installed client.

The output contains dimensions, boolean geometry and source digests. It contains
no floor/sprite identifiers, artwork, archive entries or private source paths.
"""

import argparse
import base64
import hashlib
import json
import os
import re
import struct
from pathlib import Path


def collision_table(client_directory):
    archive = client_directory / "ia.dat"
    with archive.open("rb") as stream:
        size = archive.stat().st_size
        header = stream.read(4)
        if len(header) != 4:
            raise ValueError("Truncated collision archive directory")
        count = struct.unpack("<i", header)[0] - 1
        if not 1 <= count <= 100000:
            raise ValueError("Invalid collision archive directory")
        for index in range(count):
            stream.seek(4 + index * 17)
            entry = stream.read(21)
            if len(entry) != 21:
                raise ValueError("Truncated collision archive directory")
            start = struct.unpack_from("<i", entry)[0]
            name = entry[4:17].split(b"\0")[0]
            end = struct.unpack_from("<i", entry, 17)[0]
            if name == b"sotp.dat":
                if not 0 <= start < end <= size:
                    raise ValueError("Invalid collision table bounds")
                stream.seek(start)
                return stream.read(end - start)
    raise ValueError("Collision table absent from client archive")


def blocked_tiles(raw, width, height, sotp, map_id):
    if width <= 0 or height <= 0 or len(raw) < width * height * 6:
        raise ValueError("Invalid dimensions or truncated map " + str(map_id))
    for index in range(width * height):
        left, right = struct.unpack_from("<HH", raw, index * 6 + 2)
        blocked = any(
            sprite and (sprite > len(sotp) or sotp[sprite - 1] & 15 == 15)
            for sprite in (left, right)
        )
        # Match the existing runtime's one explicit static walkable exception.
        if map_id == 97 and index % width == 46 and index // width == 21:
            blocked = False
        yield bool(blocked)


def pack_tiles(flags):
    bits = bytearray((len(flags) + 7) // 8)
    for index, blocked in enumerate(flags):
        if blocked:
            bits[index // 8] |= 1 << (index % 8)
    return bytes(bits)


def export(client_directory, routes_path, profile, output):
    if not re.fullmatch(r"[A-Za-z0-9_.-]+", profile) or profile in (".", ".."):
        raise ValueError("Invalid server profile name")
    records = json.loads(routes_path.read_text(encoding="utf-8-sig"))
    ids = [record["id"] for record in records]
    if len(set(ids)) != len(ids):
        raise ValueError("Duplicate recorded map IDs")
    sotp = collision_table(client_directory)
    directories = [client_directory]
    if client_directory.anchor and os.environ.get("LOCALAPPDATA"):
        directories.append(
            Path(os.environ["LOCALAPPDATA"])
            / "VirtualStore"
            / str(client_directory)[len(client_directory.anchor) :]
        )
    maps, missing = [], []
    total_tiles = 0
    for record in sorted(records, key=lambda item: item["id"]):
        map_id = record["id"]
        source = next(
            (
                directory / "maps" / ("lod" + str(map_id) + ".map")
                for directory in directories
                if (directory / "maps" / ("lod" + str(map_id) + ".map")).is_file()
            ),
            None,
        )
        if source is None:
            missing.append(map_id)
            continue
        raw = source.read_bytes()
        width, height = record["width"], record["height"]
        flags = list(blocked_tiles(raw, width, height, sotp, map_id))
        bits = pack_tiles(flags)
        # Audit every exported bit against the source collision interpretation.
        if any(
            bool(bits[index // 8] & (1 << (index % 8))) != blocked
            for index, blocked in enumerate(
                blocked_tiles(raw, width, height, sotp, map_id)
            )
        ):
            raise ValueError(
                "Exported collision differs from source for map " + str(map_id)
            )
        total_tiles += len(flags)
        maps.append(
            dict(
                id=map_id,
                width=width,
                height=height,
                blockedBits=base64.b64encode(bits).decode("ascii"),
                sourceSha256=hashlib.sha256(raw).hexdigest(),
            )
        )
    pack = dict(
        schemaVersion=1,
        serverProfile=profile,
        sotpSha256=hashlib.sha256(sotp).hexdigest(),
        maps=maps,
        missingMapIds=missing,
    )
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(pack, indent=2) + "\n", encoding="utf-8")
    return dict(
        recordedMaps=len(records),
        packagedMaps=len(maps),
        missingMapIds=missing,
        auditedTiles=total_tiles,
        packedBytes=output.stat().st_size,
        sha256=hashlib.sha256(output.read_bytes()).hexdigest(),
    )


def main():
    root = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("client_directory", type=Path)
    parser.add_argument("--profile", default="datester-7.41")
    parser.add_argument("--routes", type=Path, default=root / "content/routes.json")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    output = args.output or root / "content/maps" / (args.profile + ".walkability.json")
    print(
        json.dumps(
            export(args.client_directory, args.routes, args.profile, output),
            separators=(",", ":"),
        )
    )


if __name__ == "__main__":
    main()
