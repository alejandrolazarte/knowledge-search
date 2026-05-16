using KnowledgeSearch.Core.Common;

namespace KnowledgeSearch;

internal static class HttpResultMapper
{
    public static IResult ToHttpResult<T>(
        this Result<T> result,
        Func<T, IResult> onSuccess)
    {
        if (result.IsSuccess)
        {
            return onSuccess(result.Value!);
        }

        var error = result.Error!;
        return error.Kind switch
        {
            ErrorKind.Validation => Results.BadRequest(new ErrorResult(error.Message)),
            ErrorKind.NotFound => Results.NotFound(new ErrorResult(error.Message)),
            ErrorKind.Conflict => Results.Conflict(new ErrorResult(error.Message)),
            ErrorKind.Unauthorized => Results.Unauthorized(),
            _ => Results.BadRequest(new ErrorResult(error.Message)),
        };
    }
}
