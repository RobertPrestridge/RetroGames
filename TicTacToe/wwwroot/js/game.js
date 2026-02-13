(function () {
    'use strict';

    const config = window.gameConfig;
    if (!config) return;

    const statusEl = document.getElementById('status');
    const shareSection = document.getElementById('share-section');
    const shareLinkEl = document.getElementById('shareLink');
    const copyBtn = document.getElementById('copyBtn');
    const turnIndicator = document.getElementById('turn-indicator');
    const boardEl = document.getElementById('board');
    const cells = document.querySelectorAll('.cell');
    const gameOverSection = document.getElementById('game-over-section');
    const newGameBtn = document.getElementById('newGameBtn');

    let playerMark = null;
    let currentTurn = null;
    let gameActive = false;

    // SignalR connection
    const connection = new signalR.HubConnectionBuilder()
        .withUrl(config.hubUrl || '/hubs/tictactoe')
        .withAutomaticReconnect()
        .build();

    // --- Server → Client handlers ---

    connection.on('GameState', function (state) {
        playerMark = state.playerMark;
        currentTurn = state.currentTurn;
        updateBoard(state.board);

        if (state.status === 0) {
            // Waiting for opponent
            setStatus('WAITING FOR OPPONENT...', 'info');
            showShareLink();
            gameActive = false;
        } else if (state.status === 1) {
            // In progress
            gameActive = true;
            hideShareLink();
            updateTurnIndicator();
        } else {
            // Game over
            gameActive = false;
            showGameResult(state.status);
        }
    });

    connection.on('OpponentJoined', function () {
        gameActive = true;
        hideShareLink();
        setStatus('OPPONENT JOINED! FIGHT!', 'success');
        updateTurnIndicator();
    });

    connection.on('MoveMade', function (data) {
        const mark = data.mark;
        currentTurn = data.currentTurn;
        const cell = cells[data.position];
        cell.textContent = mark;
        cell.classList.add('played', 'mark-' + mark);
        updateTurnIndicator();
    });

    connection.on('GameOver', function (data) {
        gameActive = false;
        if (data.winLine) {
            data.winLine.forEach(function (idx) {
                cells[idx].classList.add('winning');
            });
        }
        showGameResult(data.status);
        gameOverSection.style.display = 'block';
    });

    connection.on('Error', function (message) {
        setStatus(message, 'danger');
    });

    connection.onreconnecting(function () {
        setStatus('CONNECTION LOST. RECONNECTING...', 'warning');
    });

    connection.onreconnected(function () {
        setStatus('RECONNECTED!', 'success');
        connection.invoke('JoinGame', config.shortCode, config.sessionId);
    });

    // --- Board click handler ---

    cells.forEach(function (cell) {
        cell.addEventListener('click', function () {
            if (!gameActive) return;
            if (String(playerMark) !== String(currentTurn)) return;
            if (cell.textContent.trim() !== '') return;

            const index = parseInt(cell.getAttribute('data-index'));
            connection.invoke('MakeMove', config.shortCode, index);
        });
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
            window.location.href = config.homePath || '/tictactoe';
        });
    }

    // --- Helpers ---

    function updateBoard(boardState) {
        for (var i = 0; i < 9; i++) {
            var ch = boardState[i];
            if (ch !== ' ') {
                cells[i].textContent = ch;
                cells[i].classList.add('played', 'mark-' + ch);
            } else {
                cells[i].textContent = '';
                cells[i].classList.remove('played', 'mark-X', 'mark-O');
            }
        }
    }

    function updateTurnIndicator() {
        turnIndicator.style.display = 'block';
        if (String(playerMark) === String(currentTurn)) {
            turnIndicator.textContent = '> YOUR TURN (' + playerMark + ') <';
            turnIndicator.className = 'turn-indicator mb-3 turn-yours';
            boardEl.classList.add('my-turn');
            boardEl.classList.remove('waiting-turn');
        } else {
            turnIndicator.textContent = "OPPONENT'S TURN (" + currentTurn + ')';
            turnIndicator.className = 'turn-indicator mb-3 turn-opponent';
            boardEl.classList.remove('my-turn');
            boardEl.classList.add('waiting-turn');
        }
    }

    function showGameResult(status) {
        turnIndicator.style.display = 'none';
        boardEl.classList.remove('my-turn', 'waiting-turn');
        var mark = String(playerMark);
        if (status === 4) {
            setStatus('DRAW GAME!', 'secondary');
        } else if ((status === 2 && mark === 'X') || (status === 3 && mark === 'O')) {
            setStatus('YOU WIN!', 'success');
        } else {
            setStatus('GAME OVER - YOU LOSE!', 'danger');
        }
        gameOverSection.style.display = 'block';
    }

    function setStatus(message, type) {
        statusEl.textContent = message;
        statusEl.className = 'retro-status retro-status-' + type + ' mt-3';
    }

    function showShareLink() {
        shareSection.style.display = 'block';
        shareLinkEl.value = window.location.href;
    }

    function hideShareLink() {
        shareSection.style.display = 'none';
    }

    // --- Start connection ---

    connection.start()
        .then(function () {
            connection.invoke('JoinGame', config.shortCode, config.sessionId);
        })
        .catch(function (err) {
            setStatus('CONNECT FAILED: ' + err.toString(), 'danger');
        });
})();
