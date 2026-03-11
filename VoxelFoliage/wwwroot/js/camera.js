var VoxelCamera = (function () {
    'use strict';

    function create(options) {
        var cam = {
            theta: options.theta || 0.5,
            phi: options.phi || 0.6,
            radius: options.radius || 25,
            center: options.center || [0, 4, 0],
            minRadius: 8,
            maxRadius: 60,
            minPhi: 0.1,
            maxPhi: Math.PI / 2 - 0.05,
            autoSpin: true,
            spinSpeed: 0.005,
            dragging: false,
            lastX: 0,
            lastY: 0,
            resumeTimer: null
        };
        return cam;
    }

    function getEye(cam) {
        var x = cam.center[0] + cam.radius * Math.sin(cam.phi) * Math.cos(cam.theta);
        var y = cam.center[1] + cam.radius * Math.cos(cam.phi);
        var z = cam.center[2] + cam.radius * Math.sin(cam.phi) * Math.sin(cam.theta);
        return [x, y, z];
    }

    function viewMatrix(cam) {
        var eye = getEye(cam);
        var center = cam.center;
        var up = [0, 1, 0];
        return lookAt(eye, center, up);
    }

    function projectionMatrix(aspect, fov, near, far) {
        fov = fov || Math.PI / 4;
        near = near || 0.1;
        far = far || 200;
        var f = 1.0 / Math.tan(fov / 2);
        var nf = 1.0 / (near - far);
        return new Float32Array([
            f / aspect, 0, 0, 0,
            0, f, 0, 0,
            0, 0, (far + near) * nf, -1,
            0, 0, 2 * far * near * nf, 0
        ]);
    }

    function viewProjectionMatrix(cam, aspect) {
        var v = viewMatrix(cam);
        var p = projectionMatrix(aspect);
        return multiply(p, v);
    }

    function update(cam, dt) {
        if (cam.autoSpin && !cam.dragging) {
            cam.theta += cam.spinSpeed * dt * 60;
        }
    }

    function attachControls(cam, canvas) {
        canvas.addEventListener('pointerdown', function (e) {
            cam.dragging = true;
            cam.lastX = e.clientX;
            cam.lastY = e.clientY;
            if (cam.resumeTimer) {
                clearTimeout(cam.resumeTimer);
                cam.resumeTimer = null;
            }
            canvas.setPointerCapture(e.pointerId);
        });

        canvas.addEventListener('pointermove', function (e) {
            if (!cam.dragging) return;
            var dx = e.clientX - cam.lastX;
            var dy = e.clientY - cam.lastY;
            cam.lastX = e.clientX;
            cam.lastY = e.clientY;
            cam.theta -= dx * 0.005;
            cam.phi = Math.max(cam.minPhi, Math.min(cam.maxPhi, cam.phi - dy * 0.005));
        });

        canvas.addEventListener('pointerup', function (e) {
            cam.dragging = false;
            canvas.releasePointerCapture(e.pointerId);
            if (cam.autoSpin) {
                cam.resumeTimer = setTimeout(function () {
                    cam.resumeTimer = null;
                }, 2000);
            }
        });

        canvas.addEventListener('wheel', function (e) {
            e.preventDefault();
            cam.radius = Math.max(cam.minRadius, Math.min(cam.maxRadius, cam.radius + e.deltaY * 0.05));
        }, { passive: false });
    }

    // --- Matrix math helpers ---

    function lookAt(eye, center, up) {
        var zx = eye[0] - center[0], zy = eye[1] - center[1], zz = eye[2] - center[2];
        var zLen = Math.sqrt(zx * zx + zy * zy + zz * zz);
        zx /= zLen; zy /= zLen; zz /= zLen;

        var xx = up[1] * zz - up[2] * zy;
        var xy = up[2] * zx - up[0] * zz;
        var xz = up[0] * zy - up[1] * zx;
        var xLen = Math.sqrt(xx * xx + xy * xy + xz * xz);
        xx /= xLen; xy /= xLen; xz /= xLen;

        var yx = zy * xz - zz * xy;
        var yy = zz * xx - zx * xz;
        var yz = zx * xy - zy * xx;

        return new Float32Array([
            xx, yx, zx, 0,
            xy, yy, zy, 0,
            xz, yz, zz, 0,
            -(xx * eye[0] + xy * eye[1] + xz * eye[2]),
            -(yx * eye[0] + yy * eye[1] + yz * eye[2]),
            -(zx * eye[0] + zy * eye[1] + zz * eye[2]),
            1
        ]);
    }

    function multiply(a, b) {
        var out = new Float32Array(16);
        for (var i = 0; i < 4; i++) {
            for (var j = 0; j < 4; j++) {
                out[i * 4 + j] = 0;
                for (var k = 0; k < 4; k++) {
                    out[i * 4 + j] += a[k * 4 + j] * b[i * 4 + k];
                }
            }
        }
        return out;
    }

    return {
        create: create,
        getEye: getEye,
        viewMatrix: viewMatrix,
        projectionMatrix: projectionMatrix,
        viewProjectionMatrix: viewProjectionMatrix,
        update: update,
        attachControls: attachControls
    };
})();
