(function () {
    'use strict';

    var config = window.tanksConfig;
    if (!config) return;

    // ===== CONSTANTS =====
    var ARENA_W = 1200;
    var ARENA_H = 800;
    var TANK_W = 30;
    var TANK_H = 16;
    var BARREL_LEN = 22;
    var TERRAIN_SAMPLE_INTERVAL = 4;

    // ===== DOM ELEMENTS =====
    var canvas = document.getElementById('tanksCanvas');
    var ctx = canvas.getContext('2d');
    canvas.width = ARENA_W;
    canvas.height = ARENA_H;

    var waitingOverlay = document.getElementById('waitingOverlay');
    var countdownOverlay = document.getElementById('countdownOverlay');
    var countdownText = document.getElementById('countdownText');
    var weaponSelectOverlay = document.getElementById('weaponSelectOverlay');
    var weaponGrid = document.getElementById('weaponGrid');
    var gameOverOverlay = document.getElementById('gameOverOverlay');
    var gameOverText = document.getElementById('gameOverText');
    var gameOverScores = document.getElementById('gameOverScores');
    var newGameBtn = document.getElementById('newGameBtn');

    var p1NameEl = document.getElementById('p1Name');
    var p2NameEl = document.getElementById('p2Name');
    var p1HealthEl = document.getElementById('p1Health');
    var p2HealthEl = document.getElementById('p2Health');
    var p1HealthBar = document.getElementById('p1HealthBar');
    var p2HealthBar = document.getElementById('p2HealthBar');
    var p1ScoreEl = document.getElementById('p1Score');
    var p2ScoreEl = document.getElementById('p2Score');
    var gameStatusEl = document.getElementById('gameStatus');
    var turnInfoEl = document.getElementById('turnInfo');
    var timerDisplayEl = document.getElementById('timerDisplay');

    var angleSlider = document.getElementById('angleSlider');
    var powerSlider = document.getElementById('powerSlider');
    var angleDisplay = document.getElementById('angleDisplay');
    var powerDisplay = document.getElementById('powerDisplay');
    var fireBtn = document.getElementById('fireBtn');
    var currentWeaponName = document.getElementById('currentWeaponName');
    var changeWeaponBtn = document.getElementById('changeWeaponBtn');
    var shareLink = document.getElementById('shareLink');
    var copyBtn = document.getElementById('copyBtn');

    // ===== GAME STATE =====
    var state = {
        status: -1, // not connected
        playerNumber: 0,
        currentTurn: 0,
        turnNumber: 0,
        terrain: null, // full 1200-element heightmap
        terrainTarget: null, // for lerp animation
        terrainLerpStart: 0,
        terrainLerpDuration: 200,
        p1: { x: 0, y: 0, health: 100, score: 0, name: '---', angle: 45, power: 50, targetY: null, lerpStart: 0 },
        p2: { x: 0, y: 0, health: 100, score: 0, name: '---', angle: 135, power: 50, targetY: null, lerpStart: 0 },
        projectiles: [], // [{x, y, trail: [{x,y}]}]
        explosions: [], // active explosion animations
        damageNumbers: [], // floating damage text
        particles: [], // all active particles
        selectedWeapon: null,
        weapons: [], // current player's weapons
        myTurn: false,
        timeRemaining: 0,
        timerStart: 0,
        timerDuration: 0
    };

    // ===== WEAPON NAMES =====
    var WEAPON_NAMES = ['STANDARD', 'BIG SHOT', 'SNIPER', 'DIRT MOVER', 'BOUNCER', '3-SHOT', 'ROLLER', 'NUKE'];
    var WEAPON_COLORS = ['#ffaa00', '#ff6600', '#00ffff', '#aa8844', '#ff44ff', '#44ff44', '#ffff00', '#ff0000'];

    var WEAPON_PROJECTILE = [
        { color: '#ffaa00', glow: '#ffaa00', size: 4, trail: 'rgba(255,170,0,' },   // Standard
        { color: '#ff6600', glow: '#ff4400', size: 6, trail: 'rgba(255,100,0,' },   // Big Shot
        { color: '#00ffff', glow: '#00ccff', size: 2, trail: 'rgba(0,255,255,' },   // Sniper
        { color: '#aa8844', glow: '#886622', size: 5, trail: 'rgba(170,136,68,' },  // Dirt Mover
        { color: '#ff44ff', glow: '#ff00ff', size: 4, trail: 'rgba(255,68,255,' },  // Bouncer
        { color: '#44ff44', glow: '#00ff00', size: 3, trail: 'rgba(68,255,68,' },   // 3-Shot
        { color: '#ffff00', glow: '#ffcc00', size: 4, trail: 'rgba(255,255,0,' },   // Roller
        { color: '#ff0000', glow: '#ff0000', size: 7, trail: 'rgba(255,0,0,' }      // Nuke
    ];

    // ===== SIGNALR CONNECTION =====
    var connection = new signalR.HubConnectionBuilder()
        .withUrl(config.hubUrl)
        .withAutomaticReconnect()
        .build();

    connection.start().then(function () {
        connection.invoke('JoinGame', config.shortCode, config.playerName, config.sessionId);
    }).catch(function (err) {
        gameStatusEl.textContent = 'CONNECTION FAILED';
        console.error('SignalR connection error:', err);
    });

    // ===== SIGNALR HANDLERS =====

    connection.on('GameState', function (data) {
        state.playerNumber = data.playerNumber;
        state.status = data.status;
        state.currentTurn = data.currentTurn;
        state.turnNumber = data.turnNumber;

        // Expand terrain from compressed (every 4th) to full 1200
        state.terrain = expandTerrain(data.terrain);

        // Players
        state.p1.x = data.player1.x;
        state.p1.y = data.player1.y;
        state.p1.health = data.player1.health;
        state.p1.score = data.player1.score;
        state.p1.name = data.player1.name;
        state.p1.angle = data.player1.angle;
        state.p1.power = data.player1.power;

        if (data.player2) {
            state.p2.x = data.player2.x;
            state.p2.y = data.player2.y;
            state.p2.health = data.player2.health;
            state.p2.score = data.player2.score;
            state.p2.name = data.player2.name;
            state.p2.angle = data.player2.angle;
            state.p2.power = data.player2.power;
        }

        // Weapons
        if (data.playerNumber === 1) {
            state.weapons = data.player1Weapons;
        } else {
            state.weapons = data.player2Weapons;
        }

        state.myTurn = (state.currentTurn === state.playerNumber);

        updateHUD();
        updateOverlays();

        // Show change button if reconnecting during own Aiming phase
        if (state.status === 3 && state.myTurn && state.weapons.length > 0) {
            changeWeaponBtn.style.display = 'inline-block';
        } else {
            changeWeaponBtn.style.display = 'none';
        }

        // Set controls to current values
        var myTank = data.playerNumber === 1 ? state.p1 : state.p2;
        angleSlider.value = myTank.angle;
        powerSlider.value = myTank.power;
        angleDisplay.textContent = Math.round(myTank.angle);
        powerDisplay.textContent = Math.round(myTank.power);
    });

    connection.on('OpponentJoined', function (data) {
        state.p2.name = data.opponentName;
        updateHUD();
    });

    connection.on('Countdown', function (data) {
        state.status = 1; // Countdown
        if (data.seconds > 0) {
            countdownOverlay.style.display = 'flex';
            waitingOverlay.style.display = 'none';
            countdownText.textContent = data.seconds;
            countdownText.style.animation = 'none';
            void countdownText.offsetWidth;
            countdownText.style.animation = 'countdown-pulse 0.5s ease-in-out';
        } else {
            countdownOverlay.style.display = 'none';
        }
    });

    connection.on('TurnStart', function (data) {
        state.status = 2; // WeaponSelect
        state.currentTurn = data.currentPlayer;
        state.turnNumber = data.turnNumber;
        state.myTurn = (data.currentPlayer === state.playerNumber);
        state.selectedWeapon = null;
        state.timeRemaining = data.timeLimit;
        state.timerStart = performance.now();
        state.timerDuration = data.timeLimit * 1000;

        // Rebuild weapon list from server data
        state.weapons = [];
        if (data.weapons) {
            data.weapons.forEach(function (w) {
                state.weapons.push(w);
            });
        }

        currentWeaponName.textContent = 'NO WEAPON';
        changeWeaponBtn.style.display = 'none';

        if (state.myTurn) {
            showWeaponSelect();
        } else {
            weaponSelectOverlay.style.display = 'none';
            disableControls();
        }

        updateHUD();
        gameStatusEl.textContent = state.myTurn ? 'YOUR TURN' : 'OPPONENT\'S TURN';
    });

    connection.on('WeaponSelected', function (data) {
        var wasAiming = (state.status === 3);
        state.status = 3; // Aiming
        state.selectedWeapon = data.weaponType;
        weaponSelectOverlay.style.display = 'none';
        currentWeaponName.textContent = data.weaponName;

        // Only reset timer on initial weapon select, not on mid-turn swap
        if (!wasAiming) {
            state.timerStart = performance.now();
            state.timerDuration = 15000;
        }

        // Update weapon list if server sent it
        if (data.weapons) {
            state.weapons = [];
            data.weapons.forEach(function (w) { state.weapons.push(w); });
        }

        if (state.myTurn) {
            enableControls();
            changeWeaponBtn.style.display = state.weapons.length > 0 ? 'inline-block' : 'none';
        }

        gameStatusEl.textContent = state.myTurn ? 'AIM & FIRE!' : 'OPPONENT AIMING...';
    });

    connection.on('AimUpdate', function (data) {
        if (!state.myTurn) {
            // Show opponent's aim
            var opTank = state.currentTurn === 1 ? state.p1 : state.p2;
            opTank.angle = data.angle;
            opTank.power = data.power;
        }
    });

    connection.on('Fired', function () {
        state.status = 4; // Firing
        disableControls();
        changeWeaponBtn.style.display = 'none';
        state.projectiles = [];
        gameStatusEl.textContent = 'FIRING!';
        timerDisplayEl.textContent = '';
    });

    connection.on('ProjectileTick', function (data) {
        if (!data.projectiles) return;
        data.projectiles.forEach(function (p) {
            while (state.projectiles.length <= p.index) {
                state.projectiles.push({ x: p.x, y: p.y, trail: [] });
            }
            var proj = state.projectiles[p.index];
            proj.trail.push({ x: proj.x, y: proj.y });
            if (proj.trail.length > 20) proj.trail.shift();
            proj.x = p.x;
            proj.y = p.y;
        });
    });

    connection.on('Bounce', function (data) {
        spawnBounceEffect(data.x, data.y);
    });

    connection.on('RollerTick', function (data) {
        if (data.positions) {
            data.positions.forEach(function (p) {
                spawnRollerTrail(p.x, p.y);
            });
        }
    });

    connection.on('Explosion', function (data) {
        // Update terrain with lerp
        var newTerrain = expandTerrain(data.terrain);
        state.terrainTarget = newTerrain;
        state.terrainLerpStart = performance.now();

        // Update health/scores
        state.p1.health = data.p1Health;
        state.p2.health = data.p2Health;
        state.p1.score = data.p1Score;
        state.p2.score = data.p2Score;

        // Trigger explosion visual
        spawnExplosion(data.x, data.y, data.radius, data.weaponType, data.directHit);

        // Spawn damage number
        if (data.damage > 0) {
            spawnDamageNumber(data.x, data.y - 30, data.damage, data.directHit);
        }

        // Clear projectiles that caused this explosion
        state.projectiles = state.projectiles.filter(function (p) {
            var dx = p.x - data.x;
            var dy = p.y - data.y;
            return Math.sqrt(dx * dx + dy * dy) > 20;
        });

        updateHUD();
    });

    connection.on('TankPositionUpdate', function (data) {
        if (data.p1) {
            state.p1.targetY = data.p1.y;
            state.p1.lerpStart = performance.now();
        }
        if (data.p2) {
            state.p2.targetY = data.p2.y;
            state.p2.lerpStart = performance.now();
        }
    });

    connection.on('AutoFire', function () {
        state.status = 4;
        disableControls();
        changeWeaponBtn.style.display = 'none';
        weaponSelectOverlay.style.display = 'none';
        gameStatusEl.textContent = 'AUTO-FIRE!';
    });

    connection.on('GameOver', function (data) {
        state.status = 5; // GameOver
        disableControls();

        var text = data.winner ? data.winner + ' WINS!' : 'DRAW!';
        var color = '#ffaa00';
        if (data.winner === state.p1.name && state.playerNumber === 1) color = '#00ffff';
        else if (data.winner === state.p2.name && state.playerNumber === 2) color = '#ff00ff';

        gameOverText.textContent = text;
        gameOverText.style.color = color;
        gameOverScores.innerHTML =
            '<span class="tanks-p1-color">' + state.p1.name + ': ' + data.p1Score + '</span>' +
            ' | ' +
            '<span class="tanks-p2-color">' + state.p2.name + ': ' + data.p2Score + '</span>';
        gameOverOverlay.style.display = 'flex';
    });

    connection.on('Error', function (msg) {
        console.error('Server error:', msg);
        gameStatusEl.textContent = 'ERROR: ' + msg;
    });

    // ===== TERRAIN HELPERS =====

    function expandTerrain(compressed) {
        var full = new Float32Array(ARENA_W);
        for (var i = 0; i < compressed.length; i++) {
            var x = i * TERRAIN_SAMPLE_INTERVAL;
            if (x < ARENA_W) full[x] = compressed[i];
        }
        // Interpolate gaps
        for (var x = 0; x < ARENA_W; x++) {
            if (x % TERRAIN_SAMPLE_INTERVAL !== 0) {
                var prevIdx = Math.floor(x / TERRAIN_SAMPLE_INTERVAL) * TERRAIN_SAMPLE_INTERVAL;
                var nextIdx = prevIdx + TERRAIN_SAMPLE_INTERVAL;
                if (nextIdx >= ARENA_W) nextIdx = prevIdx;
                var t = (x - prevIdx) / TERRAIN_SAMPLE_INTERVAL;
                full[x] = full[prevIdx] * (1 - t) + full[nextIdx] * t;
            }
        }
        return full;
    }

    // ===== HUD =====

    function updateHUD() {
        p1NameEl.textContent = state.p1.name;
        p2NameEl.textContent = state.p2.name;
        p1HealthEl.textContent = state.p1.health;
        p2HealthEl.textContent = state.p2.health;
        p1ScoreEl.textContent = 'SCORE: ' + state.p1.score;
        p2ScoreEl.textContent = 'SCORE: ' + state.p2.score;

        p1HealthBar.style.width = state.p1.health + '%';
        p2HealthBar.style.width = state.p2.health + '%';

        setHealthBarColor(p1HealthBar, state.p1.health);
        setHealthBarColor(p2HealthBar, state.p2.health);

        if (state.turnNumber > 0) {
            turnInfoEl.textContent = 'TURN ' + state.turnNumber + '/20';
        }
    }

    function setHealthBarColor(bar, health) {
        if (health > 60) bar.style.background = 'linear-gradient(90deg, #00ff41, #44ff44)';
        else if (health > 30) bar.style.background = 'linear-gradient(90deg, #ffaa00, #ffdd00)';
        else bar.style.background = 'linear-gradient(90deg, #ff2200, #ff6600)';
    }

    function updateOverlays() {
        waitingOverlay.style.display = 'none';
        countdownOverlay.style.display = 'none';
        weaponSelectOverlay.style.display = 'none';
        gameOverOverlay.style.display = 'none';

        if (state.status === 0) { // Waiting
            waitingOverlay.style.display = 'flex';
            shareLink.value = window.location.href;
            gameStatusEl.textContent = 'WAITING...';
        } else if (state.status === 1) { // Countdown
            countdownOverlay.style.display = 'flex';
            gameStatusEl.textContent = 'GET READY';
        } else if (state.status === 2) { // WeaponSelect
            gameStatusEl.textContent = state.myTurn ? 'SELECT WEAPON' : 'OPPONENT CHOOSING...';
        } else if (state.status === 3) { // Aiming
            gameStatusEl.textContent = state.myTurn ? 'AIM & FIRE!' : 'OPPONENT AIMING...';
        } else if (state.status === 4) { // Firing
            gameStatusEl.textContent = 'FIRING!';
        } else if (state.status === 5) { // GameOver
            gameOverOverlay.style.display = 'flex';
        }
    }

    // ===== WEAPON SELECT =====

    function getGroupedWeapons() {
        var grouped = {};
        state.weapons.forEach(function (w) {
            if (!grouped[w.type]) grouped[w.type] = { type: w.type, name: w.name, count: 0 };
            grouped[w.type].count++;
        });
        return Object.keys(grouped)
            .sort(function (a, b) { return parseInt(a) - parseInt(b); })
            .map(function (k) { return grouped[k]; });
    }

    function showWeaponSelect() {
        weaponGrid.innerHTML = '';
        var groups = getGroupedWeapons();
        groups.forEach(function (g) {
            var card = document.createElement('button');
            card.className = 'tanks-weapon-card';
            card.textContent = g.count > 1 ? g.name + ' x' + g.count : g.name;
            card.style.borderColor = WEAPON_COLORS[g.type] || '#ffaa00';
            card.addEventListener('click', function () {
                connection.invoke('SelectWeapon', config.shortCode, g.type);
            });
            weaponGrid.appendChild(card);
        });
        weaponSelectOverlay.style.display = 'flex';
    }

    // ===== CONTROLS =====

    function enableControls() {
        angleSlider.disabled = false;
        powerSlider.disabled = false;
        fireBtn.disabled = false;
        fireBtn.classList.add('active');
    }

    function disableControls() {
        angleSlider.disabled = true;
        powerSlider.disabled = true;
        fireBtn.disabled = true;
        fireBtn.classList.remove('active');
    }

    angleSlider.addEventListener('input', function () {
        var val = parseFloat(this.value);
        angleDisplay.textContent = Math.round(val);
        if (state.myTurn) {
            var tank = state.playerNumber === 1 ? state.p1 : state.p2;
            tank.angle = val;
            connection.invoke('SetFiringParams', config.shortCode, val, parseFloat(powerSlider.value));
        }
    });

    powerSlider.addEventListener('input', function () {
        var val = parseFloat(this.value);
        powerDisplay.textContent = Math.round(val);
        if (state.myTurn) {
            var tank = state.playerNumber === 1 ? state.p1 : state.p2;
            tank.power = val;
            connection.invoke('SetFiringParams', config.shortCode, parseFloat(angleSlider.value), val);
        }
    });

    fireBtn.addEventListener('click', function () {
        if (!state.myTurn || fireBtn.disabled) return;
        connection.invoke('Fire', config.shortCode, parseFloat(angleSlider.value), parseFloat(powerSlider.value));
    });

    // ===== KEYBOARD INPUT =====

    document.addEventListener('keydown', function (e) {
        if (state.status === 5) return; // Game over

        // Weapon quick-select (1-0 keys) — works during WeaponSelect and Aiming
        if ((state.status === 2 || state.status === 3) && state.myTurn) {
            var numKey = -1;
            if (e.key >= '1' && e.key <= '9') numKey = parseInt(e.key) - 1;
            else if (e.key === '0') numKey = 9;

            if (numKey >= 0) {
                var groups = getGroupedWeapons();
                if (numKey < groups.length) {
                    connection.invoke('SelectWeapon', config.shortCode, groups[numKey].type);
                    return;
                }
            }
        }

        // Escape — close weapon overlay during Aiming without changing weapon
        if (e.key === 'Escape' && state.status === 3 && state.myTurn) {
            weaponSelectOverlay.style.display = 'none';
            return;
        }

        // Aiming controls
        if (state.status === 3 && state.myTurn) { // Aiming
            var angleVal = parseFloat(angleSlider.value);
            var powerVal = parseFloat(powerSlider.value);
            var changed = false;

            if (e.key === 'ArrowUp' || e.key === 'w' || e.key === 'W') {
                angleVal = Math.min(180, angleVal + 1);
                changed = true;
            } else if (e.key === 'ArrowDown' || e.key === 's' || e.key === 'S') {
                angleVal = Math.max(0, angleVal - 1);
                changed = true;
            } else if (e.key === 'ArrowRight' || e.key === 'd' || e.key === 'D') {
                powerVal = Math.min(100, powerVal + 1);
                changed = true;
            } else if (e.key === 'ArrowLeft' || e.key === 'a' || e.key === 'A') {
                powerVal = Math.max(1, powerVal - 1);
                changed = true;
            } else if (e.key === ' ' || e.key === 'Enter') {
                e.preventDefault();
                fireBtn.click();
                return;
            }

            if (changed) {
                e.preventDefault();
                angleSlider.value = angleVal;
                powerSlider.value = powerVal;
                angleDisplay.textContent = Math.round(angleVal);
                powerDisplay.textContent = Math.round(powerVal);
                var tank = state.playerNumber === 1 ? state.p1 : state.p2;
                tank.angle = angleVal;
                tank.power = powerVal;
                connection.invoke('SetFiringParams', config.shortCode, angleVal, powerVal);
            }
        }
    });

    // ===== COPY BUTTON =====

    if (copyBtn) {
        copyBtn.addEventListener('click', function () {
            shareLink.select();
            navigator.clipboard.writeText(shareLink.value).then(function () {
                copyBtn.textContent = 'COPIED!';
                setTimeout(function () { copyBtn.textContent = 'COPY'; }, 2000);
            });
        });
    }

    // ===== NEW GAME BUTTON =====

    if (newGameBtn) {
        newGameBtn.addEventListener('click', function () {
            window.location.href = config.homePath;
        });
    }

    // ===== CHANGE WEAPON BUTTON =====

    if (changeWeaponBtn) {
        changeWeaponBtn.addEventListener('click', function () {
            if (state.status === 3 && state.myTurn && state.weapons.length > 0) {
                showWeaponSelect();
            }
        });
    }

    // ===== EXPLOSION VISUAL SYSTEM =====

    function spawnExplosion(x, y, radius, weaponType, directHit) {
        var now = performance.now();
        var isNuke = weaponType === 7;
        var isDirtMover = weaponType === 3;
        var scale = radius / 30; // Normalize to standard radius

        var explosion = {
            x: x,
            y: y,
            radius: radius,
            weaponType: weaponType,
            startTime: now,
            duration: isNuke ? 2500 : 2000
        };

        state.explosions.push(explosion);

        // Phase 4: Debris & Sparks particles
        var sparkCount = Math.round((isDirtMover ? 10 : 30) * scale);
        var debrisCount = Math.round((isDirtMover ? 25 : 12) * scale);
        var emberCount = Math.round(20 * scale);
        var dirtCount = Math.round((isDirtMover ? 30 : 15) * scale);
        var smokeCount = Math.round((isNuke ? 15 : 8) * scale);

        // Sparks
        for (var i = 0; i < sparkCount; i++) {
            var angle = Math.random() * Math.PI * 2;
            var speed = 3 + Math.random() * 8;
            state.particles.push({
                type: 'spark',
                x: x, y: y,
                vx: Math.cos(angle) * speed,
                vy: Math.sin(angle) * speed - 3,
                life: 1,
                decay: 0.015 + Math.random() * 0.01,
                color: Math.random() > 0.3 ? '#ffff44' : '#ffffff',
                size: 1.5 + Math.random()
            });
        }

        // Debris chunks
        for (var i = 0; i < debrisCount; i++) {
            var angle = -Math.PI * 0.1 - Math.random() * Math.PI * 0.8;
            var speed = 2 + Math.random() * 5;
            state.particles.push({
                type: 'debris',
                x: x, y: y,
                vx: Math.cos(angle) * speed * (Math.random() > 0.5 ? 1 : -1),
                vy: Math.sin(angle) * speed - 2,
                life: 1,
                decay: 0.008 + Math.random() * 0.008,
                color: Math.random() > 0.5 ? '#cc6600' : '#884400',
                size: 2 + Math.random() * 3,
                bounced: false
            });
        }

        // Embers
        for (var i = 0; i < emberCount; i++) {
            state.particles.push({
                type: 'ember',
                x: x + (Math.random() - 0.5) * radius,
                y: y + (Math.random() - 0.5) * radius * 0.5,
                vx: (Math.random() - 0.5) * 1.5,
                vy: -0.5 - Math.random() * 1.5,
                life: 1,
                decay: 0.005 + Math.random() * 0.005,
                color: '#ff8800',
                size: 1 + Math.random() * 1.5,
                flicker: Math.random() * Math.PI * 2
            });
        }

        // Dirt/dust
        for (var i = 0; i < dirtCount; i++) {
            var angle = -Math.PI * 0.2 + Math.random() * Math.PI * 0.4;
            var dir = Math.random() > 0.5 ? 1 : -1;
            state.particles.push({
                type: 'dirt',
                x: x, y: y,
                vx: dir * (1 + Math.random() * 4),
                vy: -1 - Math.random() * 2,
                life: 1,
                decay: 0.01 + Math.random() * 0.01,
                color: Math.random() > 0.5 ? '#aa8855' : '#887744',
                size: 3 + Math.random() * 4
            });
        }

        // Phase 5: Smoke
        for (var i = 0; i < smokeCount; i++) {
            state.particles.push({
                type: 'smoke',
                x: x + (Math.random() - 0.5) * radius * 0.5,
                y: y,
                vx: (Math.random() - 0.5) * 0.5,
                vy: -0.3 - Math.random() * 0.8,
                life: 0, // Delayed start
                decay: -0.006, // Negative = grows first
                maxLife: 0.8,
                color: '#333333',
                size: 5 + Math.random() * 8,
                delay: 300 + Math.random() * 300,
                startTime: now
            });
        }

        // Nuke mushroom cloud
        if (isNuke) {
            for (var i = 0; i < 20; i++) {
                state.particles.push({
                    type: 'smoke',
                    x: x + (Math.random() - 0.5) * 20,
                    y: y,
                    vx: (Math.random() - 0.5) * 2,
                    vy: -2 - Math.random() * 3,
                    life: 0,
                    decay: -0.004,
                    maxLife: 0.9,
                    color: i < 10 ? '#554400' : '#443300',
                    size: 10 + Math.random() * 15,
                    delay: 100 + Math.random() * 400,
                    startTime: now,
                    mushroom: true
                });
            }
        }
    }

    function spawnBounceEffect(x, y) {
        for (var i = 0; i < 8; i++) {
            var angle = Math.random() * Math.PI * 2;
            var speed = 2 + Math.random() * 4;
            state.particles.push({
                type: 'spark',
                x: x, y: y,
                vx: Math.cos(angle) * speed,
                vy: Math.sin(angle) * speed - 2,
                life: 1,
                decay: 0.03,
                color: '#ff44ff',
                size: 1.5
            });
        }
    }

    function spawnRollerTrail(x, y) {
        for (var i = 0; i < 2; i++) {
            state.particles.push({
                type: 'spark',
                x: x + (Math.random() - 0.5) * 6,
                y: y - Math.random() * 4,
                vx: (Math.random() - 0.5) * 1,
                vy: -0.5 - Math.random(),
                life: 0.8,
                decay: 0.04,
                color: '#ffff00',
                size: 1
            });
        }
        state.particles.push({
            type: 'dirt',
            x: x, y: y,
            vx: (Math.random() - 0.5) * 2,
            vy: -0.3 - Math.random() * 0.5,
            life: 0.6,
            decay: 0.02,
            color: '#887755',
            size: 2 + Math.random() * 2
        });
    }

    function spawnDamageNumber(x, y, damage, directHit) {
        var dmg = Math.round(damage);
        var color = '#ffffff';
        if (dmg >= 40) color = '#ff2200';
        else if (dmg >= 25) color = '#ff8800';
        else if (dmg >= 15) color = '#ffaa00';

        state.damageNumbers.push({
            x: x,
            y: y,
            text: dmg.toString(),
            color: color,
            life: 1,
            scale: 1.2,
            directHit: directHit
        });

        if (directHit) {
            state.damageNumbers.push({
                x: x,
                y: y - 25,
                text: 'DIRECT HIT!',
                color: '#ff0000',
                life: 1,
                scale: 1.3,
                directHit: true,
                isLabel: true
            });
        }
    }

    // ===== RENDER LOOP =====

    function render(timestamp) {
        requestAnimationFrame(render);

        ctx.clearRect(0, 0, ARENA_W, ARENA_H);

        // Update terrain lerp
        if (state.terrainTarget && state.terrain) {
            var elapsed = timestamp - state.terrainLerpStart;
            var t = Math.min(1, elapsed / state.terrainLerpDuration);
            for (var x = 0; x < ARENA_W; x++) {
                state.terrain[x] = state.terrain[x] + (state.terrainTarget[x] - state.terrain[x]) * t;
            }
            if (t >= 1) {
                state.terrain = state.terrainTarget;
                state.terrainTarget = null;
            }
        }

        // Update tank Y lerp
        updateTankLerp(state.p1, timestamp);
        updateTankLerp(state.p2, timestamp);

        drawSky();
        drawTerrain();
        drawTanks();
        drawProjectiles();
        drawExplosions(timestamp);
        drawParticles(timestamp);
        drawDamageNumbers(timestamp);
        drawTimer(timestamp);
    }

    function updateTankLerp(tank, timestamp) {
        if (tank.targetY !== null) {
            var elapsed = timestamp - tank.lerpStart;
            var t = Math.min(1, elapsed / 300);
            tank.y = tank.y + (tank.targetY - tank.y) * t;
            if (t >= 1) {
                tank.y = tank.targetY;
                tank.targetY = null;
            }
        }
    }

    // ===== DRAWING FUNCTIONS =====

    function drawSky() {
        var grad = ctx.createLinearGradient(0, 0, 0, ARENA_H);
        grad.addColorStop(0, '#000011');
        grad.addColorStop(0.4, '#000022');
        grad.addColorStop(0.7, '#0a0a2e');
        grad.addColorStop(1, '#1a0a2e');
        ctx.fillStyle = grad;
        ctx.fillRect(0, 0, ARENA_W, ARENA_H);

        // Stars
        ctx.fillStyle = 'rgba(255,255,255,0.3)';
        for (var i = 0; i < 50; i++) {
            var sx = (i * 137 + 43) % ARENA_W;
            var sy = (i * 89 + 17) % (ARENA_H * 0.5);
            ctx.fillRect(sx, sy, 1, 1);
        }
    }

    function drawTerrain() {
        if (!state.terrain) return;

        // Terrain fill
        ctx.beginPath();
        ctx.moveTo(0, ARENA_H);
        for (var x = 0; x < ARENA_W; x++) {
            ctx.lineTo(x, ARENA_H - state.terrain[x]);
        }
        ctx.lineTo(ARENA_W, ARENA_H);
        ctx.closePath();

        var terrGrad = ctx.createLinearGradient(0, ARENA_H - 500, 0, ARENA_H);
        terrGrad.addColorStop(0, '#3a2510');
        terrGrad.addColorStop(0.3, '#2a1a0a');
        terrGrad.addColorStop(1, '#1a0f05');
        ctx.fillStyle = terrGrad;
        ctx.fill();

        // Neon outline
        ctx.beginPath();
        for (var x = 0; x < ARENA_W; x++) {
            if (x === 0) ctx.moveTo(x, ARENA_H - state.terrain[x]);
            else ctx.lineTo(x, ARENA_H - state.terrain[x]);
        }
        ctx.strokeStyle = '#ff8800';
        ctx.lineWidth = 2;
        ctx.shadowColor = '#ff8800';
        ctx.shadowBlur = 8;
        ctx.stroke();
        ctx.shadowBlur = 0;
    }

    function drawTanks() {
        drawTank(state.p1, '#00ffff', 'rgba(0,255,255,');
        if (state.p2.name !== '---') {
            drawTank(state.p2, '#ff00ff', 'rgba(255,0,255,');
        }
    }

    function drawTank(tank, color, rgbaBase) {
        var x = tank.x;
        var y = tank.y;

        // Tank body
        ctx.fillStyle = color;
        ctx.shadowColor = color;
        ctx.shadowBlur = 6;
        ctx.fillRect(x - TANK_W / 2, y - TANK_H, TANK_W, TANK_H);

        // Tracks
        ctx.fillStyle = rgbaBase + '0.5)';
        ctx.fillRect(x - TANK_W / 2 - 2, y - 3, TANK_W + 4, 3);

        // Turret dome
        ctx.beginPath();
        ctx.arc(x, y - TANK_H, 8, Math.PI, 0);
        ctx.fillStyle = color;
        ctx.fill();

        // Barrel
        var angleRad = tank.angle * Math.PI / 180;
        var bx = x + Math.cos(angleRad) * BARREL_LEN;
        var by = y - TANK_H - Math.sin(angleRad) * BARREL_LEN;

        ctx.beginPath();
        ctx.moveTo(x, y - TANK_H);
        ctx.lineTo(bx, by);
        ctx.strokeStyle = color;
        ctx.lineWidth = 4;
        ctx.stroke();

        ctx.shadowBlur = 0;
    }

    function drawProjectiles() {
        var wp = WEAPON_PROJECTILE[state.selectedWeapon || 0];

        state.projectiles.forEach(function (proj) {
            // Trail
            if (proj.trail.length > 1) {
                for (var i = 1; i < proj.trail.length; i++) {
                    var alpha = i / proj.trail.length * 0.6;
                    ctx.beginPath();
                    ctx.moveTo(proj.trail[i - 1].x, proj.trail[i - 1].y);
                    ctx.lineTo(proj.trail[i].x, proj.trail[i].y);
                    ctx.strokeStyle = wp.trail + alpha + ')';
                    ctx.lineWidth = 2;
                    ctx.stroke();
                }
            }

            // Projectile dot
            ctx.beginPath();
            ctx.arc(proj.x, proj.y, wp.size, 0, Math.PI * 2);
            ctx.fillStyle = wp.color;
            ctx.shadowColor = wp.glow;
            ctx.shadowBlur = 12;
            ctx.fill();
            ctx.shadowBlur = 0;
        });
    }

    function drawExplosions(timestamp) {
        state.explosions = state.explosions.filter(function (exp) {
            var elapsed = timestamp - exp.startTime;
            if (elapsed > exp.duration) return false;

            var t = elapsed / exp.duration;
            var isNuke = exp.weaponType === 7;

            // Phase 1: Flash (0-50ms)
            if (elapsed < 50) {
                var flashAlpha = 1 - elapsed / 50;
                ctx.beginPath();
                ctx.arc(exp.x, exp.y, exp.radius * 0.5, 0, Math.PI * 2);
                ctx.fillStyle = 'rgba(255,255,200,' + flashAlpha + ')';
                ctx.fill();

                // Screen flash
                var screenAlpha = (isNuke ? 0.3 : 0.1) * flashAlpha;
                ctx.fillStyle = 'rgba(255,255,255,' + screenAlpha + ')';
                ctx.fillRect(0, 0, ARENA_W, ARENA_H);
            }

            // Phase 2: Fireball (50-300ms)
            if (elapsed > 50 && elapsed < 300) {
                var ft = (elapsed - 50) / 250;
                var fRadius = exp.radius * ft;
                var fAlpha = 1 - ft;

                var fireGrad = ctx.createRadialGradient(exp.x, exp.y, 0, exp.x, exp.y, fRadius);
                fireGrad.addColorStop(0, 'rgba(255,200,50,' + (fAlpha * 0.3) + ')');
                fireGrad.addColorStop(0.4, 'rgba(255,120,20,' + (fAlpha * 0.6) + ')');
                fireGrad.addColorStop(1, 'rgba(255,50,0,' + (fAlpha * 0.2) + ')');

                ctx.beginPath();
                ctx.arc(exp.x, exp.y, fRadius, 0, Math.PI * 2);
                ctx.fillStyle = fireGrad;
                ctx.fill();
            }

            // Phase 3: Shockwave ring (50-400ms)
            if (elapsed > 50 && elapsed < 400) {
                var st = (elapsed - 50) / 350;
                var sRadius = exp.radius * 1.5 * st;
                var sAlpha = 0.2 * (1 - st);

                ctx.beginPath();
                ctx.arc(exp.x, exp.y, sRadius, 0, Math.PI * 2);
                ctx.strokeStyle = 'rgba(255,200,150,' + sAlpha + ')';
                ctx.lineWidth = 3 * (1 - st);
                ctx.stroke();
            }

            // Phase 6: Crater glow (0-2000ms)
            if (elapsed < 2000) {
                var gAlpha = 0.3 * (1 - elapsed / 2000);
                var craterGrad = ctx.createRadialGradient(exp.x, exp.y, 0, exp.x, exp.y, exp.radius * 0.8);
                craterGrad.addColorStop(0, 'rgba(255,140,0,' + gAlpha + ')');
                craterGrad.addColorStop(1, 'rgba(255,80,0,0)');
                ctx.beginPath();
                ctx.arc(exp.x, exp.y, exp.radius * 0.8, 0, Math.PI * 2);
                ctx.fillStyle = craterGrad;
                ctx.fill();
            }

            return true;
        });
    }

    function drawParticles(timestamp) {
        state.particles = state.particles.filter(function (p) {
            // Handle delayed smoke
            if (p.delay) {
                var delayElapsed = timestamp - p.startTime;
                if (delayElapsed < p.delay) return true;
                p.delay = 0;
                p.life = 0.01;
            }

            // Smoke with grow then fade
            if (p.type === 'smoke') {
                if (p.decay < 0) {
                    p.life -= p.decay; // grows
                    if (p.life >= (p.maxLife || 0.8)) {
                        p.decay = 0.005; // start fading
                    }
                } else {
                    p.life -= p.decay;
                }
            } else {
                p.life -= p.decay;
            }

            if (p.life <= 0) return false;

            // Physics
            if (p.type === 'spark' || p.type === 'debris') {
                p.vy += 0.12; // gravity
            }
            if (p.type === 'ember') {
                p.vx += (Math.random() - 0.5) * 0.1; // wander
            }
            if (p.type === 'dirt') {
                p.vx *= 0.95; // friction
                p.vy += 0.08;
            }

            p.x += p.vx;
            p.y += p.vy;

            // Debris bounce
            if (p.type === 'debris' && !p.bounced && state.terrain) {
                var ix = Math.floor(p.x);
                if (ix >= 0 && ix < ARENA_W) {
                    var terrY = ARENA_H - state.terrain[ix];
                    if (p.y >= terrY) {
                        p.y = terrY;
                        p.vy = -Math.abs(p.vy) * 0.3;
                        p.vx *= 0.5;
                        p.bounced = true;
                    }
                }
            }

            // Smoke drifts and expands
            if (p.type === 'smoke') {
                p.vx += (Math.random() - 0.5) * 0.05;
                if (p.mushroom && p.y < p.y - 50) {
                    p.vx += (Math.random() - 0.5) * 0.3;
                }
            }

            // Draw particle
            ctx.globalAlpha = Math.min(1, p.life);

            if (p.type === 'spark') {
                // Motion blur line
                ctx.beginPath();
                ctx.moveTo(p.x - p.vx, p.y - p.vy);
                ctx.lineTo(p.x, p.y);
                ctx.strokeStyle = p.color;
                ctx.lineWidth = p.size;
                ctx.stroke();
            } else if (p.type === 'smoke') {
                var smokeSize = p.size * (2 - p.life);
                ctx.beginPath();
                ctx.arc(p.x, p.y, smokeSize, 0, Math.PI * 2);
                ctx.fillStyle = p.color;
                ctx.fill();
            } else if (p.type === 'ember') {
                // Flicker
                var flickerAlpha = 0.5 + 0.5 * Math.sin(timestamp * 0.01 + p.flicker);
                ctx.globalAlpha = p.life * flickerAlpha;
                ctx.beginPath();
                ctx.arc(p.x, p.y, p.size, 0, Math.PI * 2);
                ctx.fillStyle = p.color;
                ctx.fill();
            } else {
                // Debris, dirt
                ctx.beginPath();
                ctx.arc(p.x, p.y, p.size * p.life, 0, Math.PI * 2);
                ctx.fillStyle = p.color;
                ctx.fill();
            }

            ctx.globalAlpha = 1;
            return true;
        });
    }

    function drawDamageNumbers(timestamp) {
        state.damageNumbers = state.damageNumbers.filter(function (d) {
            d.life -= 0.008;
            d.y -= 0.8;

            if (d.life <= 0) return false;

            // Pop scale
            if (d.scale > 1) d.scale -= 0.015;
            if (d.scale < 1) d.scale = 1;

            ctx.save();
            ctx.translate(d.x, d.y);
            ctx.scale(d.scale, d.scale);
            ctx.globalAlpha = Math.min(1, d.life * 2);
            ctx.font = (d.isLabel ? '10' : '14') + 'px "Press Start 2P", monospace';
            ctx.textAlign = 'center';
            ctx.fillStyle = d.color;
            ctx.shadowColor = d.color;
            ctx.shadowBlur = d.directHit ? 15 : 8;
            ctx.fillText(d.text, 0, 0);
            ctx.shadowBlur = 0;
            ctx.restore();
            ctx.globalAlpha = 1;

            return true;
        });
    }

    function drawTimer(timestamp) {
        if (state.status === 2 || state.status === 3) { // WeaponSelect or Aiming
            var elapsed = timestamp - state.timerStart;
            var remaining = Math.max(0, (state.timerDuration - elapsed) / 1000);
            timerDisplayEl.textContent = Math.ceil(remaining) + 's';
            if (remaining < 5) timerDisplayEl.style.color = '#ff2200';
            else timerDisplayEl.style.color = '#ffaa00';
        }
    }

    // ===== START RENDER LOOP =====
    requestAnimationFrame(render);

})();
