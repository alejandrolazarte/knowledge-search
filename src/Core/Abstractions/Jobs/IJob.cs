namespace KnowledgeSearch.Core.Abstractions.Jobs;

/// <summary>
/// Unidad de trabajo asincrona. Las implementaciones describen <em>que</em> hacer;
/// la <see cref="IJobQueue"/> decide <em>cuando</em> y <em>como</em> ejecutarlas.
/// </summary>
public interface IJob
{
    string Kind { get; }
    Task ExecuteAsync(IJobContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Contexto inyectado al job en tiempo de ejecucion. Permite reportar progreso
/// sin acoplarse a un transporte concreto (SSE, log, etc.).
/// </summary>
public interface IJobContext
{
    Guid JobId { get; }
    Task ReportProgressAsync(string message, CancellationToken cancellationToken = default);
}
