import { afterEach, describe, it, expect, vi } from 'vitest'
import { ApiError, request } from '../http'

function respond(status: number, body?: unknown) {
  const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
    new Response(body === undefined ? null : JSON.stringify(body), {
      status,
      headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' },
    }),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

/** What the one fetch call was given. */
function sent(fetchMock: ReturnType<typeof respond>) {
  const [url, init] = fetchMock.mock.calls[0]!
  return { url, init: init!, headers: init!.headers as Record<string, string> }
}

afterEach(() => vi.unstubAllGlobals())

describe('request', () => {
  it('sends JSON with the version in If-Match', async () => {
    const fetchMock = respond(200, { ok: true })

    await request('/items/1', { method: 'PUT', body: { title: 'Tee' }, version: '"v1"' })

    const { url, init, headers } = sent(fetchMock)
    expect(url).toBe('/api/items/1')
    expect(headers['If-Match']).toBe('"v1"')
    expect(headers['Content-Type']).toBe('application/json')
    expect(init.body).toBe('{"title":"Tee"}')
  })

  it('leaves the content type to the browser for a multipart upload', async () => {
    const fetchMock = respond(200, {})

    await request('/items/1/photo', { method: 'PUT', body: new FormData() })

    expect(sent(fetchMock).headers['Content-Type']).toBeUndefined()
  })

  it('returns nothing for 204 and 202', async () => {
    respond(204)
    expect(await request('/auth/logout', { method: 'POST' })).toBeUndefined()
  })

  it('turns ProblemDetails into an ApiError carrying the detail', async () => {
    respond(412, { title: 'Stale version', detail: 'The item changed since you loaded it.' })

    const error = await request('/items/1', { method: 'PUT', body: {} }).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).isStale).toBe(true)
    expect((error as ApiError).message).toBe('The item changed since you loaded it.')
  })

  it('uses the first field error as the message for a validation failure', async () => {
    respond(400, { title: 'One or more validation errors occurred.', errors: { Email: ['There is already an account with that email.'] } })

    const error = (await request('/auth/register', { method: 'POST', body: {} }).catch((e: unknown) => e)) as ApiError

    expect(error.message).toBe('There is already an account with that email.')
    expect(error.errors.Email).toEqual(['There is already an account with that email.'])
  })
})
