(function () {
    'use strict';

    var canvas = document.getElementById('foliageCanvas');
    var fallback = document.getElementById('webgpuFallback');
    var controls = document.getElementById('foliageControls');
    var spinToggle = document.getElementById('spinToggle');
    var speedSlider = document.getElementById('speedSlider');

    // WGSL shaders
    var shaderCode = /* wgsl */`
        struct Uniforms {
            viewProjection: mat4x4f,
            lightDir: vec3f,
            time: f32,
            eyePos: vec3f,
            _pad: f32,
        }

        @group(0) @binding(0) var<uniform> u: Uniforms;

        struct VertexInput {
            @location(0) position: vec3f,
            @location(1) normal: vec3f,
            // Instance attributes
            @location(2) instPos: vec3f,
            @location(3) instColor: vec3f,
            @location(4) instScale: f32,
            @location(5) instSway: f32,
        }

        struct VertexOutput {
            @builtin(position) clipPos: vec4f,
            @location(0) color: vec3f,
            @location(1) normal: vec3f,
            @location(2) worldPos: vec3f,
        }

        @vertex fn vs_main(v: VertexInput) -> VertexOutput {
            var worldPos = v.position * v.instScale + v.instPos;

            // Wind sway for foliage
            let swayX = sin(u.time * 1.8 + worldPos.z * 0.4 + worldPos.x * 0.3) * v.instSway * 0.18;
            let swayZ = cos(u.time * 1.3 + worldPos.x * 0.5) * v.instSway * 0.08;
            worldPos.x += swayX;
            worldPos.z += swayZ;

            var out: VertexOutput;
            out.clipPos = u.viewProjection * vec4f(worldPos, 1.0);
            out.color = v.instColor;
            out.normal = v.normal;
            out.worldPos = worldPos;
            return out;
        }

        @fragment fn fs_main(f: VertexOutput) -> @location(0) vec4f {
            let n = normalize(f.normal);
            let l = normalize(u.lightDir);
            let ndotl = max(dot(n, l), 0.0);

            // Ambient + diffuse
            let ambient = 0.35;
            let diffuse = 0.65 * ndotl;
            var lit = f.color * (ambient + diffuse);

            // Subtle distance fog
            let dist = distance(f.worldPos, u.eyePos);
            let fogStart = 30.0;
            let fogEnd = 80.0;
            let fogColor = vec3f(0.04, 0.04, 0.10);
            let fogFactor = clamp((dist - fogStart) / (fogEnd - fogStart), 0.0, 1.0);
            lit = mix(lit, fogColor, fogFactor);

            return vec4f(lit, 1.0);
        }
    `;

    async function init() {
        // Check WebGPU support
        if (!navigator.gpu) {
            canvas.style.display = 'none';
            controls.style.display = 'none';
            fallback.style.display = 'block';
            return;
        }

        var adapter = await navigator.gpu.requestAdapter();
        if (!adapter) {
            canvas.style.display = 'none';
            controls.style.display = 'none';
            fallback.style.display = 'block';
            return;
        }

        var device = await adapter.requestDevice();
        var context = canvas.getContext('webgpu');
        var format = navigator.gpu.getPreferredCanvasFormat();

        context.configure({
            device: device,
            format: format,
            alphaMode: 'opaque'
        });

        // Resize canvas to pixel size
        function resize() {
            var dpr = window.devicePixelRatio || 1;
            canvas.width = Math.floor(canvas.clientWidth * dpr);
            canvas.height = Math.floor(canvas.clientHeight * dpr);
        }
        resize();
        window.addEventListener('resize', resize);

        // Generate scene
        var voxels = FoliageGenerator.generateScene(42);
        var cubeMesh = VoxelGeometry.createCubeMesh();
        var instanceData = VoxelGeometry.buildInstanceBuffer(voxels);

        // Vertex buffer (cube mesh)
        var vertexBuffer = device.createBuffer({
            size: cubeMesh.positions.byteLength,
            usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST
        });
        device.queue.writeBuffer(vertexBuffer, 0, cubeMesh.positions);

        // Index buffer
        var indexBuffer = device.createBuffer({
            size: cubeMesh.indices.byteLength,
            usage: GPUBufferUsage.INDEX | GPUBufferUsage.COPY_DST
        });
        device.queue.writeBuffer(indexBuffer, 0, cubeMesh.indices);

        // Instance buffer
        var instanceBuffer = device.createBuffer({
            size: instanceData.byteLength,
            usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST
        });
        device.queue.writeBuffer(instanceBuffer, 0, instanceData);

        // Uniform buffer: mat4 (64) + vec3 lightDir (12) + f32 time (4) + vec3 eyePos (12) + f32 pad (4) = 96 bytes
        var uniformBuffer = device.createBuffer({
            size: 96,
            usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST
        });

        // Bind group layout + bind group
        var bindGroupLayout = device.createBindGroupLayout({
            entries: [{
                binding: 0,
                visibility: GPUShaderStage.VERTEX | GPUShaderStage.FRAGMENT,
                buffer: { type: 'uniform' }
            }]
        });

        var bindGroup = device.createBindGroup({
            layout: bindGroupLayout,
            entries: [{ binding: 0, resource: { buffer: uniformBuffer } }]
        });

        // Shader module
        var shaderModule = device.createShaderModule({ code: shaderCode });

        // Pipeline
        var pipeline = device.createRenderPipeline({
            layout: device.createPipelineLayout({ bindGroupLayouts: [bindGroupLayout] }),
            vertex: {
                module: shaderModule,
                entryPoint: 'vs_main',
                buffers: [
                    // Cube vertex: position (vec3f) + normal (vec3f) = stride 24
                    {
                        arrayStride: 24,
                        stepMode: 'vertex',
                        attributes: [
                            { shaderLocation: 0, offset: 0, format: 'float32x3' },  // position
                            { shaderLocation: 1, offset: 12, format: 'float32x3' }  // normal
                        ]
                    },
                    // Instance: pos(3) + color(3) + scale(1) + sway(1) = stride 32
                    {
                        arrayStride: 32,
                        stepMode: 'instance',
                        attributes: [
                            { shaderLocation: 2, offset: 0, format: 'float32x3' },   // instPos
                            { shaderLocation: 3, offset: 12, format: 'float32x3' },  // instColor
                            { shaderLocation: 4, offset: 24, format: 'float32' },     // instScale
                            { shaderLocation: 5, offset: 28, format: 'float32' }      // instSway
                        ]
                    }
                ]
            },
            fragment: {
                module: shaderModule,
                entryPoint: 'fs_main',
                targets: [{ format: format }]
            },
            primitive: {
                topology: 'triangle-list',
                cullMode: 'back',
                frontFace: 'ccw'
            },
            depthStencil: {
                format: 'depth24plus',
                depthWriteEnabled: true,
                depthCompare: 'less'
            }
        });

        // Depth texture
        var depthTexture = null;
        function createDepthTexture() {
            if (depthTexture) depthTexture.destroy();
            depthTexture = device.createTexture({
                size: [canvas.width, canvas.height],
                format: 'depth24plus',
                usage: GPUTextureUsage.RENDER_ATTACHMENT
            });
        }
        createDepthTexture();

        // Camera
        var camera = VoxelCamera.create({
            theta: 0.5,
            phi: 0.55,
            radius: 30,
            center: [0, 4, 0]
        });
        VoxelCamera.attachControls(camera, canvas);

        // UI controls
        spinToggle.addEventListener('click', function () {
            camera.autoSpin = !camera.autoSpin;
            spinToggle.classList.toggle('active', camera.autoSpin);
        });

        speedSlider.addEventListener('input', function () {
            camera.spinSpeed = parseFloat(speedSlider.value) * 0.002;
        });
        camera.spinSpeed = parseFloat(speedSlider.value) * 0.002;

        // Uniform data
        var uniformData = new Float32Array(24); // 96 bytes / 4
        // Light direction (normalized, from upper-right)
        var lx = 0.4, ly = 0.8, lz = 0.3;
        var lLen = Math.sqrt(lx * lx + ly * ly + lz * lz);
        uniformData[16] = lx / lLen;
        uniformData[17] = ly / lLen;
        uniformData[18] = lz / lLen;

        var lastTime = performance.now();
        var totalTime = 0;
        var lastWidth = canvas.width;
        var lastHeight = canvas.height;

        function frame() {
            var now = performance.now();
            var dt = (now - lastTime) / 1000;
            lastTime = now;
            totalTime += dt;

            // Rebuild depth texture on resize
            if (canvas.width !== lastWidth || canvas.height !== lastHeight) {
                lastWidth = canvas.width;
                lastHeight = canvas.height;
                createDepthTexture();
            }

            if (canvas.width === 0 || canvas.height === 0) {
                requestAnimationFrame(frame);
                return;
            }

            // Update camera
            VoxelCamera.update(camera, dt);

            // Build view-projection matrix
            var aspect = canvas.width / canvas.height;
            var vp = VoxelCamera.viewProjectionMatrix(camera, aspect);
            uniformData.set(vp, 0); // mat4 at offset 0

            // Time
            uniformData[19] = totalTime;

            // Eye position
            var eye = VoxelCamera.getEye(camera);
            uniformData[20] = eye[0];
            uniformData[21] = eye[1];
            uniformData[22] = eye[2];

            device.queue.writeBuffer(uniformBuffer, 0, uniformData);

            var commandEncoder = device.createCommandEncoder();
            var textureView = context.getCurrentTexture().createView();

            var renderPass = commandEncoder.beginRenderPass({
                colorAttachments: [{
                    view: textureView,
                    clearValue: { r: 0.04, g: 0.04, b: 0.10, a: 1.0 },
                    loadOp: 'clear',
                    storeOp: 'store'
                }],
                depthStencilAttachment: {
                    view: depthTexture.createView(),
                    depthClearValue: 1.0,
                    depthLoadOp: 'clear',
                    depthStoreOp: 'store'
                }
            });

            renderPass.setPipeline(pipeline);
            renderPass.setVertexBuffer(0, vertexBuffer);
            renderPass.setVertexBuffer(1, instanceBuffer);
            renderPass.setIndexBuffer(indexBuffer, 'uint16');
            renderPass.setBindGroup(0, bindGroup);
            renderPass.drawIndexed(cubeMesh.indexCount, voxels.length);
            renderPass.end();

            device.queue.submit([commandEncoder.finish()]);
            requestAnimationFrame(frame);
        }

        requestAnimationFrame(frame);
    }

    init();
})();
