namespace KnowledgeSearch.Core.Abstractions.Jobs;

// Unidad de trabajo asincrona. Las implementaciones describen QUE hacer; la
// IJobQueue decide CUANDO y COMO ejecutarlas (orden, paralelismo, persistencia).
public interface IJob
{
    string Kind { get; }
    Task ExecuteAsync(IJobContext context, CancellationToken cancellationToken);
}

// Contexto inyectado al ejecutar un job. Permite emitir progreso y consultar
// el propio id sin acoplarse a un transporte concreto (SSE, log, etc.).
public interface IJobContext
{
    Guid JobId { get; }
    Task ReportProgressAsync(string message, CancellationToken cancellationToken = default);
}
