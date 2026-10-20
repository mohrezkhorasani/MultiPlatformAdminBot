using System.IO;
using System.Threading.Tasks;

namespace Application.Interfaces.IInfrastructure
{
    public interface ISourceMediaDownloader
    {
        Task<string> DownloadFileAsync(string fileId, string sourcePlatform, System.IProgress<double> progress = null);
    }
}
