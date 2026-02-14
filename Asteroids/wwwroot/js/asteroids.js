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

    // --- Canvas setup ---
    canvas.width = ARENA_WIDTH;
    canvas.height = ARENA_HEIGHT;

    // --- Input ---
    var inputState = { thrust: false, rotateLeft: false, rotateRight: false, fire: false };
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

        // Update particles
        updateParticles();

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
                var color = e.size === 'ship' ? (P1_COLOR) : ASTEROID_COLOR;
                spawnExplosion(e.x, e.y, e.size, color);
            }
        }

        if (data.p1Score !== undefined) p1Score = data.p1Score;
        if (data.p2Score !== undefined) p2Score = data.p2Score;
        if (data.p1Lives !== undefined) p1Lives = data.p1Lives;
        if (data.p2Lives !== undefined) p2Lives = data.p2Lives;
        if (data.wave !== undefined) wave = data.wave;

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
