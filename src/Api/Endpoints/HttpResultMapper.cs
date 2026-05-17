using KnowledgeSearch.Core.Common;

namespace KnowledgeSearch;

internal static class HttpResultMapper
{
    extension<T>(Result<T> result)
    {
        public IResult ToHttpResult(Func<T, IResult> onSuccess)
        {
            return result.Resolve(
                onSuccess,
                error => error.Kind switch
                {
                    ErrorKind.Validation => Results.BadRequest(new ErrorResult(error.Message)),
                    ErrorKind.NotFound => Results.NotFound(new ErrorResult(error.Message)),
                    ErrorKind.Conflict => Results.Conflict(new ErrorResult(error.Message)),
                    ErrorKind.Unauthorized => Results.Unauthorized(),
                    _ => Results.BadRequest(new ErrorResult(error.Message)),
                });
        }
    }
}
