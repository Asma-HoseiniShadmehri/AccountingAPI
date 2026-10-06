using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AccountingAPI.Filters
{
    /// <summary>
    /// فیلتر مرتب‌سازی کنترلرها در Swagger UI
    /// به‌گونه‌ای که Accounting قبل از AccountGroup نمایش داده شود.
    /// </summary>
    public class SwaggerDocumentFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            // ============================================================
            //  ترتیب دلخواه کنترلرها
            // ============================================================
            var order = new List<string>
            {
                "Accounting",       // اول
                "PasswordReset",    // ✅ جدید — دوم
                "AccountGroup",     // سوم
                "AccountKol",       // چهارم
                "AccountMoein",     // پنجم
                "AccountTafzili",   // ششم
                "Document"          // هفتم
            };
            // ============================================================
            //  مرتب‌سازی Tags (گروه‌های Swagger)
            // ============================================================
            if (swaggerDoc.Tags != null)
            {
                swaggerDoc.Tags = swaggerDoc.Tags
                    .OrderBy(t =>
                    {
                        var index = order.IndexOf(t.Name);
                        return index == -1 ? int.MaxValue : index;
                    })
                    .ToList();
            }

            // ============================================================
            //  مرتب‌سازی مسیرها (Paths) بر اساس Tag
            // ============================================================
            var sortedPaths = swaggerDoc.Paths
                .OrderBy(p =>
                {
                    var tag = p.Value.Operations.Values
                        .SelectMany(o => o.Tags)
                        .FirstOrDefault()?.Name ?? "";
                    var index = order.IndexOf(tag);
                    return index == -1 ? int.MaxValue : index;
                })
                .ToDictionary(k => k.Key, v => v.Value);

            swaggerDoc.Paths = new OpenApiPaths();
            foreach (var path in sortedPaths)
            {
                swaggerDoc.Paths.Add(path.Key, path.Value);
            }
        }
    }
}