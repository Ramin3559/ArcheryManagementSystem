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

    function roundMoney(value) {
        return Math.round((Number(value) || 0) * 100) / 100;
    }

    function computePayable(listPrice, discountAmount) {
        const list = Math.max(0, Number(listPrice) || 0);
        const discount = Math.min(Math.max(0, Number(discountAmount) || 0), list);
        return roundMoney(list - discount);
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

    function bindPair(cashInput, cardInput, getPayable, onChange) {
        let lastEdited = null;

        function notify() {
            if (typeof onChange === "function") onChange();
        }

        function balance(from) {
            const payable = computePayable(getPayable().listPrice, getPayable().discount);
            if (payable <= Tolerance) {
                if (cashInput instanceof HTMLInputElement) cashInput.value = "";
                if (cardInput instanceof HTMLInputElement) cardInput.value = "";
                notify();
                return;
            }

            if (from === "cash" && cashInput instanceof HTMLInputElement && cardInput instanceof HTMLInputElement) {
                const cash = parseAmount(cashInput);
                const card = roundMoney(Math.max(0, payable - cash));
                cardInput.value = card > 0 ? card.toFixed(2) : "";
            } else if (from === "card" && cashInput instanceof HTMLInputElement && cardInput instanceof HTMLInputElement) {
                const card = parseAmount(cardInput);
                const cash = roundMoney(Math.max(0, payable - card));
                cashInput.value = cash > 0 ? cash.toFixed(2) : "";
            }
            notify();
        }

        cashInput?.addEventListener("input", () => {
            lastEdited = "cash";
            balance("cash");
        });
        cardInput?.addEventListener("input", () => {
            lastEdited = "card";
            balance("card");
        });

        return {
            rebalance() {
                balance(lastEdited || "cash");
            }
        };
    }

    global.PaymentSplit = {
        Tolerance,
        parseAmount,
        roundMoney,
        computePayable,
        splitCombinedCheckout,
        bindPair
    };
})(window);
