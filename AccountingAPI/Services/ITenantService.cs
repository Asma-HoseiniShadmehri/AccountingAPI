using System.Threading.Tasks;

namespace AccountingAPI.Services
{
    public interface ITenantService
    {
        Task<string> GetCompanyConnectionString(int companyId);
        Task<string> GetConnectionString(int companyId, string level);
        Task<bool> CreateCompanyDatabase(int companyId, string serverName, string databaseName, string level);
        Task<bool> UpdateCompanyConnectionString(int companyId, string connectionString);
    }
}