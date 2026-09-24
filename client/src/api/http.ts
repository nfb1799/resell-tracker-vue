// The one place the client talks HTTP. Same-origin, so the session cookie rides
// along on its own; errors arrive as ProblemDetails and become ApiError.

export class ApiError extends Error {
  readonly status: number
  readonly title: string
  /** Field errors from a 400, keyed by field name. */
  readonly errors: Record<string, string[]>

  constructor(status: number, title: string, detail: string | undefined, errors: Record<string, string[]> = {}) {
    super(detail || Object.values(errors).flat()[0] || title || `Request failed (${status})`)
    this.status = status
    this.title = title
    this.errors = errors
  }

  /** The item changed somewhere else since it was loaded (If-Match was stale). */
  get isStale(): boolean {
    return this.status === 412
  }
}

interface ProblemBody {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  /** Sent as JSON, or as-is for FormData. */
  body?: unknown
  /** The item version for If-Match. */
  version?: string
  signal?: AbortSignal
}

export async function request<T>(path: string, { method = 'GET', body, version, signal }: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  let payload: BodyInit | undefined
  if (body instanceof FormData) {
    payload = body
  } else if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
    payload = JSON.stringify(body)
  }
  if (version) headers['If-Match'] = version

  const response = await fetch(`/api${path}`, { method, headers, body: payload, signal, credentials: 'same-origin' })

  if (!response.ok) {
    let problem: ProblemBody = {}
    try {
      problem = (await response.json()) as ProblemBody
    } catch {
      // Not every failure has a body (a proxy error, say).
    }
    throw new ApiError(response.status, problem.title ?? response.statusText, problem.detail, problem.errors)
  }

  if (response.status === 204 || response.status === 202) return undefined as T
  return (await response.json()) as T
}
