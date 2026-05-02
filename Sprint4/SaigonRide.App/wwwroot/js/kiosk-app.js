document.addEventListener('DOMContentLoaded', async () => {

    function goToState(name, payload = {}) {
        document.querySelectorAll('.state-container').forEach(el => el.style.display = 'none');
        const el = document.getElementById('paymentState_' + name);
        if (!el) { console.error('Missing state div: paymentState_' + name); return; }
        el.style.display = 'flex';
        currentState = name;
        onEnter[name]?.(payload);
    }

    let currentState = null;
    let kioskToken      = null;
    let userToken       = null;
    let currentRentalId = null;
    let timerInterval   = null;
    let pollingInterval = null;
    let otpPhone        = null;

    const $ = id => document.getElementById(id);

    document.addEventListener('click', e => {
        const key = e.target.closest('.numpad-key');
        if (!key) return;
        const input = document.getElementById(key.dataset.target);
        if (!input) return;
        const val = key.dataset.val;
        if (val === 'clear') input.value = '';
        else if (val === 'back') input.value = input.value.slice(0, -1);
        else if (input.value.length < parseInt(input.maxLength)) input.value += val;
    });

    const onEnter = {

        Splash: () => {
            $('btnTouchToStart')?.addEventListener('click', () => goToState('PhoneInput'), { once: true });
        },

        PhoneInput: () => {
            $('phoneInput').value = '';
            $('phoneError').textContent = '';
            $('btnSubmitPhone')?.addEventListener('click', async () => {
                const phone = $('phoneInput').value.trim();
                if (!phone.match(/^(0|\+84)\d{8,10}$/)) {
                    $('phoneError').textContent = 'So dien thoai khong hop le.';
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
                        $('phoneError').textContent = data.message || 'Khong the gui OTP.';
                        return;
                    }
                    goToState('OtpInput');
                } catch {
                    $('phoneError').textContent = 'Khong the gui OTP. Thu lai.';
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
                        $('otpError').textContent = data.message || 'Ma OTP sai. Thu lai.';
                    }
                } catch {
                    $('otpError').textContent = 'Loi ket noi.';
                }
            }, { once: true });
        },

        AuthSuccess: ({ userName } = {}) => {
            if ($('authUserName')) $('authUserName').textContent = userName ?? '';
            setTimeout(() => goToState('VehicleSelect'), 2000);
        },

        VehicleSelect: async () => {
            const list = document.querySelector('.vehicle-option-list');
            list.innerHTML = '<p class="section-subtitle">Dang tai...</p>';
            try {
                const res = await fetch('/api/vehicles');
                if (!res.ok) throw new Error(`HTTP ${res.status}`);
                const vehicles = await res.json();

                if (!vehicles.length) {
                    list.innerHTML = '<p class="error-msg">Khong co xe kha dung.</p>';
                    return;
                }

                list.innerHTML = vehicles.map(v => {
                    const depositRate = v.grade === 2 ? 0.20 : v.grade === 1 ? 0.15 : 0.10;
                    const deposit = (v.marketValue * depositRate).toLocaleString('vi-VN');
                    const icon = v.grade === 2 ? '\u{1F6F5}' : v.grade === 1 ? '\u26A1' : '\u{1F6B2}';
                    const gradeName = ['C', 'B', 'A'][v.grade] ?? '?';
                    return `<button class="vehicle-option-btn" data-vehicle-id="${v.id}" data-market-value="${v.marketValue}" data-grade="${v.grade}" data-hourly-rate="${v.hourlyRate}" data-name="${v.name}">
                        <span class="vehicle-icon">${icon}</span>
                        <div class="vehicle-option-info">
                            <span class="vehicle-option-name">${v.name}</span>
                            <span class="vehicle-option-desc">Grade ${gradeName} — Phi coc: ${deposit} VND</span>
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
                list.innerHTML = `<p class="error-msg">Khong the tai danh sach xe: ${err.message}</p>`;
            }
        },// In the deposit state HTML render:
        html += `
  <button class="btn btn-primary" onclick="startVietQR()">🇻🇳 VietQR (Domestic)</button>
  <button class="btn btn-outline" onclick="startStripeCheckout()">💳 Card Payment (International)</button>
`;const urlParams = new URLSearchParams(window.location.search);
        const stripeSession = urlParams.get('stripe_session');
        const rentalId = urlParams.get('rental_id');
        const cancelled = urlParams.get('stripe_cancelled');

        if (stripeSession && rentalId) {
        // Payment submitted — poll status same as VietQR flow
        state.rentalId = rentalId;
        goToState('polling'); // your existing polling state
    } else if (cancelled && rentalId) {
        state.rentalId = rentalId;
        goToState('deposit', { error: 'Payment cancelled. Try again.' });
    } else {
        goToState('splash');
    }

        async function startStripeCheckout() {
            const res = await fetch('/api/payment/stripe/create-checkout', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${state.token}`
                },
                body: JSON.stringify({
                    rentalId: state.rentalId,
                    depositAmountVnd: state.depositAmount,
                    baseUrl: window.location.origin
                })
            });
            const data = await res.json();
            window.location.href = data.url; // redirect to Stripe Hosted Checkout
        }
        

        DepositInfo: ({ vehicleId, marketValue, grade, hourlyRate, name } = {}) => {
            window._selectedVehicleId = vehicleId;
            const depositRate = grade === 2 ? 0.20 : grade === 1 ? 0.15 : 0.10;
            const depositAmt  = marketValue * depositRate;
            const rows = document.querySelectorAll('#paymentState_DepositInfo .deposit-row');
            if (rows[0]) rows[0].querySelector('.deposit-value').textContent = hourlyRate.toLocaleString('vi-VN') + ' VND';
            if (rows[1]) rows[1].querySelector('.deposit-value').textContent = depositAmt.toLocaleString('vi-VN') + ' VND';
            $('btnConfirmDeposit')?.addEventListener('click', () => goToState('Idle'), { once: true });
        },

        Idle: () => {
            $('systemMessage').textContent = '';
            $('btnStartRental').disabled = false;
            $('btnStartRental').textContent = 'TAO MA VIETQR';
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
            setTimeout(() => goToState('Splash'), 30000);
            $('btnDone')?.addEventListener('click', () => goToState('Splash'), { once: true });
        },

        ReturnScan: () => {
            $('bikeIdInput').value = '';
            $('returnError').textContent = '';
            $('btnSubmitReturn')?.addEventListener('click', async () => {
                const bikeCode = $('bikeIdInput').value.trim();
                if (!bikeCode) { $('returnError').textContent = 'Nhap ma xe.'; return; }
                try {
                    const res = await fetch('/api/rentals/return', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${userToken}` },
                        body: JSON.stringify({ bikeCode })
                    });
                    const data = await res.json();
                    if (res.ok) goToState('ReturnProcessing', { rentalId: data.rentalId });
                    else $('returnError').textContent = data.message || 'Khong tim thay xe.';
                } catch {
                    $('returnError').textContent = 'Loi ket noi.';
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
            if ($('errorMessage')) $('errorMessage').textContent = message ?? 'Da xay ra loi.';
            setTimeout(() => goToState('Splash'), 10000);
            $('btnErrorRetry')?.addEventListener('click', () => goToState('Splash'), { once: true });
        }
    };

    async function handleStartRental() {
        $('btnStartRental').disabled = true;
        $('btnStartRental').textContent = 'DANG TAO MA...';
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
                console.log('[START] rentalId:', data.rentalId);
                goToState('Active', { qrUrl: data.qrUrl, rentalId: data.rentalId });
            } else {
                $('systemMessage').textContent = data.message || 'Loi he thong.';
                goToState('Idle');
            }
        } catch {
            goToState('Error', { message: 'Khong the ket noi may chu.' });
        }
    }

    function startPolling(rentalId) {
        console.log('[POLL] starting for rentalId:', rentalId);
        console.log('[POLL] userToken preview:', userToken?.substring(0, 50));
        pollingInterval = setInterval(async () => {
            try {
                const res = await fetch(`/api/rentals/${rentalId}/status`, {
                    headers: { 'Authorization': `Bearer ${userToken}` }
                });
                console.log('[POLL] status HTTP:', res.status, 'rentalId:', rentalId);
                if (!res.ok) return;
                const data = await res.json();
                if (data.status === 'Active') {
                    stopAll();
                    goToState('Success', { vehicleId: data.vehicleCode, dockId: data.dockId });
                } else if (data.status === 'Cancelled') {
                    stopAll();
                    goToState('Error', { message: 'Giao dich da bi huy.' });
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
                goToState('Error', { message: 'Tra xe qua thoi gian. Lien he ky thuat vien.' });
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
                goToState('Error', { message: 'Phien giao dich da het han. Vui long thu lai.' });
            }
        }, 1000);
    }

    function stopAll() {
        clearInterval(timerInterval);
        clearInterval(pollingInterval);
        timerInterval = pollingInterval = null;
    }

    const fmt = n => n != null ? n.toLocaleString('vi-VN') + ' VND' : 'N/A';

    try {
        const res = await fetch('/api/auth/kiosk-token', { method: 'POST' });
        const data = await res.json();
        kioskToken = data.token;
    } catch {
        goToState('Error', { message: 'Loi ket noi he thong. Vui long lien he ky thuat vien.' });
        return;
    }

    goToState('Splash');
});