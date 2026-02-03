using System.Threading.Tasks;

namespace SlskDown
{
    public interface ICountryCacheService
    {
        Task<string> GetCountryAsync(string username);
    }
}

