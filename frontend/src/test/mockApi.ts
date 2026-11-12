import { vi } from 'vitest'

export interface MockCall {
  method: string
  path: string
  headers: Headers
  body: unknown
}

export interface MockResult {
  status?: number
  body?: unknown
}

export interface MockRoute {
  method: string
  path: string
  status?: number
  body?: unknown
  handler?: (call: MockCall) => MockResult | Promise<MockResult>
}

export function mockApi(routes: MockRoute[]) {
  const calls: MockCall[] = []

  const stub = vi.fn(async (input: Request) => {
    const url = new URL(input.url)
    const path = url.pathname + url.search
    const text = input.method === 'GET' ? '' : await input.text()
    const call: MockCall = {
      method: input.method,
      path,
      headers: input.headers,
      body: text ? JSON.parse(text) : undefined,
    }
    calls.push(call)

    const route = routes.find((candidate) => candidate.method === call.method && candidate.path === path)
    if (!route) {
      throw new Error(`No mock route for ${call.method} ${path}`)
    }

    const result = route.handler ? await route.handler(call) : { status: route.status, body: route.body }
    const status = result.status ?? 200
    const isProblem = status >= 400
    return new Response(result.body === undefined ? null : JSON.stringify(result.body), {
      status,
      headers: {
        'Content-Type': isProblem ? 'application/problem+json' : 'application/json',
      },
    })
  })

  vi.stubGlobal('fetch', stub)
  return { calls, stub }
}
