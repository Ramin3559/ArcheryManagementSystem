(function (global) {
    "use strict";

    const Tolerance = 0.01;

    function parseAmount(input) {
        if (!(input instanceof HTMLInputElement)) return 0;
        const raw = input.value.trim();
        if (!raw) return 0;
        const n = Number(raw);
        return Number.isFinite(n) && n >= 0 ? n : 0;
    }

    function isEmpty(input) {
        return !(input instanceof HTMLInputElement) || input.value.trim() === "";
    }

    function isBlankOrZero(input) {
        if (!(input instanceof HTMLInputElement)) return true;
        const raw = input.value.trim();
        if (!raw) return true;
        const n = Number(raw);
        return Number.isFinite(n) && Math.abs(n) <= Tolerance;
    }

    function roundMoney(value) {
        return Math.round((Number(value) || 0) * 100) / 100;
    }

    function computePayable(listPrice, discountAmount) {
        const list = Math.max(0, Number(listPrice) || 0);
        const discount = Math.min(Math.max(0, Number(discountAmount) || 0), list);
        return roundMoney(list - discount);
    }

    function resolvePayable(getPayable) {
        const v = typeof getPayable === "function" ? getPayable() : getPayable;
        if (typeof v === "number") {
            return roundMoney(Math.max(0, v));
        }
        if (v && typeof v === "object") {
            return computePayable(v.listPrice, v.discount);
        }
        return 0;
    }

    function setAmount(input, value) {
        if (!(input instanceof HTMLInputElement) || input.disabled) return;
        input.value = roundMoney(Math.max(0, value)).toFixed(2);
    }

    function splitCombinedCheckout(packageListPrice, equipmentListPrice, totalDiscount, totalCash, totalCard) {
        packageListPrice = Math.max(0, Number(packageListPrice) || 0);
        equipmentListPrice = Math.max(0, Number(equipmentListPrice) || 0);
        const totalList = packageListPrice + equipmentListPrice;
        if (totalList <= Tolerance) {
            return {
                package: { listPrice: 0, discount: 0, cash: 0, card: 0 },
                equipment: { listPrice: 0, discount: 0, cash: 0, card: 0 }
            };
        }

        const discount = Math.min(Math.max(0, Number(totalDiscount) || 0), totalList);
        const packageDiscount = roundMoney(discount * packageListPrice / totalList);
        const equipmentDiscount = roundMoney(discount - packageDiscount);
        const packagePayable = packageListPrice - packageDiscount;
        const equipmentPayable = equipmentListPrice - equipmentDiscount;
        const cash = Math.max(0, Number(totalCash) || 0);
        const card = Math.max(0, Number(totalCard) || 0);
        const packageCash = Math.min(cash, packagePayable);
        const packageCard = Math.min(card, Math.max(0, packagePayable - packageCash));
        const cashAfterPackage = cash - packageCash;
        const cardAfterPackage = card - packageCard;
        const equipmentCash = Math.min(cashAfterPackage, equipmentPayable);
        const equipmentCard = Math.min(cardAfterPackage, Math.max(0, equipmentPayable - equipmentCash));

        return {
            package: {
                listPrice: packageListPrice,
                discount: packageDiscount,
                cash: packageCash,
                card: packageCard
            },
            equipment: {
                listPrice: equipmentListPrice,
                discount: equipmentDiscount,
                cash: equipmentCash,
                card: equipmentCard
            }
        };
    }

    /**
     * Nağd yazılanda kart = ödəniləcək − nağd;
     * kart yazılanda nağd = ödəniləcək − kart;
     * endirim/qiymət dəyişəndə rebalance() eyni qayda ilə yeniləyir.
     */
    function bindPair(cashInput, cardInput, getPayable, onChange) {
        let lastEdited = null; // "cash" | "card" | null
        let syncing = false;

        function notify() {
            if (typeof onChange === "function") onChange();
        }

        function fillRemainderFromCash() {
            const payable = resolvePayable(getPayable);
            // Skip only when both blank/zero AND nothing to allocate.
            if (payable <= Tolerance && isBlankOrZero(cashInput) && isBlankOrZero(cardInput)) {
                return;
            }
            const cash = parseAmount(cashInput);
            // Always rewrite the other side, even if it already shows 0.00.
            setAmount(cardInput, Math.max(0, payable - cash));
        }

        function fillRemainderFromCard() {
            const payable = resolvePayable(getPayable);
            if (payable <= Tolerance && isBlankOrZero(cashInput) && isBlankOrZero(cardInput)) {
                return;
            }
            const card = parseAmount(cardInput);
            setAmount(cashInput, Math.max(0, payable - card));
        }

        function rebalance() {
            if (syncing) return;
            syncing = true;
            try {
                const payable = resolvePayable(getPayable);
                if (payable <= Tolerance) {
                    if (cashInput instanceof HTMLInputElement && !cashInput.disabled) {
                        cashInput.value = "0.00";
                    }
                    if (cardInput instanceof HTMLInputElement && !cardInput.disabled) {
                        cardInput.value = "0.00";
                    }
                    notify();
                    return;
                }

                if (isBlankOrZero(cashInput) && isBlankOrZero(cardInput)) {
                    if (cashInput instanceof HTMLInputElement && !cashInput.disabled && cashInput.value.trim() !== "") cashInput.value = "";
                    if (cardInput instanceof HTMLInputElement && !cardInput.disabled && cardInput.value.trim() !== "") cardInput.value = "";
                    notify();
                    return;
                }

                if (lastEdited === "card") {
                    fillRemainderFromCard();
                } else if (lastEdited === "cash") {
                    fillRemainderFromCash();
                } else if (!isBlankOrZero(cashInput)) {
                    fillRemainderFromCash();
                } else if (!isBlankOrZero(cardInput)) {
                    fillRemainderFromCard();
                } else {
                    notify();
                    return;
                }
                notify();
            } finally {
                syncing = false;
            }
        }

        function onCashEdit() {
            if (syncing) return;
            lastEdited = "cash";
            syncing = true;
            try {
                fillRemainderFromCash();
                notify();
            } finally {
                syncing = false;
            }
        }

        function onCardEdit() {
            if (syncing) return;
            lastEdited = "card";
            syncing = true;
            try {
                fillRemainderFromCard();
                notify();
            } finally {
                syncing = false;
            }
        }

        cashInput?.addEventListener("input", onCashEdit);
        cashInput?.addEventListener("change", onCashEdit);
        cardInput?.addEventListener("input", onCardEdit);
        cardInput?.addEventListener("change", onCardEdit);

        return {
            rebalance,
            fillRemainderFromCash,
            fillRemainderFromCard
        };
    }

    global.PaymentSplit = {
        Tolerance,
        parseAmount,
        isBlankOrZero,
        roundMoney,
        computePayable,
        splitCombinedCheckout,
        bindPair
    };
})(window);
