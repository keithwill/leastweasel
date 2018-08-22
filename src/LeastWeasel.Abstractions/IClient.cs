using System.Threading.Tasks;

namespace LeastWeasel.Abstractions
{
    public interface IClient
    {
        void Send<TRequest>(string method, TRequest message);
        Task<TResult> Request<TRequest, TResult>(string method, TRequest request);
        Task SendFile(string filePath, string remoteFilePath);

        Task SendDirectory(string directoryPath, string remoteDirectoryPath);

    }
}