using System.Threading.Tasks;
using BlazorGrid.Abstractions.Models;

namespace BlazorGrid.Abstractions.Interfaces
{
    public interface IGridProvider
    {
        Task<DataPageResult<T>> GetAsync<T>(string requestUrl);
        string GetRequestUrl(string BaseUrl, string RowId);
        string GetRequestUrl(string BaseUrl, int Offset, int Length, string OrderBy, bool OrderByDescending, string SearchQuery);
    }
}