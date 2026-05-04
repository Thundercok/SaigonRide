// api-client.js — all network calls. No state. No DOM. Pure async functions.

const ApiClient = {

    async kioskToken() {
        const res = await fetch('/api/auth/kiosk-token', { method: 'POST' });
        if (!res.ok) throw new Error('Kiosk token failed');
        return res.json();
    },

    async sendOtp(phone) {
        const res = await fetch('/api/auth/send-otp', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ phone })
        });
        return { ok: res.ok, data: await res.json() };
    },

    async verifyOtp(phone, otp) {
        const res = await fetch('/api/auth/verify-otp', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ phone, otp })
        });
        return { ok: res.ok, data: await res.json() };
    },

    async rfidLogin(rfidId) {
        const res = await fetch('/api/auth/rfid', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ rfidId })
        });
        return { ok: res.ok, data: await res.json() };
    },

    async getVehicles() {
        const res = await fetch('/api/vehicles');
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        return res.json();
    },

    async startRental(vehicleId, token) {
        const res = await fetch('/api/rentals/start', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
            body: JSON.stringify({ vehicleId, mode: 0 })
        });
        return { ok: res.ok, data: await res.json() };
    },

    async getRentalStatus(rentalId, token) {
        const res = await fetch(`/api/rentals/${rentalId}/status`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (!res.ok) return null;
        return res.json();
    },

    async cancelRental(rentalId, token) {
        const res = await fetch(`/api/rentals/${rentalId}/cancel`, {
            method: 'DELETE',
            headers: { 'Authorization': `Bearer ${token}` }
        });
        return { ok: res.ok, data: await res.json() };
    },

    async createStripeCheckout(rentalId, depositAmountVnd, baseUrl, token) {
        const res = await fetch('/api/payment/stripe/create-checkout', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
            body: JSON.stringify({ rentalId, depositAmountVnd, baseUrl })
        });
        return { ok: res.ok, data: await res.json() };
    },

    async returnRental(bikeCode, token) {
        const res = await fetch('/api/rentals/return', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
            body: JSON.stringify({ bikeCode })
        });
        return { ok: res.ok, data: await res.json() };
    },

    async getReturnStatus(rentalId, token) {
        const res = await fetch(`/api/rentals/${rentalId}/return-status`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (!res.ok) return null;
        return res.json();
    }
};
