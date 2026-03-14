using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public interface IAPIClient
{
    Task<T> GetAsync<T>(string endpoint);
    Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data);
    Task PostAsync<TRequest>(string endpoint, TRequest data);
    Task<TResponse> PutAsync<TRequest, TResponse>(string endpoint, TRequest data);
    Task PutAsync<TRequest>(string endpoint, TRequest data);
    Task DeleteAsync(string endpoint);
    void SetBearerToken(string token);
    void AddHeader(string name, string value);
}
