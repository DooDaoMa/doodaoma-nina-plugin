using System.Threading.Tasks;

namespace Doodaoma.NINA.Doodaoma.Uploader {
    public interface IFileUploader<in TParams, TOutput>
        where TParams : struct
        where TOutput : class {
        Task<TOutput> Upload(TParams fileParams);
    }
}