(function () {
    'use strict';

    var config = window.asteroidsConfig;
    if (!config) return;

    // --- Constants ---
    var ARENA_WIDTH = 1200;
    var ARENA_HEIGHT = 800;
    var P1_COLOR = '#00ff41';
    var P1_GLOW = 'rgba(0,255,65,0.6)';
    var P2_COLOR = '#ff8800';
    var P2_GLOW = 'rgba(255,136,0,0.6)';
    var ASTEROID_COLOR = '#aaaaaa';
    var ASTEROID_GLOW = 'rgba(170,170,170,0.4)';
    var BULLET_GLOW_RADIUS = 6;
    var BG_COLOR = '#0a0a1a';
    var SHIP_SIZE = 15;

    // Pre-generated asteroid shapes (unit-circle vertex offsets)
    var ASTEROID_SHAPES = [];
    (function () {
        var rng = function (seed) {
            // Simple seeded pseudo-random
            var s = seed;
            return function () {
                s = (s * 16807 + 0) % 2147483647;
                return s / 2147483647;
            };
        };
        for (var v = 0; v < 5; v++) {
            var rand = rng(v * 1000 + 42);
            var verts = [];
            var numVerts = 8 + Math.floor(rand() * 4);
            for (var i = 0; i < numVerts; i++) {
                var angle = (i / numVerts) * Math.PI * 2;
                var r = 0.7 + rand() * 0.3;
                verts.push({ x: Math.cos(angle) * r, y: Math.sin(angle) * r });
            }
            ASTEROID_SHAPES.push(verts);
        }
    })();

    // --- DOM ---
    var canvas = document.getElementById('asteroidsCanvas');
    var ctx = canvas.getContext('2d');
    var countdownOverlay = document.getElementById('countdownOverlay');
    var countdownText = document.getElementById('countdownText');
    var gameOverOverlay = document.getElementById('gameOverOverlay');
    var gameOverText = document.getElementById('gameOverText');
    var gameOverScores = document.getElementById('gameOverScores');
    var waitingOverlay = document.getElementById('waitingOverlay');
    var shareLink = document.getElementById('shareLink');
    var copyBtn = document.getElementById('copyBtn');
    var newGameBtn = document.getElementById('newGameBtn');
    var p1NameEl = document.getElementById('p1Name');
    var p2NameEl = document.getElementById('p2Name');
    var p1ScoreEl = document.getElementById('p1Score');
    var p2ScoreEl = document.getElementById('p2Score');
    var p1LivesEl = document.getElementById('p1Lives');
    var p2LivesEl = document.getElementById('p2Lives');
    var waveTextEl = document.getElementById('waveText');
    var gameStatusEl = document.getElementById('gameStatus');
    var p1NukesEl = document.getElementById('p1Nukes');
    var p2NukesEl = document.getElementById('p2Nukes');

    // --- State ---
    var p1 = null;
    var p2 = null;
    var asteroids = [];
    var bullets = [];
    var particles = [];
    var playerNumber = 0;
    var gameStatus = -1;
    var p1Score = 0, p2Score = 0;
    var p1Lives = 0, p2Lives = 0;
    var wave = 0;
    var animFrameId = null;
    var nukeAnimations = [];
    var p1Nukes = 0, p2Nukes = 0;
    var screenShake = 0;

    // --- Canvas setup ---
    canvas.width = ARENA_WIDTH;
    canvas.height = ARENA_HEIGHT;

    // --- Input ---
    var inputState = { thrust: false, rotateLeft: false, rotateRight: false, fire: false, nuke: false };
    var inputDirty = false;

    document.addEventListener('keydown', function (e) {
        var changed = false;
        switch (e.key) {
            case 'ArrowUp': case 'w': case 'W':
                if (!inputState.thrust) { inputState.thrust = true; changed = true; } break;
            case 'ArrowLeft': case 'a': case 'A':
                if (!inputState.rotateLeft) { inputState.rotateLeft = true; changed = true; } break;
            case 'ArrowRight': case 'd': case 'D':
                if (!inputState.rotateRight) { inputState.rotateRight = true; changed = true; } break;
            case ' ':
                if (!inputState.fire) { inputState.fire = true; changed = true; } break;
            case 'n': case 'N':
                if (!inputState.nuke) { inputState.nuke = true; changed = true; } break;
        }
        if (changed) {
            e.preventDefault();
            inputDirty = true;
        }
    });

    document.addEventListener('keyup', function (e) {
        var changed = false;
        switch (e.key) {
            case 'ArrowUp': case 'w': case 'W':
                if (inputState.thrust) { inputState.thrust = false; changed = true; } break;
            case 'ArrowLeft': case 'a': case 'A':
                if (inputState.rotateLeft) { inputState.rotateLeft = false; changed = true; } break;
            case 'ArrowRight': case 'd': case 'D':
                if (inputState.rotateRight) { inputState.rotateRight = false; changed = true; } break;
            case ' ':
                if (inputState.fire) { inputState.fire = false; changed = true; } break;
            case 'n': case 'N':
                if (inputState.nuke) { inputState.nuke = false; changed = true; } break;
        }
        if (changed) {
            e.preventDefault();
            inputDirty = true;
        }
    });

    // Send input at a fixed rate
    setInterval(function () {
        if (inputDirty && connection && connection.state === signalR.HubConnectionState.Connected) {
            connection.invoke('SendInput', config.shortCode, inputState).catch(function () { });
            inputDirty = false;
        }
    }, 50);

    // --- Rendering ---
    function render() {
        ctx.save();

        // Screen shake
        if (screenShake > 0) {
            var shakeIntensity = screenShake * 0.5;
            var shakeX = (Math.random() - 0.5) * shakeIntensity;
            var shakeY = (Math.random() - 0.5) * shakeIntensity;
            ctx.translate(shakeX, shakeY);
        }

        ctx.fillStyle = BG_COLOR;
        ctx.fillRect(0, 0, ARENA_WIDTH, ARENA_HEIGHT);

        // Stars background
        ctx.fillStyle = '#222';
        for (var i = 0; i < 80; i++) {
            var sx = (i * 7919 + 13) % ARENA_WIDTH;
            var sy = (i * 6271 + 37) % ARENA_HEIGHT;
            ctx.fillRect(sx, sy, 1, 1);
        }

        // Draw asteroids
        drawAsteroids();

        // Draw bullets
        drawBullets();

        // Draw particles
        drawParticles();

        // Draw ships
        if (p1 && p1.alive) drawShip(p1, P1_COLOR, P1_GLOW);
        if (p1 && p1.invulnerable && p1.alive) {
            // Blink when invulnerable
            if (Math.floor(Date.now() / 100) % 2 === 0) {
                ctx.globalAlpha = 0.3;
                drawShip(p1, P1_COLOR, P1_GLOW);
                ctx.globalAlpha = 1.0;
            }
        }
        if (p2 && p2.alive) drawShip(p2, P2_COLOR, P2_GLOW);
        if (p2 && p2.invulnerable && p2.alive) {
            if (Math.floor(Date.now() / 100) % 2 === 0) {
                ctx.globalAlpha = 0.3;
                drawShip(p2, P2_COLOR, P2_GLOW);
                ctx.globalAlpha = 1.0;
            }
        }

        // Draw nuke explosions (on top of everything)
        drawNukeAnimations();

        // Update particles and nuke animations
        updateParticles();
        updateNukeAnimations();

        ctx.restore();

        animFrameId = requestAnimationFrame(render);
    }

    function drawShip(ship, color, glow) {
        ctx.save();
        ctx.translate(ship.x, ship.y);
        ctx.rotate(ship.rotation);

        ctx.strokeStyle = color;
        ctx.lineWidth = 2;
        ctx.shadowColor = glow;
        ctx.shadowBlur = 10;

        // Ship triangle
        ctx.beginPath();
        ctx.moveTo(SHIP_SIZE, 0);
        ctx.lineTo(-SHIP_SIZE * 0.7, -SHIP_SIZE * 0.6);
        ctx.lineTo(-SHIP_SIZE * 0.4, 0);
        ctx.lineTo(-SHIP_SIZE * 0.7, SHIP_SIZE * 0.6);
        ctx.closePath();
        ctx.stroke();

        // Thrust flame
        if (ship.thrusting) {
            ctx.strokeStyle = '#ffaa00';
            ctx.shadowColor = 'rgba(255,170,0,0.6)';
            ctx.shadowBlur = 15;
            ctx.beginPath();
            ctx.moveTo(-SHIP_SIZE * 0.5, -SHIP_SIZE * 0.25);
            ctx.lineTo(-SHIP_SIZE * (0.8 + Math.random() * 0.4), 0);
            ctx.lineTo(-SHIP_SIZE * 0.5, SHIP_SIZE * 0.25);
            ctx.stroke();
        }

        ctx.shadowBlur = 0;
        ctx.restore();
    }

    function drawAsteroids() {
        ctx.strokeStyle = ASTEROID_COLOR;
        ctx.lineWidth = 1.5;
        ctx.shadowColor = ASTEROID_GLOW;
        ctx.shadowBlur = 5;

        for (var i = 0; i < asteroids.length; i++) {
            var a = asteroids[i];
            var radius = getSizeRadius(a.size);
            var shape = ASTEROID_SHAPES[a.shapeVariant % ASTEROID_SHAPES.length];

            ctx.save();
            ctx.translate(a.x, a.y);
            ctx.rotate(a.rotation);

            ctx.beginPath();
            ctx.moveTo(shape[0].x * radius, shape[0].y * radius);
            for (var j = 1; j < shape.length; j++) {
                ctx.lineTo(shape[j].x * radius, shape[j].y * radius);
            }
            ctx.closePath();
            ctx.stroke();

            ctx.restore();
        }
        ctx.shadowBlur = 0;
    }

    function drawBullets() {
        for (var i = 0; i < bullets.length; i++) {
            var b = bullets[i];
            var color = b.owner === 1 ? P1_COLOR : P2_COLOR;
            var glow = b.owner === 1 ? P1_GLOW : P2_GLOW;

            ctx.fillStyle = color;
            ctx.shadowColor = glow;
            ctx.shadowBlur = BULLET_GLOW_RADIUS;

            ctx.beginPath();
            ctx.arc(b.x, b.y, 2, 0, Math.PI * 2);
            ctx.fill();
        }
        ctx.shadowBlur = 0;
    }

    function drawParticles() {
        for (var i = 0; i < particles.length; i++) {
            var p = particles[i];
            var alpha = p.life / p.maxLife;
            ctx.strokeStyle = p.color;
            ctx.globalAlpha = alpha;
            ctx.lineWidth = 1;

            ctx.beginPath();
            ctx.moveTo(p.x, p.y);
            ctx.lineTo(p.x - p.vx * 2, p.y - p.vy * 2);
            ctx.stroke();
        }
        ctx.globalAlpha = 1.0;
    }

    function updateParticles() {
        for (var i = particles.length - 1; i >= 0; i--) {
            var p = particles[i];
            p.x += p.vx;
            p.y += p.vy;
            p.life--;
            if (p.life <= 0) {
                particles.splice(i, 1);
            }
        }
    }

    function spawnExplosion(x, y, size, color) {
        var count = size === 'large' ? 15 : size === 'medium' ? 10 : size === 'ship' ? 20 : 6;
        var speed = size === 'large' ? 3 : size === 'medium' ? 2.5 : 2;
        var c = color || ASTEROID_COLOR;
        for (var i = 0; i < count; i++) {
            var angle = Math.random() * Math.PI * 2;
            var spd = (0.5 + Math.random() * speed);
            particles.push({
                x: x,
                y: y,
                vx: Math.cos(angle) * spd,
                vy: Math.sin(angle) * spd,
                life: 20 + Math.floor(Math.random() * 20),
                maxLife: 40,
                color: c
            });
        }
    }

    function spawnNukeExplosion(x, y) {
        // Clamp position so mushroom cloud stays visible
        var cx = Math.max(150, Math.min(x, ARENA_WIDTH - 150));
        var cy = Math.max(200, Math.min(y, ARENA_HEIGHT - 100));

        nukeAnimations.push({
            x: cx,
            y: cy,
            frame: 0,
            maxFrames: 180,
            // Mushroom cloud particles (persistent cloud puffs)
            cloudPuffs: generateCloudPuffs(cx, cy),
            // Ring shockwave
            ringRadius: 0,
            // Debris
            debris: generateNukeDebris(cx, cy),
            // Stem particles
            stemPuffs: generateStemPuffs(cx, cy)
        });

        // Screen shake
        screenShake = 30;

        // Also spawn a burst of regular particles for extra flair
        for (var i = 0; i < 60; i++) {
            var angle = Math.random() * Math.PI * 2;
            var spd = 2 + Math.random() * 6;
            particles.push({
                x: cx,
                y: cy,
                vx: Math.cos(angle) * spd,
                vy: Math.sin(angle) * spd,
                life: 30 + Math.floor(Math.random() * 40),
                maxLife: 70,
                color: ['#ff4400', '#ff8800', '#ffcc00', '#ffffff'][Math.floor(Math.random() * 4)]
            });
        }
    }

    function generateCloudPuffs(cx, cy) {
        var puffs = [];
        // Main mushroom cap — cluster of overlapping circles
        for (var i = 0; i < 18; i++) {
            var angle = (i / 18) * Math.PI * 2 + (Math.random() - 0.5) * 0.3;
            var dist = 20 + Math.random() * 40;
            puffs.push({
                ox: Math.cos(angle) * dist,
                oy: -120 + Math.sin(angle) * dist * 0.5,
                radius: 25 + Math.random() * 30,
                delay: Math.floor(Math.random() * 20),
                color: Math.random() > 0.3 ? 'fire' : 'smoke'
            });
        }
        // Inner bright core at top
        for (var j = 0; j < 8; j++) {
            var a2 = Math.random() * Math.PI * 2;
            var d2 = Math.random() * 20;
            puffs.push({
                ox: Math.cos(a2) * d2,
                oy: -115 + Math.sin(a2) * d2 * 0.4,
                radius: 15 + Math.random() * 20,
                delay: Math.floor(Math.random() * 10),
                color: 'hot'
            });
        }
        return puffs;
    }

    function generateStemPuffs(cx, cy) {
        var puffs = [];
        for (var i = 0; i < 12; i++) {
            puffs.push({
                ox: (Math.random() - 0.5) * 20,
                oy: -i * 10,
                radius: 12 + Math.random() * 10,
                delay: i * 2
            });
        }
        return puffs;
    }

    function generateNukeDebris(cx, cy) {
        var debris = [];
        for (var i = 0; i < 40; i++) {
            var angle = Math.random() * Math.PI * 2;
            var spd = 1 + Math.random() * 4;
            debris.push({
                x: cx,
                y: cy,
                vx: Math.cos(angle) * spd,
                vy: Math.sin(angle) * spd - Math.random() * 2,
                life: 60 + Math.floor(Math.random() * 80),
                maxLife: 140,
                size: 1 + Math.random() * 3,
                color: ['#ff4400', '#ff8800', '#ffcc00', '#ffaa00'][Math.floor(Math.random() * 4)]
            });
        }
        return debris;
    }

    function drawNukeAnimations() {
        for (var n = 0; n < nukeAnimations.length; n++) {
            var nuke = nukeAnimations[n];
            var f = nuke.frame;
            var progress = f / nuke.maxFrames;
            var cx = nuke.x;
            var cy = nuke.y;

            // Phase 1: Initial white flash (frames 0-20)
            if (f < 20) {
                var flashAlpha = (1 - f / 20) * 0.8;
                ctx.fillStyle = 'rgba(255,255,255,' + flashAlpha + ')';
                ctx.fillRect(0, 0, ARENA_WIDTH, ARENA_HEIGHT);
            }

            // Phase 2: Expanding shockwave ring (frames 5-80)
            if (f > 5 && f < 80) {
                var ringProgress = (f - 5) / 75;
                var ringR = ringProgress * 350;
                var ringAlpha = (1 - ringProgress) * 0.6;
                var ringWidth = 3 + (1 - ringProgress) * 8;

                ctx.strokeStyle = 'rgba(255,170,0,' + ringAlpha + ')';
                ctx.lineWidth = ringWidth;
                ctx.shadowColor = 'rgba(255,100,0,' + ringAlpha + ')';
                ctx.shadowBlur = 20;
                ctx.beginPath();
                ctx.arc(cx, cy, ringR, 0, Math.PI * 2);
                ctx.stroke();

                // Second ring slightly behind
                if (f > 12) {
                    var ring2Progress = (f - 12) / 68;
                    var ring2R = ring2Progress * 300;
                    var ring2Alpha = (1 - ring2Progress) * 0.3;
                    ctx.strokeStyle = 'rgba(255,255,200,' + ring2Alpha + ')';
                    ctx.lineWidth = 2;
                    ctx.beginPath();
                    ctx.arc(cx, cy, ring2R, 0, Math.PI * 2);
                    ctx.stroke();
                }

                ctx.shadowBlur = 0;
            }

            // Phase 3: Fireball at base rising up (frames 3-60)
            if (f > 3 && f < 100) {
                var fbProgress = Math.min((f - 3) / 50, 1);
                var fbRadius = 15 + fbProgress * 50;
                var fbY = cy - fbProgress * 80;
                var fbAlpha = f < 60 ? 0.9 : 0.9 * (1 - (f - 60) / 40);
                if (fbAlpha > 0) {
                    var fbGrad = ctx.createRadialGradient(cx, fbY, 0, cx, fbY, fbRadius);
                    fbGrad.addColorStop(0, 'rgba(255,255,200,' + fbAlpha + ')');
                    fbGrad.addColorStop(0.3, 'rgba(255,200,50,' + fbAlpha + ')');
                    fbGrad.addColorStop(0.6, 'rgba(255,100,0,' + (fbAlpha * 0.8) + ')');
                    fbGrad.addColorStop(1, 'rgba(180,30,0,0)');
                    ctx.fillStyle = fbGrad;
                    ctx.beginPath();
                    ctx.arc(cx, fbY, fbRadius, 0, Math.PI * 2);
                    ctx.fill();
                }
            }

            // Phase 4: Mushroom stem (frames 10-150)
            if (f > 10 && f < 150) {
                var stemProgress = Math.min((f - 10) / 40, 1);
                var stemAlpha = f < 100 ? 0.7 : 0.7 * (1 - (f - 100) / 50);
                if (stemAlpha > 0) {
                    var stemTop = cy - stemProgress * 100;
                    var stemWidth = 18 + stemProgress * 10;

                    // Draw stem as gradient column
                    var stemGrad = ctx.createLinearGradient(cx - stemWidth, 0, cx + stemWidth, 0);
                    stemGrad.addColorStop(0, 'rgba(180,60,0,0)');
                    stemGrad.addColorStop(0.2, 'rgba(200,80,10,' + (stemAlpha * 0.5) + ')');
                    stemGrad.addColorStop(0.5, 'rgba(255,140,30,' + stemAlpha + ')');
                    stemGrad.addColorStop(0.8, 'rgba(200,80,10,' + (stemAlpha * 0.5) + ')');
                    stemGrad.addColorStop(1, 'rgba(180,60,0,0)');
                    ctx.fillStyle = stemGrad;
                    ctx.fillRect(cx - stemWidth, stemTop, stemWidth * 2, cy - stemTop + 10);

                    // Stem puffs for turbulence
                    for (var sp = 0; sp < nuke.stemPuffs.length; sp++) {
                        var puff = nuke.stemPuffs[sp];
                        if (f < puff.delay + 10) continue;
                        var spAlpha = stemAlpha * 0.4;
                        var spY = cy + puff.oy * stemProgress;
                        ctx.fillStyle = 'rgba(255,120,20,' + spAlpha + ')';
                        ctx.beginPath();
                        ctx.arc(cx + puff.ox, spY, puff.radius * stemProgress, 0, Math.PI * 2);
                        ctx.fill();
                    }
                }
            }

            // Phase 5: Mushroom cap (frames 15-170)
            if (f > 15 && f < 170) {
                var capProgress = Math.min((f - 15) / 45, 1);
                var capBaseAlpha = f < 110 ? 1 : (1 - (f - 110) / 60);
                if (capBaseAlpha > 0) {
                    var capY = cy - 80 - capProgress * 50;
                    var capRadiusX = 40 + capProgress * 70;
                    var capRadiusY = 25 + capProgress * 35;

                    // Draw cloud puffs
                    for (var cp = 0; cp < nuke.cloudPuffs.length; cp++) {
                        var cloud = nuke.cloudPuffs[cp];
                        if (f < cloud.delay + 15) continue;
                        var puffProgress = Math.min((f - cloud.delay - 15) / 30, 1);
                        var pAlpha;
                        var r, g, b;

                        if (cloud.color === 'hot') {
                            r = 255; g = 230; b = 150;
                            pAlpha = capBaseAlpha * 0.8 * puffProgress;
                        } else if (cloud.color === 'fire') {
                            r = 255; g = 100 + Math.floor(Math.random() * 40); b = 0;
                            pAlpha = capBaseAlpha * 0.6 * puffProgress;
                        } else {
                            r = 120; g = 60; b = 30;
                            pAlpha = capBaseAlpha * 0.4 * puffProgress;
                        }

                        var pr = cloud.radius * puffProgress * capProgress;
                        var px = cx + cloud.ox * capProgress;
                        var py = capY + cloud.oy * capProgress;

                        if (pr > 0 && pAlpha > 0.01) {
                            var pGrad = ctx.createRadialGradient(px, py, 0, px, py, pr);
                            pGrad.addColorStop(0, 'rgba(' + r + ',' + g + ',' + b + ',' + pAlpha + ')');
                            pGrad.addColorStop(1, 'rgba(' + r + ',' + g + ',' + b + ',0)');
                            ctx.fillStyle = pGrad;
                            ctx.beginPath();
                            ctx.arc(px, py, pr, 0, Math.PI * 2);
                            ctx.fill();
                        }
                    }

                    // Outer glow on the cap
                    if (capProgress > 0.3) {
                        var glowAlpha = capBaseAlpha * 0.15;
                        var glowGrad = ctx.createRadialGradient(cx, capY, 0, cx, capY, capRadiusX * 1.3);
                        glowGrad.addColorStop(0, 'rgba(255,150,30,' + glowAlpha + ')');
                        glowGrad.addColorStop(1, 'rgba(255,50,0,0)');
                        ctx.fillStyle = glowGrad;
                        ctx.beginPath();
                        ctx.ellipse(cx, capY, capRadiusX * 1.3, capRadiusY * 1.3, 0, 0, Math.PI * 2);
                        ctx.fill();
                    }
                }
            }

            // Phase 6: Debris particles
            for (var di = 0; di < nuke.debris.length; di++) {
                var d = nuke.debris[di];
                if (d.life <= 0) continue;
                var dAlpha = (d.life / d.maxLife) * 0.8;
                ctx.fillStyle = d.color;
                ctx.globalAlpha = dAlpha;
                ctx.fillRect(d.x, d.y, d.size, d.size);
            }
            ctx.globalAlpha = 1.0;

            // Ground-level dust ring (frames 20-120)
            if (f > 20 && f < 120) {
                var dustProgress = (f - 20) / 100;
                var dustR = dustProgress * 200;
                var dustAlpha = (1 - dustProgress) * 0.3;
                ctx.strokeStyle = 'rgba(200,150,80,' + dustAlpha + ')';
                ctx.lineWidth = 8 * (1 - dustProgress);
                ctx.beginPath();
                ctx.ellipse(cx, cy + 10, dustR, dustR * 0.3, 0, 0, Math.PI * 2);
                ctx.stroke();
            }
        }
    }

    function updateNukeAnimations() {
        for (var n = nukeAnimations.length - 1; n >= 0; n--) {
            var nuke = nukeAnimations[n];
            nuke.frame++;

            // Update debris positions
            for (var di = nuke.debris.length - 1; di >= 0; di--) {
                var d = nuke.debris[di];
                d.x += d.vx;
                d.y += d.vy;
                d.vy += 0.02; // gravity
                d.life--;
            }

            if (nuke.frame >= nuke.maxFrames) {
                nukeAnimations.splice(n, 1);
            }
        }

        // Screen shake decay
        if (screenShake > 0) {
            screenShake--;
        }
    }

    function getSizeRadius(size) {
        if (size === 'large') return 40;
        if (size === 'medium') return 20;
        return 10;
    }

    // --- HUD Updates ---
    function updateHUD() {
        p1ScoreEl.textContent = p1Score;
        p2ScoreEl.textContent = p2Score;

        var p1LivesStr = '';
        for (var i = 0; i < p1Lives; i++) p1LivesStr += '\u25B2 ';
        p1LivesEl.textContent = p1LivesStr;
        p1LivesEl.className = 'asteroids-hud-lives asteroids-p1-color';

        var p2LivesStr = '';
        for (var j = 0; j < p2Lives; j++) p2LivesStr += '\u25B2 ';
        p2LivesEl.textContent = p2LivesStr;
        p2LivesEl.className = 'asteroids-hud-lives asteroids-p2-color';

        if (wave > 0) {
            waveTextEl.textContent = 'WAVE ' + wave;
        }

        if (p1NukesEl) {
            p1NukesEl.textContent = p1Nukes > 0 ? '\u2622 ' + p1Nukes : '';
        }
        if (p2NukesEl) {
            p2NukesEl.textContent = p2Nukes > 0 ? '\u2622 ' + p2Nukes : '';
        }
    }

    // --- Share link ---
    if (shareLink) {
        shareLink.value = window.location.href;
    }
    if (copyBtn) {
        copyBtn.addEventListener('click', function () {
            shareLink.select();
            navigator.clipboard.writeText(shareLink.value).then(function () {
                copyBtn.textContent = 'COPIED!';
                setTimeout(function () { copyBtn.textContent = 'COPY'; }, 2000);
            });
        });
    }
    if (newGameBtn) {
        newGameBtn.addEventListener('click', function () {
            window.location.href = config.homePath;
        });
    }

    // --- SignalR ---
    var connection = new signalR.HubConnectionBuilder()
        .withUrl(config.hubUrl)
        .withAutomaticReconnect()
        .build();

    connection.on('GameState', function (data) {
        playerNumber = data.playerNumber;
        gameStatus = data.status;

        if (data.player1) {
            p1NameEl.textContent = data.player1.name;
        }
        if (data.player2) {
            p2NameEl.textContent = data.player2.name;
        }

        // Apply full state
        if (data.state) {
            applyTickState(data.state);
        }

        if (gameStatus === 0) {
            // Waiting
            waitingOverlay.style.display = 'flex';
            gameStatusEl.textContent = 'WAITING...';
        } else {
            waitingOverlay.style.display = 'none';
        }

        if (!animFrameId) {
            render();
        }
    });

    connection.on('OpponentJoined', function (data) {
        p2NameEl.textContent = data.opponentName;
        waitingOverlay.style.display = 'none';
        gameStatusEl.textContent = 'GET READY!';
    });

    connection.on('Countdown', function (data) {
        if (data.seconds > 0) {
            countdownText.textContent = data.seconds;
            countdownOverlay.style.display = 'flex';
            gameStatusEl.textContent = 'COUNTDOWN';
        } else {
            countdownOverlay.style.display = 'none';
            gameStatusEl.textContent = '';
        }
    });

    connection.on('Tick', function (data) {
        applyTickState(data);
    });

    connection.on('GameOver', function (data) {
        gameOverOverlay.style.display = 'flex';
        gameOverText.textContent = 'GAME OVER';
        gameOverScores.innerHTML =
            '<span class="asteroids-p1-color">' + (p1NameEl.textContent || 'P1') + ': ' + data.p1Score + '</span><br>' +
            '<span class="asteroids-p2-color">' + (p2NameEl.textContent || 'P2') + ': ' + data.p2Score + '</span><br><br>' +
            'WAVE ' + data.wave + '<br>' +
            '<span style="color:#00ff41;font-size:0.8rem;">' + data.winner + ' WINS!</span>';
        gameStatusEl.textContent = 'GAME OVER';
    });

    connection.on('Error', function (msg) {
        gameStatusEl.textContent = 'ERROR';
        alert('Error: ' + msg);
    });

    function applyTickState(data) {
        if (data.p1) {
            p1 = { x: data.p1.x, y: data.p1.y, rotation: data.p1.rotation, alive: data.p1.alive, thrusting: data.p1.thrusting, invulnerable: data.p1.invulnerable };
        }
        if (data.p2) {
            p2 = { x: data.p2.x, y: data.p2.y, rotation: data.p2.rotation, alive: data.p2.alive, thrusting: data.p2.thrusting, invulnerable: data.p2.invulnerable };
        }

        if (data.asteroids && data.asteroids.length > 0) {
            asteroids = [];
            for (var i = 0; i < data.asteroids.length; i++) {
                var a = data.asteroids[i];
                asteroids.push({ id: a.id, x: a.x, y: a.y, rotation: a.rotation, size: a.size, shapeVariant: a.shapeVariant });
            }
        }

        if (data.bullets) {
            bullets = [];
            for (var j = 0; j < data.bullets.length; j++) {
                var b = data.bullets[j];
                bullets.push({ id: b.id, x: b.x, y: b.y, owner: b.owner });
            }
        }

        // Spawn explosion particles
        if (data.explosions) {
            for (var k = 0; k < data.explosions.length; k++) {
                var e = data.explosions[k];
                if (e.size === 'nuke') {
                    spawnNukeExplosion(e.x, e.y);
                } else {
                    var color = e.size === 'ship' ? (P1_COLOR) : ASTEROID_COLOR;
                    spawnExplosion(e.x, e.y, e.size, color);
                }
            }
        }

        if (data.p1Score !== undefined) p1Score = data.p1Score;
        if (data.p2Score !== undefined) p2Score = data.p2Score;
        if (data.p1Lives !== undefined) p1Lives = data.p1Lives;
        if (data.p2Lives !== undefined) p2Lives = data.p2Lives;
        if (data.wave !== undefined) wave = data.wave;
        if (data.p1Nukes !== undefined) p1Nukes = data.p1Nukes;
        if (data.p2Nukes !== undefined) p2Nukes = data.p2Nukes;

        updateHUD();
    }

    // --- Connect ---
    connection.start()
        .then(function () {
            gameStatusEl.textContent = 'CONNECTED';
            connection.invoke('JoinGame', config.shortCode, config.playerName, config.sessionId);
        })
        .catch(function (err) {
            gameStatusEl.textContent = 'CONNECTION FAILED';
            alert('Connection failed: ' + err);
        });
})();
