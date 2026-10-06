using System.Globalization;

namespace AccountingAPI.Helpers
{
    public static class DateHelper
    {
        private static readonly PersianCalendar pc = new PersianCalendar();

        /// <summary>
        /// تبدیل تاریخ شمسی به میلادی
        /// فرمت ورودی: yyyy/MM/dd
        /// </summary>
        public static DateTime PersianToGregorian(string persianDate)
        {
            var parts = persianDate.Split('/');
            if (parts.Length != 3)
                throw new FormatException("فرمت تاریخ باید yyyy/MM/dd باشد");

            int year = int.Parse(parts[0]);
            int month = int.Parse(parts[1]);
            int day = int.Parse(parts[2]);

            return pc.ToDateTime(year, month, day, 0, 0, 0, 0);
        }

        /// <summary>
        /// تبدیل تاریخ میلادی به شمسی (فقط تاریخ)
        /// خروجی: yyyy/MM/dd
        /// </summary>
        public static string GregorianToPersian(DateTime gregorianDate)
        {
            return $"{pc.GetYear(gregorianDate)}/{pc.GetMonth(gregorianDate):00}/{pc.GetDayOfMonth(gregorianDate):00}";
        }

        /// <summary>
        /// تبدیل تاریخ میلادی به شمسی + ساعت
        /// خروجی: yyyy/MM/dd - HH:mm
        /// مثال: 1405/06/30 - 10:30
        /// </summary>
        public static string GregorianToPersianWithTime(DateTime gregorianDate)
        {
            var persianDate = $"{pc.GetYear(gregorianDate)}/{pc.GetMonth(gregorianDate):00}/{pc.GetDayOfMonth(gregorianDate):00}";
            var time = gregorianDate.ToString("HH:mm");
            return $"{persianDate} - {time}";
        }
    }
}