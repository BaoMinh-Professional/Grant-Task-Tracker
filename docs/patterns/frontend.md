# Front-end patterns

The front end uses React for the interface and RTK Query for server state. Keep
feature endpoints close to their feature; use component state for short-lived form
and interaction state. See [ARCHITECTURE.md](../ARCHITECTURE.md) for the shared
request and error flow, and [UI-STYLE.md](../UI-STYLE.md) for visual rules.

## Add an endpoint

Inject feature endpoints into the shared API rather than creating another API
slice. This excerpt is from [grantsApi.ts](../../frontend/src/features/grants/grantsApi.ts):

```ts
export const grantsApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    getGrants: build.query<GrantListItemDto[], GrantStatusFilter>({
      query: (status) => (status === 'All' ? '/grants' : `/grants?status=${status}`),
      providesTags: (result) => [
        { type: 'Grant', id: 'LIST' },
        ...(result ?? []).map(({ id }) => ({ type: 'Grant' as const, id })),
      ],
    }),
```

Add each tag name to `tagTypes` in `baseApi.ts` before using it (the grant tag is
`'Grant'`). Queries declare what they provide. Mutations declare the tags they
invalidate; a refused business action (`409`) also invalidates them, so the screen
reloads the rule that refused it (see below).

## Idempotent mutations

Create an idempotency key once for each user intent and pass it with the mutation
argument. This excerpt is from [grantsApi.ts](../../frontend/src/features/grants/grantsApi.ts):

```ts
    addTask: build.mutation<TaskDto, { grantId: string; task: NewTask; idempotencyKey: string }>({
      query: ({ grantId, task, idempotencyKey }) => ({
        url: `/grants/${grantId}/tasks`,
        method: 'POST',
        body: task,
        headers: { 'Idempotency-Key': idempotencyKey },
      }),
      invalidatesTags: (_result, error, { grantId }) =>
        error && error.status !== 409 ? [] : [{ type: 'Grant', id: grantId }],
    }),
```

Keep the key while the form values are unchanged so a manual retry reuses it.
Replace it when the officer edits the form or after a successful response. Never
generate a key in `prepareHeaders`: headers are prepared per request, not per user
intent.

## Refused actions refresh the data

A `409` from a business rule means the screen was out of date (for example, a
High task was added by another officer). The mutation therefore invalidates the
same tags on `409` as on success, so the grant detail and list reload and show the
current state. Other failures invalidate nothing.

## Dialogs and keys

[TaskFormDialog.tsx](../../frontend/src/features/grants/TaskFormDialog.tsx) mounts its
form only while the dialog is open, so each opening starts with empty values and no
errors. The same form adds and edits a task. The idempotency key is a ref that changes on every field edit and after a
`422` mismatch:

```tsx
  const keyRef = useRef(crypto.randomUUID())
```

```tsx
  function fieldChanged(field: FieldName) {
    keyRef.current = crypto.randomUUID()
    setFieldErrors((current) => ({ ...current, [field]: undefined }))
  }
```

The server's field messages map to fields by their lower-cased keys (see
`toApiError` in [apiError.ts](../../frontend/src/api/apiError.ts)); the `title`
and `dueDate` messages appear beside the field, and a `409` or `422` appears in an
alert at the top of the dialog. From [TaskFormDialog.tsx](../../frontend/src/features/grants/TaskFormDialog.tsx):

```tsx
      if (error.status === 400) {
        const serverErrors: FieldErrors = {
          title: error.fieldErrors.title?.[0],
          dueDate: error.fieldErrors.duedate?.[0],
          priority: error.fieldErrors.priority?.[0],
        }
```

## Test pattern

[mockApi.ts](../../frontend/src/test/mockApi.ts) replaces `fetch` and records each
request, so tests assert on the path, headers and body that RTK Query sent. Pair it
with [renderWithStore.tsx](../../frontend/src/test/renderWithStore.tsx), which uses
the real store and a signed-in officer. This test is from
[grantsApi.test.tsx](../../frontend/src/features/grants/grantsApi.test.tsx):

```ts
  it('requests open grants with the signed-in officer id', async () => {
    const { calls } = mockApi([{ method: 'GET', path: '/grants?status=Open', body: [grantItem] }])
    const subscription = store.dispatch(grantsApi.endpoints.getGrants.initiate('Open'))
    await subscription
    subscription.unsubscribe()

    expect(calls[0].path).toBe('/grants?status=Open')
    expect(calls[0].headers.get('X-User-Id')).toBe(testUser.id)
  })
```

## Staleness

[baseApi.ts](../../frontend/src/api/baseApi.ts) refetches on focus and reconnect.
The comment above the options records the limit: data can be stale between
refetches, and the server still enforces the rules.

## Edit with a version

Edit sends the version the form was opened with, so the server can refuse a stale
save with `412`. The version is captured when the form mounts, not refreshed on
conflict: the officer closes and reopens the dialog to see the latest task. This
excerpt is from [grantsApi.ts](../../frontend/src/features/grants/grantsApi.ts):

```ts
    editTask: build.mutation<
      TaskDto,
      { taskId: string; grantId: string; version: number; task: NewTask; idempotencyKey: string }
    >({
      query: ({ taskId, version, task, idempotencyKey }) => ({
        url: `/tasks/${taskId}`,
        method: 'PUT',
        body: task,
        headers: { 'If-Match': `"${version}"`, 'Idempotency-Key': idempotencyKey },
      }),
      invalidatesTags: (_result, error, { grantId }) =>
        error && error.status !== 409 && error.status !== 412 ? [] : [{ type: 'Grant', id: grantId }],
    }),
```

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
