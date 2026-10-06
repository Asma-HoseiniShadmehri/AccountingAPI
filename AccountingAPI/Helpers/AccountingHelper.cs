using AccountingAPI.Models;

namespace AccountingAPI.Helpers
{
    public static class AccountingHelper
    {
        // ============================================================
        //  متدهای نمایش کدها به صورت S, K, M, T
        // ============================================================

        public static string GetFullCodeDisplay(int groupCode)
        {
            return $"S{groupCode}";
        }

        public static string GetFullCodeDisplay(int groupCode, int kolCode)
        {
            return $"S{groupCode}K{kolCode:D2}";
        }

        public static string GetFullCodeDisplay(int groupCode, int kolCode, int moeinCode)
        {
            return $"S{groupCode}K{kolCode:D2}M{moeinCode:D3}";
        }

        public static string GetFullCodeDisplay(int groupCode, int kolCode, int moeinCode, int tafziliCode)
        {
            return $"S{groupCode}K{kolCode:D2}M{moeinCode:D3}T{tafziliCode:D3}";
        }

        // ============================================================
        //  متدهای Overload برای دریافت اشیاء مدل (با پشتیبانی از null)
        // ============================================================

        /// <summary>
        /// دریافت کد کامل از شیء AccountTafzili (پشتیبانی از null)
        /// </summary>
        public static string GetFullCodeDisplay(AccountTafzili? tafzili)
        {
            if (tafzili == null) return string.Empty;

            int groupCode = tafzili.Moein?.Kol?.Group?.Code_Group ?? 0;
            int kolCode = (tafzili.Moein?.Kol?.Code_kol ?? 0) % 100;
            int moeinCode = (tafzili.Moein?.Code_moein ?? 0) % 1000;
            int tafziliCode = tafzili.Code_tafzili % 1000;
            return GetFullCodeDisplay(groupCode, kolCode, moeinCode, tafziliCode);
        }

        /// <summary>
        /// دریافت کد کامل از شیء AccountMoein (پشتیبانی از null)
        /// </summary>
        public static string GetFullCodeDisplay(AccountMoein? moein)
        {
            if (moein == null) return string.Empty;

            int groupCode = moein.Kol?.Group?.Code_Group ?? 0;
            int kolCode = (moein.Kol?.Code_kol ?? 0) % 100;   // ← KK
            int moeinCode = moein.Code_moein % 1000;            // ← MMM
            return GetFullCodeDisplay(groupCode, kolCode, moeinCode);
        }

        /// <summary>
        /// دریافت کد کامل از شیء AccountKol (پشتیبانی از null)
        /// </summary>
        public static string GetFullCodeDisplay(AccountKol? kol)
        {
            if (kol == null) return string.Empty;

            int groupCode = kol.Group?.Code_Group ?? 0;
            int kolCode = kol.Code_kol % 100;   // ← اصلاح: KK را جدا کن
            return GetFullCodeDisplay(groupCode, kolCode);
        }
        // ============================================================
        //  متدهای ساخت کدهای ترکیبی
        // ============================================================

        public static int BuildKolCode(int groupCode, int kolCode)
        {
            return int.Parse($"{groupCode}{kolCode:D2}");
        }

        public static int BuildMoeinCode(int groupCode, int kolCode, int moeinCode)
        {
            return int.Parse($"{groupCode}{kolCode:D2}{moeinCode:D3}");
        }

        public static int BuildTafziliCode(int groupCode, int kolCode, int moeinCode, int tafziliCode)
        {
            return int.Parse($"{groupCode}{kolCode:D2}{moeinCode:D3}{tafziliCode:D3}");
        }

        // ============================================================
        //  متدهای استخراج اجزای کدها
        // ============================================================

        public static (int GroupCode, int KolCode, int MoeinCode) ParseMoeinCode(int moeinCode)
        {
            string codeStr = moeinCode.ToString().PadLeft(6, '0');
            int groupCode = int.Parse(codeStr.Substring(0, 1));
            int kolCode = int.Parse(codeStr.Substring(1, 2));
            int moein = int.Parse(codeStr.Substring(3, 3));
            return (groupCode, kolCode, moein);
        }

        public static (int GroupCode, int KolCode, int MoeinCode, int TafziliCode) ParseTafziliCode(int tafziliCode)
        {
            string codeStr = tafziliCode.ToString().PadLeft(9, '0');
            int groupCode = int.Parse(codeStr.Substring(0, 1));
            int kolCode = int.Parse(codeStr.Substring(1, 2));
            int moeinCode = int.Parse(codeStr.Substring(3, 3));
            int tafzili = int.Parse(codeStr.Substring(6, 3));
            return (groupCode, kolCode, moeinCode, tafzili);
        }

        // ============================================================
        //  متدهای اعتبارسنجی
        // ============================================================

        public static bool IsValidGroupCode(int code) => code >= 0 && code <= 9;
        public static bool IsValidKolCode(int code) => code >= 0 && code <= 999;
        public static bool IsValidMoeinCode(int code) => code >= 0 && code <= 999999;
        public static bool IsValidTafziliCode(int code) => code >= 0 && code <= 999999999;

        // ============================================================
        //  متد قدیمی (برای سازگاری با کدهای قبلی)
        // ============================================================

        [System.Obsolete("Use GetFullCodeDisplay(AccountTafzili?) instead.")]
        public static string GetFullCode(AccountTafzili? tafzili)
        {
            return GetFullCodeDisplay(tafzili);
        }
    }
    
}