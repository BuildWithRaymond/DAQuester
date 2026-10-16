"""Build a closed Shinewood Entrance tile sweep from read-only client collision.

No game assets are exported. Sprite 70 is guide metadata, not a live drop claim.
"""

import argparse
import json
import struct
from collections import deque
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("client_directory", type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    record = next(
        r for r in json.loads((root / "content/routes.json").read_text()) if r["id"] == 542
    )
    archive = (args.client_directory / "ia.dat").read_bytes()
    count = struct.unpack_from("<i", archive)[0] - 1
    sotp = None
    for i in range(count):
        offset = 4 + i * 17
        start = struct.unpack_from("<i", archive, offset)[0]
        name = archive[offset + 4 : offset + 17].split(b"\0")[0]
        end = struct.unpack_from("<i", archive, offset + 17)[0]
        if name == b"sotp.dat":
            if not 0 <= start < end <= len(archive):
                raise ValueError("Invalid collision archive bounds")
            sotp = archive[start:end]
            break
    if sotp is None:
        raise ValueError("Missing collision table")
    tiles = (args.client_directory / "maps/lod542.map").read_bytes()
    width, height = record["width"], record["height"]
    portals = {(p["from"]["X"], p["from"]["Y"]) for p in record["portals"]}


    def blocked(x, y):
        if not (0 <= x < width and 0 <= y < height) or (x, y) in portals:
            return True
        left, right = struct.unpack_from("<HH", tiles, (y * width + x) * 6 + 2)
        return any(
            sprite and (sprite > len(sotp) or sotp[sprite - 1] & 15 == 15)
            for sprite in (left, right)
        )


    start = (14, 23)
    if blocked(*start):
        raise ValueError("Recorded landing is blocked")
    reachable, queue = {start}, deque([start])
    while queue:
        x, y = queue.popleft()
        for tile in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if tile not in reachable and not blocked(*tile):
                reachable.add(tile)
                queue.append(tile)


    def path(origin, goal):
        """Connect sweep rows around walls without entering a doorway."""
        parents, queue = {origin: None}, deque([origin])
        while queue:
            tile = queue.popleft()
            if tile == goal:
                result = []
                while tile != origin:
                    result.append(tile)
                    tile = parents[tile]
                return result[::-1]
            x, y = tile
            for neighbor in ((x, y - 1), (x + 1, y), (x, y + 1), (x - 1, y)):
                if neighbor in reachable and neighbor not in parents:
                    parents[neighbor] = tile
                    queue.append(neighbor)
        raise ValueError("Sweep target is unreachable")


    # Visit actual tiles, rather than merely getting within sight of them. Alternating
    # rows keep long runs available to the existing fast movement queue.
    walk, visited = [start], {start}
    for point in sorted(reachable, key=lambda p: (p[1], p[0] if p[1] % 2 == 0 else -p[0])):
        if point not in visited:
            leg = path(walk[-1], point)
            walk.extend(leg)
            visited.update(leg)
    walk.extend(path(walk[-1], start))  # Verify and repeat the same closed lap.
    if visited != reachable or any(blocked(*tile) for tile in walk):
        raise ValueError("Sweep must cover every reachable non-door tile")


    def direction(first, second):
        return second[0] - first[0], second[1] - first[1]


    # Retain each turn and endpoint. Straight legs have a unique shortest route,
    # so navigation cannot cut across the sweep and omit intermediate tiles.
    points = [walk[0]]
    for index in range(1, len(walk) - 1):
        if direction(walk[index - 1], walk[index]) != direction(
            walk[index], walk[index + 1]
        ):
            points.append(walk[index])
    points.append(walk[-1])
    patrol = [{"X": x, "Y": y} for x, y in points]
    profile = dict(
        schemaVersion=1,
        map=542,
        source="https://vorlof.com/quests/emeralddawn.html",
        collisionSource="DATester lod542.map and ia.dat/sotp.dat, read only",
        routeSource="Normalized read-only WorldLogs 542.json; portal tiles excluded",
        sprite=70,
        spriteBasis="Guide item image images/items2/70.png; candidate, not captured ground evidence",
        status="Authored exact tile sweep; server inventory alone proves acquisition",
        coverageMode="walk-every-reachable-non-door-tile",
        reachableTiles=len(reachable),
        walkSteps=len(walk) - 1,
        patrol=patrol,
    )
    (root / "content/calibration/emerald-dawn-patrol.json").write_text(
        json.dumps(profile, indent=2) + "\n", encoding="utf-8"
    )
    recipe = dict(
        Id="emerald-dawn",
        Version=1,
        Name="Emerald Dawn",
        Source=profile["source"],
        Capabilities=["navigate", "dialog", "inventory", "legend", "speech", "ground-item"],
        CooldownHours=0,
        Repeatable=False,
        Requirements=[],
        OutcomeConditions=[
            "Fresh recovery legend with retained insignia",
            "One consumed insignia and new selected own legend, or exactly 1,000,000 new gold from the saved sale",
        ],
        Steps=[],
    )
    stages = [
        ("preflight", "preflight"),
        ("mioas", "mioas"),
        ("berg", "briefing"),
        ("gather", "gather"),
        ("recover", "recover"),
        ("druid", "druid"),
        ("deliver", "deliver"),
        ("confirm", "confirm"),
    ]
    for index, (name, action) in enumerate(stages):
        step = dict(
            Id=name,
            Action="emerald-" + action,
            Next=stages[index + 1][0] if index + 1 < len(stages) else "complete",
        )
        if name == "gather":
            step["Arguments"] = dict(patrol=json.dumps(patrol, separators=(",", ":")))
        recipe["Steps"].append(step)
    (root / "content/recipes/emerald-dawn.v1.json").write_text(
        json.dumps(recipe, indent=2) + "\n", encoding="utf-8"
    )
    print(
        f"Shinewood: {len(reachable)} reachable tiles, {len(patrol)} turn/end waypoints, {len(walk) - 1} walking steps, no door tiles."
    )


if __name__ == "__main__":
    main()
