namespace AccountingAPI.Models.Enums
{
    public enum NatureType
    {
        Debit = 1,           // بدهکار
        Credit = 2,          // بستانکار
        StrictlyDebit = 3,   // اکیدا بدهکار
        StrictlyCredit = 4,  // اکیدا بستانکار
        Both = 5             // هم بستانکار-بدهکار
    }

    public enum GroupType
    {
        Permanent = 1,       // دایم
        Temporary = 2        // موقت
    }
}