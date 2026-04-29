document.addEventListener('DOMContentLoaded', async () => {
    const btnStartRental = document.getElementById('btnStartRental');
    const systemMessage = document.getElementById('systemMessage');
    const stateIdle = document.getElementById('paymentState_Idle');
    const stateActive = document.getElementById('paymentState_Active');
    const stateSuccess = document.getElementById('paymentState_Success');
    const qrImage = document.getElementById('qrImage');
    const countdownTimer = document.getElementById('countdownTimer');

    let timerInterval = null;
    let pollingInterval = null;
    let kioskToken = null;
    let currentRentalId = null;

    // ── Auth ──────────────────────────────────────────────────────────────────
    try {
        const res = await fetch('/api/auth/kiosk-token', { method: 'POST' });
        const data = await res.json();
        kioskToken = data.token;
    } catch {
        systemMessage.innerText = "Lỗi kết nối hệ thống. Vui lòng liên hệ kỹ thuật viên.";
        btnStartRental.disabled = true;
    }

    // ── Start Rental ──────────────────────────────────────────────────────────
    btnStartRental.addEventListener('click', async () => {
        if (!kioskToken) return;

        btnStartRental.disabled = true;
        btnStartRental.innerText = "ĐANG TẠO MÃ...";
        systemMessage.innerText = "";

        try {
            const res = await fetch('/api/rentals/start', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${kioskToken}`
                },
                body: JSON.stringify({ vehicleId: 1, mode: 0 })
            });

            const data = await res.json();

            if (res.ok) {
                currentRentalId = data.rentalId;
                stateIdle.style.display = 'none';
                stateActive.style.display = 'block';
                qrImage.src = data.qrUrl;
                startCountdown(900);
                startPolling(data.rentalId);
                btnStartRental.innerText = "ĐÃ TẠO MÃ THANH TOÁN";
            } else {
                systemMessage.innerText = data.message || "Lỗi hệ thống.";
                resetButton();
            }
        } catch {
            systemMessage.innerText = "Không thể kết nối máy chủ.";
            resetButton();
        }
    });

    // ── Polling ───────────────────────────────────────────────────────────────
    function startPolling(rentalId) {
        pollingInterval = setInterval(async () => {
            try {
                const res = await fetch(`/api/rentals/${rentalId}/status`, {
                    headers: { 'Authorization': `Bearer ${kioskToken}` }
                });
                if (!res.ok) return;

                const data = await res.json();

                if (data.status === 'Active') {
                    stopAll();
                    showSuccess();
                } else if (data.status === 'Cancelled') {
                    stopAll();
                    showExpired("Giao dịch đã bị huỷ.");
                }
            } catch {
                // silent — keep polling
            }
        }, 3000);
    }

    // ── Countdown ─────────────────────────────────────────────────────────────
    function startCountdown(seconds) {
        let timer = seconds;
        clearInterval(timerInterval);

        timerInterval = setInterval(() => {
            const m = String(Math.floor(timer / 60)).padStart(2, '0');
            const s = String(timer % 60).padStart(2, '0');
            countdownTimer.textContent = `${m}:${s}`;

            if (--timer < 0) {
                stopAll();
                showExpired("Phiên giao dịch đã hết hạn. Vui lòng thử lại.");
            }
        }, 1000);
    }

    // ── UI States ─────────────────────────────────────────────────────────────
    function showSuccess() {
        stateActive.style.display = 'none';
        if (stateSuccess) stateSuccess.style.display = 'block';
        // Auto-reset về Idle sau 5 giây
        setTimeout(() => resetToIdle(), 5000);
    }

    function showExpired(msg) {
        qrImage.style.opacity = '0.2';
        countdownTimer.textContent = "HẾT HẠN";
        systemMessage.innerText = msg;
        resetButton();
        setTimeout(() => resetToIdle(), 5000);
    }

    function resetToIdle() {
        stateActive.style.display = 'none';
        if (stateSuccess) stateSuccess.style.display = 'none';
        stateIdle.style.display = 'block';
        qrImage.src = '';
        qrImage.style.opacity = '1';
        systemMessage.innerText = '';
        currentRentalId = null;
        resetButton();
    }

    function resetButton() {
        btnStartRental.disabled = false;
        btnStartRental.innerText = "BẮT ĐẦU THUÊ XE";
    }

    function stopAll() {
        clearInterval(timerInterval);
        clearInterval(pollingInterval);
        timerInterval = null;
        pollingInterval = null;
    }
});