document.addEventListener('DOMContentLoaded', async () => {

    // ── State Machine Core ────────────────────────────────────────────────────
    const STATES = [
        'Splash', 'PhoneInput', 'OtpInput', 'AuthSuccess',
        'VehicleSelect', 'DepositInfo', 'Idle', 'Active',
        'Success', 'ReturnScan', 'ReturnProcessing', 'ReturnReceipt',
        'Error'
    ];

    function goToState(name, payload = {}) {
        document.querySelectorAll('.state-container').forEach(el => el.style.display = 'none');
        const el = document.getElementById('paymentState_' + name);
        if (!el) { console.error('Missing state div: paymentState_' + name); return; }
        el.style.display = 'flex';
        currentState = name;
        onEnter[name]?.(payload);
    }

    let currentState = null;

    // ── Runtime State ─────────────────────────────────────────────────────────
    let kioskToken    = null;
    let userToken     = null;
    let currentRentalId = null;
    let timerInterval = null;
    let pollingInterval = null;
    let otpPhone      = null;

    // ── Element Refs ──────────────────────────────────────────────────────────
    const $ = id => document.getElementById(id);

    // ── State Entry Handlers ──────────────────────────────────────────────────
    const onEnter = {

        Splash: () => {
            $('btnTouchToStart')?.addEventListener('click', () => goToState('PhoneInput'), { once: true });
        },

        PhoneInput: () => {
            $('phoneInput').value = '';
            $('phoneError').textContent = '';
            $('btnSubmitPhone')?.addEventListener('click', async () => {
                const phone = $('phoneInput').value.trim();
                if (!phone.match(/^(0|\+84)\d{9}$/)) {
                    $('phoneError').textContent = 'Số điện thoại không hợp lệ.';
                    return;
                }
                otpPhone = phone;
                try {
                    await fetch('/api/auth/send-otp', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ phone })
                    });
                    goToState('OtpInput');
                } catch {
                    $('phoneError').textContent = 'Không thể gửi OTP. Thử lại.';
                }
            }, { once: true });
        },

        OtpInput: () => {
            $('otpError').textContent = '';
            $('btnSubmitOtp')?.addEventListener('click', async () => {
                const otp = $('otpInput').value.trim();
                try {
                    const res = await fetch('/api/auth/verify-otp', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ phone: otpPhone, otp })
                    });
                    const data = await res.json();
                    if (res.ok) {
                        userToken = data.token;
                        goToState('AuthSuccess', { userName: data.userName });
                    } else {
                        $('otpError').textContent = data.message || 'Mã OTP sai. Thử lại.';
                    }
                } catch {
                    $('otpError').textContent = 'Lỗi kết nối.';
                }
            }, { once: true });
        },

        AuthSuccess: ({ userName } = {}) => {
            if ($('authUserName')) $('authUserName').textContent = userName ?? '';
            setTimeout(() => goToState('VehicleSelect'), 2000);
        },

        VehicleSelect: () => {
            document.querySelectorAll('.vehicle-option-btn').forEach(btn => {
                btn.addEventListener('click', () => {
                    const vehicleId = parseInt(btn.dataset.vehicleId);
                    goToState('DepositInfo', { vehicleId });
                }, { once: true });
            });
        },

        DepositInfo: ({ vehicleId } = {}) => {
            // Store selected vehicle for rental start
            window._selectedVehicleId = vehicleId;
            $('btnConfirmDeposit')?.addEventListener('click', () => goToState('Idle'), { once: true });
        },

        // ── Original Idle → Active → Success flow, preserved ─────────────────
        Idle: () => {
            $('systemMessage').textContent = '';
            $('btnStartRental').disabled = false;
            $('btnStartRental').textContent = 'TẠO MÃ VIETQR';
            $('btnStartRental')?.addEventListener('click', handleStartRental, { once: true });
        },

        Active: ({ qrUrl, rentalId } = {}) => {
            $('qrImage').src = qrUrl ?? '';
            startCountdown(900);
            startPolling(rentalId);
            $('btnCancelRental')?.addEventListener('click', () => {
                stopAll();
                goToState('Idle');
            }, { once: true });
        },

        Success: ({ vehicleId, dockId } = {}) => {
            if ($('assignedVehicleId')) $('assignedVehicleId').textContent = vehicleId ?? 'N/A';
            if ($('assignedDockId'))   $('assignedDockId').textContent   = dockId   ?? 'N/A';
            setTimeout(() => goToState('Splash'), 30000); // reset after 30s
            $('btnDone')?.addEventListener('click', () => goToState('Splash'), { once: true });
        },

        ReturnScan: () => {
            $('bikeIdInput').value = '';
            $('returnError').textContent = '';
            $('btnSubmitReturn')?.addEventListener('click', async () => {
                const bikeCode = $('bikeIdInput').value.trim();
                if (!bikeCode) { $('returnError').textContent = 'Nhập mã xe.'; return; }
                try {
                    const res = await fetch('/api/rentals/return', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'Authorization': `Bearer ${userToken}`
                        },
                        body: JSON.stringify({ bikeCode })
                    });
                    const data = await res.json();
                    if (res.ok) {
                        goToState('ReturnProcessing', { rentalId: data.rentalId });
                    } else {
                        $('returnError').textContent = data.message || 'Không tìm thấy xe.';
                    }
                } catch {
                    $('returnError').textContent = 'Lỗi kết nối.';
                }
            }, { once: true });
        },

        ReturnProcessing: ({ rentalId } = {}) => {
            pollReturnStatus(rentalId);
        },

        ReturnReceipt: ({ summary } = {}) => {
            if ($('receiptBaseFare'))     $('receiptBaseFare').textContent     = fmt(summary?.baseFare);
            if ($('receiptDiscount'))     $('receiptDiscount').textContent     = fmt(summary?.discount);
            if ($('receiptFinalFare'))    $('receiptFinalFare').textContent    = fmt(summary?.finalFare);
            if ($('receiptDepositNote'))  $('receiptDepositNote').textContent  = summary?.depositNote ?? '';
            setTimeout(() => goToState('Splash'), 30000);
            $('btnReceiptDone')?.addEventListener('click', () => goToState('Splash'), { once: true });
        },

        Error: ({ message } = {}) => {
            if ($('errorMessage')) $('errorMessage').textContent = message ?? 'Đã xảy ra lỗi.';
            setTimeout(() => goToState('Splash'), 10000);
            $('btnErrorRetry')?.addEventListener('click', () => goToState('Splash'), { once: true });
        }
    };

    // ── Handlers ──────────────────────────────────────────────────────────────
    async function handleStartRental() {
        if (!kioskToken) return;
        $('btnStartRental').disabled = true;
        $('btnStartRental').textContent = 'ĐANG TẠO MÃ...';

        try {
            const res = await fetch('/api/rentals/start', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${kioskToken}`
                },
                body: JSON.stringify({ vehicleId: window._selectedVehicleId ?? 1, mode: 0 })
            });
            const data = await res.json();

            if (res.ok) {
                currentRentalId = data.rentalId;
                goToState('Active', { qrUrl: data.qrUrl, rentalId: data.rentalId });
            } else {
                $('systemMessage').textContent = data.message || 'Lỗi hệ thống.';
                goToState('Idle');
            }
        } catch {
            goToState('Error', { message: 'Không thể kết nối máy chủ.' });
        }
    }

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
                    goToState('Success', { vehicleId: data.vehicleCode, dockId: data.dockId });
                } else if (data.status === 'Cancelled') {
                    stopAll();
                    goToState('Error', { message: 'Giao dịch đã bị huỷ.' });
                }
            } catch { /* silent */ }
        }, 3000);
    }

    function pollReturnStatus(rentalId) {
        let attempts = 0;
        const interval = setInterval(async () => {
            attempts++;
            try {
                const res = await fetch(`/api/rentals/${rentalId}/return-status`, {
                    headers: { 'Authorization': `Bearer ${userToken}` }
                });
                if (!res.ok) return;
                const data = await res.json();

                if (data.status === 'Completed') {
                    clearInterval(interval);
                    goToState('ReturnReceipt', { summary: data.summary });
                }
            } catch { /* silent */ }

            if (attempts > 20) { // 60s timeout
                clearInterval(interval);
                goToState('Error', { message: 'Trả xe quá thời gian. Liên hệ kỹ thuật viên.' });
            }
        }, 3000);
    }

    // ── Countdown ─────────────────────────────────────────────────────────────
    function startCountdown(seconds) {
        clearInterval(timerInterval);
        let timer = seconds;
        timerInterval = setInterval(() => {
            const el = $('countdownTimer');
            if (el) el.textContent = `${String(Math.floor(timer / 60)).padStart(2,'0')}:${String(timer % 60).padStart(2,'0')}`;
            if (--timer < 0) {
                stopAll();
                goToState('Error', { message: 'Phiên giao dịch đã hết hạn. Vui lòng thử lại.' });
            }
        }, 1000);
    }

    function stopAll() {
        clearInterval(timerInterval);
        clearInterval(pollingInterval);
        timerInterval = pollingInterval = null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    const fmt = n => n != null ? n.toLocaleString('vi-VN') + ' VNĐ' : 'N/A';

    // ── Boot ──────────────────────────────────────────────────────────────────
    try {
        const res = await fetch('/api/auth/kiosk-token', { method: 'POST' });
        const data = await res.json();
        kioskToken = data.token;
    } catch {
        goToState('Error', { message: 'Lỗi kết nối hệ thống. Vui lòng liên hệ kỹ thuật viên.' });
        return;
    }

    goToState('Splash');
});