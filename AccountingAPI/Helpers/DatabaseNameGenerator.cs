namespace AccountingAPI.Helpers
{
    /// <summary>
    /// تولید نام دیتابیس با فرمت:
    /// DB_{CompanyNameEN}_{FiscalYear}_{Level}
    /// 
    /// مثال: DB_Pars_1404_AccountKol
    /// </summary>
    public static class DatabaseNameGenerator
    {
        private const string DefaultPattern = "DB_{CompanyNameEN}_{FiscalYear}_{Level}";

        // ============================================================
        //  ۴ سطح حساب (نام دقیق: بدون تغییر)
        // ============================================================
        public const string LevelAccountGroup = "AccountGroup";
        public const string LevelAccountKol = "AccountKol";
        public const string LevelAccountMoein = "AccountMoein";
        public const string LevelAccountTafzili = "AccountTafzili";

        public static readonly string[] AllLevels =
        {
            LevelAccountGroup,
            LevelAccountKol,
            LevelAccountMoein,
            LevelAccountTafzili
        };

        public static string Generate(
            string? pattern,
            string? companyNameEN,
            string? fiscalYear,
            string level)
        {
            
            if (string.IsNullOrEmpty(pattern))
                pattern = "DB_{CompanyNameEN}_{FiscalYear}";   // پیش‌فرض

            var result = pattern
                .Replace("{CompanyNameEN}", SanitizeName(companyNameEN))
                .Replace("{FiscalYear}", SanitizeYear(fiscalYear));
                

            // حذف آندرلاین‌های تکراری
            while (result.Contains("__"))
                result = result.Replace("__", "_");

            return result.Trim('_');
        }

        /// <summary>
        /// فقط حروف و اعداد انگلیسی و آندرلاین مجاز است
        /// </summary>
        private static string SanitizeName(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return "Unknown";

            var filtered = new string(
                input.Where(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_').ToArray()
            );

            return string.IsNullOrEmpty(filtered) ? "Unknown" : filtered;
        }

        private static string SanitizeYear(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return "0000";

            var filtered = new string(input.Where(char.IsDigit).ToArray());
            return string.IsNullOrEmpty(filtered) ? "0000" : filtered;
        }

        /// <summary>
        /// آیا این رشته یکی از سطوح معتبر است؟
        /// </summary>
        public static bool IsValidLevel(string? level)
        {
            if (string.IsNullOrEmpty(level)) return false;
            return AllLevels.Contains(level);
        }
    }
}