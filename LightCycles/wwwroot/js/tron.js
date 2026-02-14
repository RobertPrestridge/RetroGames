(function () {
    'use strict';

    var config = window.tronConfig;
    if (!config) return;

    // --- DOM elements ---
    var canvas = document.getElementById('tronCanvas');
    var ctx = canvas.getContext('2d');
    var p1NameEl = document.getElementById('p1Name');
    var p2NameEl = document.getElementById('p2Name');
    var statusEl = document.getElementById('gameStatus');
    var countdownOverlay = document.getElementById('countdownOverlay');
    var countdownText = document.getElementById('countdownText');
    var gameOverOverlay = document.getElementById('gameOverOverlay');
    var gameOverText = document.getElementById('gameOverText');
    var waitingOverlay = document.getElementById('waitingOverlay');
    var shareLinkEl = document.getElementById('shareLink');
    var copyBtn = document.getElementById('copyBtn');
    var newGameBtn = document.getElementById('newGameBtn');

    // --- Constants ---
    var CELL_SIZE = 13;
    var GRID_WIDTH = 60;
    var GRID_HEIGHT = 40;
    var TRAIL_WIDTH = 3;
    var TRAIL_GLOW = 8;
    var HEAD_RADIUS = 3;
    var HEAD_GLOW = 20;
    var P1_COLOR = '#00ffff';
    var P1_TRAIL_COLOR = 'rgba(0, 255, 255, 1.0)';
    var P1_GLOW = 'rgba(0, 255, 255, 0.8)';
    var P2_COLOR = '#ff00ff';
    var P2_TRAIL_COLOR = 'rgba(255, 0, 255, 1.0)';
    var P2_GLOW = 'rgba(255, 0, 255, 0.8)';
    var HEAD_COLOR = '#ffffff';
    var GRID_LINE_COLOR = 'rgba(30, 30, 80, 0.5)';
    var BG_COLOR = '#0a0a1a';

    // --- State ---
    var grid = null;   // 2D array [x][y] = 0/1/2
    var p1 = null;
    var p2 = null;
    var playerNumber = 0;
    var gameStatus = -1;
    var lastDirection = null;

    // --- Canvas setup ---
    function initCanvas() {
        canvas.width = GRID_WIDTH * CELL_SIZE;
        canvas.height = GRID_HEIGHT * CELL_SIZE;
    }

    function initGrid() {
        grid = [];
        for (var x = 0; x < GRID_WIDTH; x++) {
            grid[x] = [];
            for (var y = 0; y < GRID_HEIGHT; y++) {
                grid[x][y] = 0;
            }
        }
    }

    // --- Rendering ---
    function render() {
        if (!grid) return;

        // Background
        ctx.fillStyle = BG_COLOR;
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        // Grid lines
        ctx.strokeStyle = GRID_LINE_COLOR;
        ctx.lineWidth = 0.5;
        for (var x = 0; x <= GRID_WIDTH; x++) {
            ctx.beginPath();
            ctx.moveTo(x * CELL_SIZE, 0);
            ctx.lineTo(x * CELL_SIZE, canvas.height);
            ctx.stroke();
        }
        for (var y = 0; y <= GRID_HEIGHT; y++) {
            ctx.beginPath();
            ctx.moveTo(0, y * CELL_SIZE);
            ctx.lineTo(canvas.width, y * CELL_SIZE);
            ctx.stroke();
        }

        // Trails — thin connected lines between cell centers
        ctx.lineWidth = TRAIL_WIDTH;
        ctx.lineCap = 'square';

        // Draw P1 trails
        ctx.strokeStyle = P1_TRAIL_COLOR;
        ctx.fillStyle = P1_TRAIL_COLOR;
        ctx.shadowColor = P1_GLOW;
        ctx.shadowBlur = TRAIL_GLOW;
        for (var x = 0; x < GRID_WIDTH; x++) {
            for (var y = 0; y < GRID_HEIGHT; y++) {
                if (grid[x][y] !== 1) continue;
                var cx = x * CELL_SIZE + CELL_SIZE / 2;
                var cy = y * CELL_SIZE + CELL_SIZE / 2;
                if (x + 1 < GRID_WIDTH && grid[x + 1][y] === 1) {
                    ctx.beginPath();
                    ctx.moveTo(cx, cy);
                    ctx.lineTo(cx + CELL_SIZE, cy);
                    ctx.stroke();
                }
                if (y + 1 < GRID_HEIGHT && grid[x][y + 1] === 1) {
                    ctx.beginPath();
                    ctx.moveTo(cx, cy);
                    ctx.lineTo(cx, cy + CELL_SIZE);
                    ctx.stroke();
                }
                ctx.fillRect(cx - TRAIL_WIDTH / 2, cy - TRAIL_WIDTH / 2, TRAIL_WIDTH, TRAIL_WIDTH);
            }
        }

        // Draw P2 trails
        ctx.strokeStyle = P2_TRAIL_COLOR;
        ctx.fillStyle = P2_TRAIL_COLOR;
        ctx.shadowColor = P2_GLOW;
        ctx.shadowBlur = TRAIL_GLOW;
        for (var x = 0; x < GRID_WIDTH; x++) {
            for (var y = 0; y < GRID_HEIGHT; y++) {
                if (grid[x][y] !== 2) continue;
                var cx = x * CELL_SIZE + CELL_SIZE / 2;
                var cy = y * CELL_SIZE + CELL_SIZE / 2;
                if (x + 1 < GRID_WIDTH && grid[x + 1][y] === 2) {
                    ctx.beginPath();
                    ctx.moveTo(cx, cy);
                    ctx.lineTo(cx + CELL_SIZE, cy);
                    ctx.stroke();
                }
                if (y + 1 < GRID_HEIGHT && grid[x][y + 1] === 2) {
                    ctx.beginPath();
                    ctx.moveTo(cx, cy);
                    ctx.lineTo(cx, cy + CELL_SIZE);
                    ctx.stroke();
                }
                ctx.fillRect(cx - TRAIL_WIDTH / 2, cy - TRAIL_WIDTH / 2, TRAIL_WIDTH, TRAIL_WIDTH);
            }
        }
        ctx.shadowBlur = 0;

        // Player heads (bright glow)
        if (p1 && p1.alive) {
            drawHead(p1.x, p1.y, P1_COLOR, P1_GLOW);
        }
        if (p2 && p2.alive) {
            drawHead(p2.x, p2.y, P2_COLOR, P2_GLOW);
        }
    }

    function drawHead(x, y, color, glowColor) {
        var cx = x * CELL_SIZE + CELL_SIZE / 2;
        var cy = y * CELL_SIZE + CELL_SIZE / 2;

        // Glow
        ctx.shadowColor = glowColor;
        ctx.shadowBlur = HEAD_GLOW;

        // Outer white circle
        ctx.fillStyle = HEAD_COLOR;
        ctx.beginPath();
        ctx.arc(cx, cy, HEAD_RADIUS, 0, 2 * Math.PI);
        ctx.fill();

        // Inner colored circle
        ctx.fillStyle = color;
        ctx.beginPath();
        ctx.arc(cx, cy, HEAD_RADIUS - 1, 0, 2 * Math.PI);
        ctx.fill();

        ctx.shadowBlur = 0;
    }

    // --- SignalR ---
    var connection = new signalR.HubConnectionBuilder()
        .withUrl(config.hubUrl)
        .withAutomaticReconnect()
        .build();

    connection.on('GameState', function (state) {
        GRID_WIDTH = state.gridWidth;
        GRID_HEIGHT = state.gridHeight;
        playerNumber = state.playerNumber;
        gameStatus = state.status;

        initCanvas();
        initGrid();

        // Apply existing trails
        if (state.trails) {
            state.trails.forEach(function (t) {
                grid[t.x][t.y] = t.player;
            });
        }

        p1 = state.player1;
        if (state.player2) {
            p2 = state.player2;
        }

        // Update HUD
        p1NameEl.textContent = p1 ? p1.name : '---';
        if (p2) {
            p2NameEl.textContent = p2.name;
        }

        if (state.status === 0) {
            // Waiting
            statusEl.textContent = 'WAITING FOR OPPONENT';
            waitingOverlay.style.display = 'flex';
            shareLinkEl.value = window.location.href;
        } else if (state.status === 1) {
            // Countdown
            statusEl.textContent = 'GET READY';
            waitingOverlay.style.display = 'none';
        } else if (state.status === 2) {
            // InProgress
            statusEl.textContent = 'FIGHT!';
            waitingOverlay.style.display = 'none';
            countdownOverlay.style.display = 'none';
        }

        render();
    });

    connection.on('OpponentJoined', function (data) {
        p2NameEl.textContent = data.opponentName;
        waitingOverlay.style.display = 'none';
        statusEl.textContent = 'GET READY';
    });

    connection.on('Countdown', function (data) {
        if (data.seconds === 0) {
            countdownText.textContent = 'GO!';
            countdownOverlay.style.display = 'flex';
            statusEl.textContent = 'FIGHT!';
            setTimeout(function () {
                countdownOverlay.style.display = 'none';
            }, 500);
        } else {
            countdownText.textContent = data.seconds;
            countdownOverlay.style.display = 'flex';
            statusEl.textContent = 'GET READY';
        }
    });

    connection.on('Tick', function (data) {
        // Update trails
        if (data.newTrails) {
            data.newTrails.forEach(function (t) {
                grid[t.x][t.y] = t.player;
            });
        }

        // Update player positions
        if (data.p1) {
            p1.x = data.p1.x;
            p1.y = data.p1.y;
            p1.alive = data.p1.alive;
        }
        if (data.p2) {
            p2.x = data.p2.x;
            p2.y = data.p2.y;
            p2.alive = data.p2.alive;
        }

        render();
    });

    connection.on('GameOver', function (data) {
        gameStatus = data.status;
        var isP1 = playerNumber === 1;

        var resultText = '';
        var resultColor = '';

        if (data.status === 5) {
            // Draw
            resultText = 'DRAW!';
            resultColor = 'var(--neon-yellow)';
        } else if ((data.status === 3 && isP1) || (data.status === 4 && !isP1)) {
            // This player wins
            resultText = 'YOU WIN!';
            resultColor = 'var(--neon-green)';
        } else {
            resultText = 'GAME OVER';
            resultColor = 'var(--neon-red)';
        }

        gameOverText.textContent = resultText;
        gameOverText.style.color = resultColor;
        gameOverOverlay.style.display = 'flex';
        statusEl.textContent = resultText;
    });

    connection.on('Error', function (message) {
        statusEl.textContent = 'ERROR: ' + message;
    });

    connection.onreconnecting(function () {
        statusEl.textContent = 'RECONNECTING...';
    });

    connection.onreconnected(function () {
        statusEl.textContent = 'RECONNECTED';
        connection.invoke('JoinGame', config.shortCode, config.playerName, config.sessionId);
    });

    // --- Input handling ---
    var directionMap = {
        'ArrowUp': 'up', 'ArrowDown': 'down', 'ArrowLeft': 'left', 'ArrowRight': 'right',
        'w': 'up', 'W': 'up', 's': 'down', 'S': 'down',
        'a': 'left', 'A': 'left', 'd': 'right', 'D': 'right'
    };

    var opposites = { 'up': 'down', 'down': 'up', 'left': 'right', 'right': 'left' };

    document.addEventListener('keydown', function (e) {
        var dir = directionMap[e.key];
        if (!dir) return;

        e.preventDefault();

        // Don't send same direction twice
        if (dir === lastDirection) return;

        // Don't allow 180 reversal
        if (opposites[dir] === lastDirection) return;

        lastDirection = dir;
        connection.invoke('ChangeDirection', config.shortCode, dir);
    });

    // --- Copy share link ---
    if (copyBtn) {
        copyBtn.addEventListener('click', function () {
            shareLinkEl.select();
            navigator.clipboard.writeText(shareLinkEl.value).then(function () {
                copyBtn.textContent = 'COPIED!';
                setTimeout(function () { copyBtn.textContent = 'COPY'; }, 2000);
            });
        });
    }

    // --- New game ---
    if (newGameBtn) {
        newGameBtn.addEventListener('click', function () {
            window.location.href = config.homePath;
        });
    }

    // --- Init ---
    initCanvas();
    initGrid();
    render();

    connection.start()
        .then(function () {
            connection.invoke('JoinGame', config.shortCode, config.playerName, config.sessionId);
        })
        .catch(function (err) {
            statusEl.textContent = 'CONNECT FAILED';
            console.error('SignalR connect error:', err);
        });
})();
