"""Detect Vorlof's red circles and register the three authored patrol areas.

Run against a locally downloaded reference image. These are candidate areas,
not guaranteed drop tiles; the runner validates them against server inventory.
"""

import json
import sys
from collections import deque
from pathlib import Path
from PIL import Image


def main():
    image = Image.open(sys.argv[1]).convert("RGB")
    red = {
        (x, y)
        for y in range(image.height)
        for x in range(image.width)
        if (lambda c: c[0] > 170 and c[1] < 95 and c[2] < 95)(image.getpixel((x, y)))
    }
    components = []
    while red:
        seed = red.pop()
        points = [seed]
        queue = deque([seed])
        while queue:
            x, y = queue.popleft()
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    q = x + dx, y + dy
                    if q in red:
                        red.remove(q)
                        points.append(q)
                        queue.append(q)
        if len(points) > 25:
            components.append(points)
    components.sort(key=lambda p: min(x for x, y in p))
    if len(components) != 3:
        raise ValueError(f"Expected three marked regions, got {len(components)}")
    # The western guide circle is beside Armor Shop (entrance 18,62), not the
    # Tutorial Book's 40,40 location. Sweep its adjacent collision-filtered park.
    # Book coordinates and live drops still corroborate the central/east areas.
    world_centers = ((18, 56), (54, 43), (64, 44))
    regions = []
    for name, points, (x, y), radius in zip(
        ("west", "central", "east"), components, world_centers, (4, 3, 3)
    ):
        sx = (min(x for x, y in points) + max(x for x, y in points)) / 2
        sy = (min(y for x, y in points) + max(y for x, y in points)) / 2
        regions.append(
            dict(
                id=name,
                center=dict(x=x, y=y),
                radius=radius,
                imageCenter=dict(x=sx, y=sy),
                basis="Armor-shop guide area for west; Tutorial Book/live areas for central/east; server inventory proves collection",
            )
        )
    result = dict(
        schemaVersion=1,
        map=500,
        width=100,
        height=100,
        source="https://vorlof.com/images/quests/spareasticktrees.png",
        collisionSource="DATester maps/lod500.map and ia.dat/sotp.dat",
        coordinateSource="Armor Shop portal (18,62) and client collision park for west; https://darkages.fandom.com/wiki/Tutorial_Book for central/east",
        status="player-confirmed three-area rotation",
        regions=regions,
    )
    Path(sys.argv[2]).write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(regions))


if __name__ == "__main__":
    main()
