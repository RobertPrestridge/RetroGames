var VoxelGeometry = (function () {
    'use strict';

    // Unit cube: 24 vertices (4 per face, unique normals), 36 indices
    function createCubeMesh() {
        // positions (x,y,z) + normals (nx,ny,nz) per vertex
        var positions = new Float32Array([
            // Front face (z+)
            -0.5, -0.5,  0.5,   0,  0,  1,
             0.5, -0.5,  0.5,   0,  0,  1,
             0.5,  0.5,  0.5,   0,  0,  1,
            -0.5,  0.5,  0.5,   0,  0,  1,
            // Back face (z-)
             0.5, -0.5, -0.5,   0,  0, -1,
            -0.5, -0.5, -0.5,   0,  0, -1,
            -0.5,  0.5, -0.5,   0,  0, -1,
             0.5,  0.5, -0.5,   0,  0, -1,
            // Top face (y+)
            -0.5,  0.5,  0.5,   0,  1,  0,
             0.5,  0.5,  0.5,   0,  1,  0,
             0.5,  0.5, -0.5,   0,  1,  0,
            -0.5,  0.5, -0.5,   0,  1,  0,
            // Bottom face (y-)
            -0.5, -0.5, -0.5,   0, -1,  0,
             0.5, -0.5, -0.5,   0, -1,  0,
             0.5, -0.5,  0.5,   0, -1,  0,
            -0.5, -0.5,  0.5,   0, -1,  0,
            // Right face (x+)
             0.5, -0.5,  0.5,   1,  0,  0,
             0.5, -0.5, -0.5,   1,  0,  0,
             0.5,  0.5, -0.5,   1,  0,  0,
             0.5,  0.5,  0.5,   1,  0,  0,
            // Left face (x-)
            -0.5, -0.5, -0.5,  -1,  0,  0,
            -0.5, -0.5,  0.5,  -1,  0,  0,
            -0.5,  0.5,  0.5,  -1,  0,  0,
            -0.5,  0.5, -0.5,  -1,  0,  0
        ]);

        var indices = new Uint16Array([
             0,  1,  2,   0,  2,  3,   // front
             4,  5,  6,   4,  6,  7,   // back
             8,  9, 10,   8, 10, 11,   // top
            12, 13, 14,  12, 14, 15,   // bottom
            16, 17, 18,  16, 18, 19,   // right
            20, 21, 22,  20, 22, 23    // left
        ]);

        return { positions: positions, indices: indices, vertexCount: 24, indexCount: 36 };
    }

    // Build instance data buffer from voxel array
    // Each voxel: { x, y, z, r, g, b, scale, sway }
    // Instance layout: vec3 pos, vec3 color, f32 scale, f32 sway = 8 floats
    function buildInstanceBuffer(voxels) {
        var data = new Float32Array(voxels.length * 8);
        for (var i = 0; i < voxels.length; i++) {
            var v = voxels[i];
            var off = i * 8;
            data[off]     = v.x;
            data[off + 1] = v.y;
            data[off + 2] = v.z;
            data[off + 3] = v.r;
            data[off + 4] = v.g;
            data[off + 5] = v.b;
            data[off + 6] = v.scale || 1.0;
            data[off + 7] = v.sway  || 0.0;
        }
        return data;
    }

    return {
        createCubeMesh: createCubeMesh,
        buildInstanceBuffer: buildInstanceBuffer
    };
})();
