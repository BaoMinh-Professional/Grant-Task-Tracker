# Front-end patterns

The front end uses React for the interface and RTK Query for server state. Keep
feature endpoints close to their feature; use component state for short-lived form
and interaction state. See [ARCHITECTURE.md](../ARCHITECTURE.md) for the shared
request and error flow, and [UI-STYLE.md](../UI-STYLE.md) for visual rules.

## Add an endpoint

Inject feature endpoints into the shared API rather than creating another API
slice:

```ts
import { baseApi } from '../../api/baseApi'

export const grantsApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    getGrants: build.query<GrantDto[], void>({
      query: () => '/grants',
      providesTags: (grants) => [
        ...(grants?.map(({ id }) => ({ type: 'Grants' as const, id })) ?? []),
        { type: 'Grants', id: 'LIST' },
      ],
    }),
  }),
})
```

Add each tag name to `tagTypes` in `baseApi.ts` before using it. Queries declare
what they provide; successful mutations declare the same tags in
`invalidatesTags` so RTK Query refreshes affected data.

## Idempotent mutations

Create an idempotency key once for each user intent and pass it with the mutation
argument:

```ts
addTask: build.mutation<TaskDto, {
  grantId: string
  task: NewTask
  idempotencyKey: string
}>({
  query: ({ grantId, task, idempotencyKey }) => ({
    url: `/grants/${grantId}/tasks`,
    method: 'POST',
    body: task,
    headers: { 'Idempotency-Key': idempotencyKey },
  }),
})
```

Keep the key while the form values are unchanged so a manual retry reuses it.
Replace it when the officer edits the form or after a successful response. Never
generate a key in `prepareHeaders`: headers are prepared per request, not per user
intent.

## Route errors to the right place

Use `toApiError()` to map RTK Query failures. Field validation belongs beside its
field; refused business actions belong beside the action; errors the officer
cannot fix use `notify.error`; network failures set the connection banner. The
global listener already handles shared cases. Do not add a retry button to a banner
or toast; the officer retries with the original action.

## State and theme

Use RTK Query for data from the API. Add a Redux slice only for client-owned state
that must be shared across components; session and connection state are examples.
Keep isolated form values and temporary display state in the component.

Use the shared classes (`.field`, `.input`, `.btn`, `.alert`, `.tag`, and related
classes) and CSS variables from `frontend/src/styles/theme.css`. Do not add raw
palette values to feature styles.
