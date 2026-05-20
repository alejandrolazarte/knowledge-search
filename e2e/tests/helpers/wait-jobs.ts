import type { APIRequestContext } from '@playwright/test'

type JobStatus = {
  jobId: string
  kind: string
  state: 'Queued' | 'Running' | 'Completed' | 'Failed'
  error: string | null
}

const POLL_INTERVAL_MS = 200
const DEFAULT_TIMEOUT_MS = 60_000

async function fetchJobStatus(request: APIRequestContext, jobId: string): Promise<JobStatus | null> {
  const response = await request.get(`/jobs/${jobId}`)
  if (!response.ok()) {
    return null
  }
  return (await response.json()) as JobStatus
}

export async function waitForJob(
  request: APIRequestContext,
  jobId: string,
  timeoutMs: number = DEFAULT_TIMEOUT_MS,
): Promise<JobStatus> {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    const status = await fetchJobStatus(request, jobId)
    if (status && (status.state === 'Completed' || status.state === 'Failed')) {
      if (status.state === 'Failed') {
        throw new Error(`Job ${jobId} (${status.kind}) failed: ${status.error ?? 'unknown'}`)
      }
      return status
    }
    await new Promise(resolve => setTimeout(resolve, POLL_INTERVAL_MS))
  }
  throw new Error(`Job ${jobId} did not complete within ${timeoutMs}ms`)
}

export async function waitForJobs(
  request: APIRequestContext,
  jobIds: ReadonlyArray<string>,
  timeoutMs: number = DEFAULT_TIMEOUT_MS,
): Promise<void> {
  await Promise.all(jobIds.map(id => waitForJob(request, id, timeoutMs)))
}
