document.addEventListener('DOMContentLoaded', async () => {

    function goToState(name, payload = {}) {
        document.querySelectorAll('.state-container').forEach(el => el.style.display = 'none');
        const el = document.getElementById('paymentState_' + name);
        if (!el) { console.error('Missing state div: paymentState_' + name); return; }
        el.style.display = 'flex';
        currentState = name;
        resetIdleTimer(); // Reset idle timer on state change
        onEnter[name]?.(payload);
    }

    let currentState    = null;
    let kioskToken      = null;
    let userToken       = null;
    let currentRentalId = null;
    let currentDepositAmt = 0;   // tracked for Stripe checkout
    let timerInterval   = null;
    let pollingInterval = null;
    let otpPhone        = null;

    const $ = id => document.getElementById(id);

    // ── Touchscreen Numpad ───────────────────────────────────────────────────────
    document.addEventListener('click', e => {
        const key = e.target.closest('.numpad-key');
        if (!key) return;
        const input = document.getElementById(key.dataset.target);
        if (!input) return;
        const val = key.dataset.val;
        if (val === 'clear') input.value = '';
        else if (val === 'back') input.value = input.value.slice(0, -1);
        else if (input.value.length < parseInt(input.maxLength)) input.value += val;

        // Trigger input event for validation logic if needed elsewhere
        input.dispatchEvent(new Event('input', { bubbles: true }));
    });

    // ── Kiosk Idle Timer (Walk-Away Protection) ──────────────────────────────────
    let idleTimeout = null;
    const IDLE_LIMIT_MS = 60000; // 60 seconds

    function resetIdleTimer() {
        clearTimeout(idleTimeout);
        // Do not timeout if we are on the Splash screen, waiting for Stripe, or in the middle of a Return Process
        if (['Splash', 'Active', 'ReturnProcessing'].includes(currentState)) return;

        idleTimeout = setTimeout(() => {
            console.log('[SYSTEM] User idle timeout. Resetting kiosk.');
            stopAll();
            userToken = null; // Clear session
            goToState('Splash');
        }, IDLE_LIMIT_MS);
    }
    document.addEventListener('click', resetIdleTimer);
    document.addEventListener('touchstart', resetIdleTimer);

    // ── RFID / EasyCard Hardware Wedge ───────────────────────────────────────────
    let rfidBuffer = '';
    let rfidTimeout = null;

    document.addEventListener('keydown', (e) => {
        // USB RFID readers "type" the card number rapidly and press Enter
        if (e.key === 'Enter' && rfidBuffer.length >= 8) {
            const cardId = rfidBuffer;
            rfidBuffer = '';
            handleRfidLogin(cardId);
        } else if (/^\d$/.test(e.key)) {
            rfidBuffer += e.key;
            clearTimeout(rfidTimeout);
            // If more than 100ms passes between keystrokes, it's a human, not a scanner. Clear buffer.
            rfidTimeout = setTimeout(() => { rfidBuffer = ''; }, 100);
        }
    });

    async function handleRfidLogin(cardId) {
        // Only allow RFID tap on welcome/auth screens
        if (!['Splash', 'PhoneInput', 'OtpInput'].includes(currentState)) return;

        try {
            console.log(`[RFID] Processing tap: ${cardId}`);
            // Show loading on splash or disable buttons
            const res = await fetch('/api/auth/rfid', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ rfidId: cardId })
            });
            const data = await res.json();

            if (res.ok) {
                userToken = data.token;
                goToState('AuthSuccess', { userName: data.userName });
            } else {
                goToState('Error', { message: data.message || 'Thẻ không hợp lệ hoặc chưa đăng ký.' });
            }
        } catch {
            goToState('Error', { message: 'Lỗi đọc thẻ. Vui lòng thử lại.' });
        }
    }

    // ── State Dictionary ─────────────────────────────────────────────────────────
    const onEnter = {

        Splash: () => {
            $('btnTouchToStart')?.addEventListener('click', () => goToState('PhoneInput'), { once: true });
            $('btnGoToReturn')?.addEventListener('click', () => goToState('ReturnScan'), { once: true });
        },

        PhoneInput: () => {
            $('phoneInput').value = '';
            $('phoneError').textContent = '';
            $('btnSubmitPhone')?.addEventListener('click', async () => {
                const phone = $('phoneInput').value.trim();
                if (!phone.match(/^(0|\+84)\d{8,10}$/)) {
                    $('phoneError').textContent = 'Số điện thoại không hợp lệ.';
                    return;
                }
                otpPhone = phone;
                try {
                    const res = await fetch('/api/auth/send-otp', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ phone })
                    });
                    if (!res.ok) {
                        const data = await res.json();
                        $('phoneError').textContent = data.message || 'Không thể gửi OTP.';
                        return;
                    }
                    goToState('OtpInput');
                } catch {
                    $('phoneError').textContent = 'Không thể gửi OTP. Thử lại.';
                }
            }, { once: true });
        },

        OtpInput: () => {
            $('otpInput').value = '';
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

        VehicleSelect: async () => {
            const list = document.querySelector('.vehicle-option-list');
            list.innerHTML = '<p class="section-subtitle">Đang tải danh sách xe...</p>';
            try {
                const res = await fetch('/api/vehicles');
                if (!res.ok) throw new Error(`HTTP ${res.status}`);
                const vehicles = await res.json();

                if (!vehicles.length) {
                    list.innerHTML = '<p class="error-msg">Không có xe khả dụng tại trạm này.</p>';
                    return;
                }

                list.innerHTML = vehicles.map(v => {
                    const depositRate = v.grade === 2 ? 0.20 : v.grade === 1 ? 0.15 : 0.10;
                    const deposit = (v.marketValue * depositRate).toLocaleString('vi-VN');
                    const icon = v.grade === 2 ? '🛵' : v.grade === 1 ? '⚡' : '🚲';
                    const gradeName = ['C', 'B', 'A'][v.grade] ?? '?';

                    return `<button class="vehicle-option-btn" data-vehicle-id="${v.id}" data-market-value="${v.marketValue}" data-grade="${v.grade}" data-hourly-rate="${v.hourlyRate}" data-name="${v.name}">
                        <span class="vehicle-icon">${icon}</span>
                        <div class="vehicle-option-info">
                            <span class="vehicle-option-name">${v.name}</span>
                            <span class="vehicle-option-desc">Grade ${gradeName} — Phí cọc: ${deposit} VNĐ</span>
                        </div>
                        <span class="vehicle-option-arrow">&rarr;</span>
                    </button>`;
                }).join('');

                document.querySelectorAll('.vehicle-option-btn').forEach(btn => {
                    btn.addEventListener('click', () => {
                        goToState('DepositInfo', {
                            vehicleId:   parseInt(btn.dataset.vehicleId),
                            marketValue: parseFloat(btn.dataset.marketValue),
                            grade:       parseInt(btn.dataset.grade),
                            hourlyRate:  parseFloat(btn.dataset.hourlyRate),
                            name:        btn.dataset.name
                        });
                    }, { once: true });
                });
            } catch (err) {
                list.innerHTML = `<p class="error-msg">Không thể tải danh sách xe: ${err.message}</p>`;
            }
        },

        DepositInfo: ({ vehicleId, marketValue, grade, hourlyRate, name } = {}) => {
            window._selectedVehicleId = vehicleId;
            const depositRate = grade === 2 ? 0.20 : grade === 1 ? 0.15 : 0.10;
            const depositAmt  = Math.round(marketValue * depositRate);
            currentDepositAmt = depositAmt;  // store for Stripe

            const rows = document.querySelectorAll('#paymentState_DepositInfo .deposit-row');
            if (rows[0]) rows[0].querySelector('.deposit-value').textContent = hourlyRate.toLocaleString('vi-VN') + ' VNĐ';
            if (rows[1]) rows[1].querySelector('.deposit-value').textContent = depositAmt.toLocaleString('vi-VN') + ' VNĐ';
            $('btnConfirmDeposit')?.addEventListener('click', () => goToState('Idle'), { once: true });
        },

        Idle: ({ error } = {}) => {
            if ($('systemMessage')) $('systemMessage').textContent = error ?? '';

            const actionArea = $('idleActionArea');
            if (actionArea) {
                actionArea.innerHTML = `
                    <button id="btnVietQR" class="action-btn btn-primary" style="margin-bottom:12px;">🇻🇳 VietQR (Nội địa)</button>
                    <button id="btnStripe" class="action-btn btn-secondary">💳 Thẻ quốc tế</button>
                `;
                $('btnVietQR').addEventListener('click', handleStartRental, { once: true });
                $('btnStripe').addEventListener('click', handleStripeCheckout, { once: true });
            }
        },

        Active: ({ qrUrl, rentalId } = {}) => {
            $('qrImage').src = qrUrl ?? '';
            // If it's a Stripe waiting screen, we can hide the QR image entirely
            if (!qrUrl) $('qrImage').style.display = 'none';
            else $('qrImage').style.display = 'block';

            startCountdown(900); // 15 minutes to pay
            startPolling(rentalId);
            $('btnCancelRental')?.addEventListener('click', () => {
                stopAll();
                goToState('Idle');
            }, { once: true });
        },

        Success: ({ vehicleId, dockId } = {}) => {
            if ($('assignedVehicleId')) $('assignedVehicleId').textContent = vehicleId ?? 'N/A';
            if ($('assignedDockId'))    $('assignedDockId').textContent    = dockId    ?? 'N/A';
            setTimeout(() => goToState('Splash'), 30000);
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
                        headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${userToken}` },
                        body: JSON.stringify({ bikeCode })
                    });
                    const data = await res.json();
                    if (res.ok) goToState('ReturnProcessing', { rentalId: data.rentalId });
                    else $('returnError').textContent = data.message || 'Không tìm thấy xe.';
                } catch {
                    $('returnError').textContent = 'Lỗi kết nối.';
                }
            }, { once: true });
        },

        ReturnProcessing: ({ rentalId } = {}) => { pollReturnStatus(rentalId); },

        ReturnReceipt: ({ summary } = {}) => {
            if ($('receiptBaseFare'))    $('receiptBaseFare').textContent    = fmt(summary?.baseFare);
            if ($('receiptDiscount'))    $('receiptDiscount').textContent    = fmt(summary?.discount);
            if ($('receiptFinalFare'))   $('receiptFinalFare').textContent   = fmt(summary?.finalFare);
            if ($('receiptDepositNote')) $('receiptDepositNote').textContent = summary?.depositNote ?? '';
            setTimeout(() => goToState('Splash'), 30000);
            $('btnReceiptDone')?.addEventListener('click', () => goToState('Splash'), { once: true });
        },

        Error: ({ message } = {}) => {
            if ($('errorMessage')) $('errorMessage').textContent = message ?? 'Đã xảy ra lỗi.';
            setTimeout(() => goToState('Splash'), 10000);
            $('btnErrorRetry')?.addEventListener('click', () => goToState('Splash'), { once: true });
        }
    };

    // ── Payment Flows ────────────────────────────────────────────────────────────

    async function handleStartRental() {
        const btn = $('btnVietQR') ?? $('btnStartRental');
        if (btn) { btn.disabled = true; btn.textContent = 'ĐANG TẠO MÃ...'; }

        try {
            const res = await fetch('/api/rentals/start', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${userToken}` },
                body: JSON.stringify({ vehicleId: window._selectedVehicleId, mode: 0 })
            });
            const data = await res.json();
            console.log('[START] response:', JSON.stringify(data));
            if (res.ok) {
                currentRentalId = data.rentalId;
                goToState('Active', { qrUrl: data.qrUrl, rentalId: data.rentalId });
            } else {
                if ($('systemMessage')) $('systemMessage').textContent = data.message || 'Lỗi hệ thống.';
                goToState('Idle');
            }
        } catch {
            goToState('Error', { message: 'Không thể kết nối máy chủ.' });
        }
    }

    async function handleStripeCheckout() {
        const btn = $('btnStripe');
        if (btn) { btn.disabled = true; btn.textContent = 'ĐANG XỬ LÝ...'; }

        try {
            const startRes = await fetch('/api/rentals/start', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${userToken}` },
                body: JSON.stringify({ vehicleId: window._selectedVehicleId, mode: 0 })
            });
            const startData = await startRes.json();
            if (!startRes.ok) {
                goToState('Error', { message: startData.message || 'Không thể tạo thuê xe.' });
                return;
            }
            currentRentalId = startData.rentalId;

            const checkoutRes = await fetch('/api/payment/stripe/create-checkout', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${userToken}` },
                body: JSON.stringify({
                    rentalId:         currentRentalId,
                    depositAmountVnd: currentDepositAmt,
                    baseUrl:          window.location.origin
                })
            });
            const checkoutData = await checkoutRes.json();
            if (!checkoutRes.ok) {
                goToState('Error', { message: checkoutData.error || 'Không thể tạo phiên thanh toán.' });
                return;
            }

            // Redirect kiosk to Stripe hosted checkout page
            window.location.href = checkoutData.url;

        } catch {
            goToState('Error', { message: 'Không thể kết nối Stripe.' });
        }
    }

    // ── Polling & Timers ─────────────────────────────────────────────────────────

    function startPolling(rentalId) {
        console.log('[POLL] starting for rentalId:', rentalId);
        pollingInterval = setInterval(async () => {
            try {
                const res = await fetch(`/api/rentals/${rentalId}/status`, {
                    headers: { 'Authorization': `Bearer ${userToken}` }
                });
                if (!res.ok) return;
                const data = await res.json();
                if (data.status === 'Active') {
                    stopAll();
                    goToState('Success', { vehicleId: data.vehicleCode, dockId: data.dockId });
                } else if (data.status === 'Cancelled') {
                    stopAll();
                    goToState('Error', { message: 'Giao dịch đã bị hủy.' });
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
            if (attempts > 20) {
                clearInterval(interval);
                goToState('Error', { message: 'Trả xe quá thời gian. Liên hệ kỹ thuật viên.' });
            }
        }, 3000);
    }

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

    const fmt = n => n != null ? n.toLocaleString('vi-VN') + ' VNĐ' : 'N/A';

    // ── Boot ─────────────────────────────────────────────────────────────────────

    try {
        const res = await fetch('/api/auth/kiosk-token', { method: 'POST' });
        const data = await res.json();
        kioskToken = data.token;
    } catch {
        goToState('Error', { message: 'Lỗi kết nối hệ thống. Vui lòng liên hệ kỹ thuật viên.' });
        return;
    }

    // Handle redirect back from Stripe Checkout
    const urlParams  = new URLSearchParams(window.location.search);
    const stripeSession = urlParams.get('stripe_session');
    const stripeRentalId = urlParams.get('rental_id');
    const stripeCancelled = urlParams.get('stripe_cancelled');

    if (stripeSession && stripeRentalId) {
        history.replaceState({}, '', '/Kiosk');
        currentRentalId = stripeRentalId;
        goToState('Active', { qrUrl: '', rentalId: stripeRentalId });
    } else if (stripeCancelled && stripeRentalId) {
        history.replaceState({}, '', '/Kiosk');
        goToState('Idle', { error: 'Thanh toán bị hủy. Vui lòng thử lại.' });
    } else {
        goToState('Splash');
    }
});