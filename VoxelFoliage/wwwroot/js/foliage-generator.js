var FoliageGenerator = (function () {
    'use strict';

    // Seeded random for reproducibility
    function mulberry32(seed) {
        return function () {
            seed |= 0; seed = seed + 0x6D2B79F5 | 0;
            var t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
            t = t + Math.imul(t ^ (t >>> 7), 61 | t) ^ t;
            return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
        };
    }

    // Color palettes
    var TRUNK_COLORS = [
        { r: 0.35, g: 0.22, b: 0.12 },
        { r: 0.40, g: 0.25, b: 0.14 },
        { r: 0.30, g: 0.18, b: 0.10 }
    ];

    var LEAF_COLORS = [
        { r: 0.15, g: 0.55, b: 0.12 },
        { r: 0.20, g: 0.65, b: 0.15 },
        { r: 0.12, g: 0.45, b: 0.10 },
        { r: 0.25, g: 0.70, b: 0.18 },
        { r: 0.18, g: 0.50, b: 0.08 },
        { r: 0.30, g: 0.75, b: 0.22 }
    ];

    var GRASS_COLORS = [
        { r: 0.10, g: 0.30, b: 0.08 },
        { r: 0.12, g: 0.35, b: 0.10 },
        { r: 0.08, g: 0.25, b: 0.06 },
        { r: 0.14, g: 0.38, b: 0.12 }
    ];

    var FLOWER_COLORS = [
        { r: 0.90, g: 0.25, b: 0.40 },
        { r: 0.95, g: 0.85, b: 0.20 },
        { r: 0.70, g: 0.30, b: 0.80 },
        { r: 0.95, g: 0.55, b: 0.20 }
    ];

    function pick(arr, rng) {
        return arr[Math.floor(rng() * arr.length)];
    }

    function generateTree(cx, cz, rng, sizeScale) {
        var voxels = [];
        sizeScale = sizeScale || 1.0;

        // Trunk
        var trunkHeight = Math.floor((4 + rng() * 4) * sizeScale);
        var trunkBase = Math.max(1, Math.floor(1.5 * sizeScale));

        for (var y = 0; y < trunkHeight; y++) {
            var width = Math.max(1, Math.floor(trunkBase * (1 - y / trunkHeight * 0.6)));
            var halfW = Math.floor(width / 2);
            for (var dx = -halfW; dx <= halfW; dx++) {
                for (var dz = -halfW; dz <= halfW; dz++) {
                    var col = pick(TRUNK_COLORS, rng);
                    voxels.push({ x: cx + dx, y: y + 0.5, z: cz + dz, r: col.r, g: col.g, b: col.b, scale: 1.0, sway: 0.0 });
                }
            }
        }

        // Canopy — spherical cluster(s)
        var canopyRadius = Math.floor((2.5 + rng() * 2) * sizeScale);
        var canopyCenterY = trunkHeight + canopyRadius * 0.4;
        var numClusters = 1 + Math.floor(rng() * 2);

        for (var c = 0; c < numClusters; c++) {
            var offX = c === 0 ? 0 : (rng() - 0.5) * canopyRadius;
            var offZ = c === 0 ? 0 : (rng() - 0.5) * canopyRadius;
            var offY = c === 0 ? 0 : (rng() - 0.5) * canopyRadius * 0.5;
            var r = canopyRadius * (c === 0 ? 1.0 : 0.6 + rng() * 0.3);

            for (var lx = -Math.ceil(r); lx <= Math.ceil(r); lx++) {
                for (var ly = -Math.ceil(r); ly <= Math.ceil(r); ly++) {
                    for (var lz = -Math.ceil(r); lz <= Math.ceil(r); lz++) {
                        var dist = Math.sqrt(lx * lx + ly * ly + lz * lz);
                        if (dist <= r && (dist <= r - 0.8 || rng() > 0.3)) {
                            var col = pick(LEAF_COLORS, rng);
                            var swayAmount = 0.3 + rng() * 0.7;
                            voxels.push({
                                x: cx + lx + offX,
                                y: canopyCenterY + ly + offY + 0.5,
                                z: cz + lz + offZ,
                                r: col.r, g: col.g, b: col.b,
                                scale: 1.0,
                                sway: swayAmount
                            });
                        }
                    }
                }
            }
        }

        return voxels;
    }

    function generateBush(cx, cz, rng) {
        var voxels = [];
        var r = 1 + Math.floor(rng() * 2);

        for (var lx = -r; lx <= r; lx++) {
            for (var ly = 0; ly <= r; ly++) {
                for (var lz = -r; lz <= r; lz++) {
                    var dist = Math.sqrt(lx * lx + ly * ly + lz * lz);
                    if (dist <= r + 0.3 && rng() > 0.2) {
                        var col = pick(LEAF_COLORS, rng);
                        voxels.push({
                            x: cx + lx, y: ly + 0.5, z: cz + lz,
                            r: col.r, g: col.g, b: col.b,
                            scale: 1.0, sway: 0.2 + rng() * 0.4
                        });
                    }
                }
            }
        }

        // Occasional flower on top
        if (rng() > 0.5) {
            var col = pick(FLOWER_COLORS, rng);
            voxels.push({
                x: cx, y: r + 1.5, z: cz,
                r: col.r, g: col.g, b: col.b,
                scale: 0.7, sway: 0.8
            });
        }

        return voxels;
    }

    function generateGround(halfSize, rng) {
        var voxels = [];
        for (var gx = -halfSize; gx <= halfSize; gx++) {
            for (var gz = -halfSize; gz <= halfSize; gz++) {
                // Circular ground
                if (gx * gx + gz * gz > (halfSize + 2) * (halfSize + 2)) continue;
                var col = pick(GRASS_COLORS, rng);
                voxels.push({
                    x: gx, y: -0.5, z: gz,
                    r: col.r, g: col.g, b: col.b,
                    scale: 1.0, sway: 0.0
                });
            }
        }
        return voxels;
    }

    function generateScene(seed) {
        seed = seed || 42;
        var rng = mulberry32(seed);
        var voxels = [];

        // Ground
        var groundSize = 12;
        voxels = voxels.concat(generateGround(groundSize, rng));

        // Main center tree (large)
        voxels = voxels.concat(generateTree(0, 0, rng, 1.3));

        // Surrounding trees
        var treePositions = [
            { x: -7, z: -5 }, { x: 6, z: -6 }, { x: -5, z: 7 },
            { x: 8, z: 4 }, { x: -9, z: 1 }, { x: 3, z: 8 },
            { x: -3, z: -9 }
        ];

        for (var t = 0; t < treePositions.length; t++) {
            var size = 0.6 + rng() * 0.7;
            voxels = voxels.concat(generateTree(treePositions[t].x, treePositions[t].z, rng, size));
        }

        // Bushes
        var bushCount = 5 + Math.floor(rng() * 5);
        for (var b = 0; b < bushCount; b++) {
            var bx = Math.floor((rng() - 0.5) * groundSize * 2);
            var bz = Math.floor((rng() - 0.5) * groundSize * 2);
            if (bx * bx + bz * bz < groundSize * groundSize) {
                voxels = voxels.concat(generateBush(bx, bz, rng));
            }
        }

        // Scattered flowers
        var flowerCount = 8 + Math.floor(rng() * 8);
        for (var f = 0; f < flowerCount; f++) {
            var fx = (rng() - 0.5) * groundSize * 2;
            var fz = (rng() - 0.5) * groundSize * 2;
            if (fx * fx + fz * fz < groundSize * groundSize) {
                var col = pick(FLOWER_COLORS, rng);
                voxels.push({
                    x: fx, y: 0.2, z: fz,
                    r: col.r, g: col.g, b: col.b,
                    scale: 0.4 + rng() * 0.3,
                    sway: 0.5 + rng() * 0.5
                });
            }
        }

        return voxels;
    }

    return {
        generateScene: generateScene
    };
})();
