// api-client.js
const ApiClient = {
    // Simulate sending an OTP
    sendOtp: async (phoneNumber) => {
        console.log(`API: Sending OTP to ${phoneNumber}...`);
        return new Promise((resolve) => {
            setTimeout(() => {
                resolve({ success: true, message: "OTP Sent" });
            }, 800); // 800ms fake network delay
        });
    },

    // Simulate verifying the OTP
    verifyOtp: async (otpCode) => {
        console.log(`API: Verifying OTP ${otpCode}...`);
        return new Promise((resolve, reject) => {
            setTimeout(() => {
                if (otpCode === "123456") { // Hardcoded success code for testing
                    resolve({ success: true, userName: "Huỳnh Nhật Huy" });
                } else {
                    resolve({ success: false, message: "Mã OTP không hợp lệ." });
                }
            }, 1000);
        });
    },

    // Simulate generating a VietQR code string
    generateVietQr: async (vehicleId) => {
        console.log(`API: Generating QR for vehicle type ${vehicleId}...`);
        return new Promise((resolve) => {
            setTimeout(() => {
                resolve({
                    success: true,
                    // Using a placeholder image for the QR code
                    qrUrl: "https://api.qrserver.com/v1/create-qr-code/?size=250x250&data=ThanhToanSaigonRide"
                });
            }, 1200);
        });
    },

    // Simulate polling the bank to see if they transferred the money
    checkPaymentStatus: async () => {
        return new Promise((resolve) => {
            setTimeout(() => {
                resolve({ success: true, assignedBike: "BK-089", dockId: "Dock 04" });
            }, 3000); // Pretend it takes 3 seconds for the user to scan and pay
        });
    }
};