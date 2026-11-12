import { createElement, type ReactNode } from 'react'
import { toast } from 'sonner'
import { Reference } from './Reference'

interface ErrorNotification {
  id: string
  title: string
  traceId?: string
}

export const notify = {
  error({ id, title, traceId }: ErrorNotification) {
    toast.error(title, {
      id,
      duration: Infinity,
      ...(traceId
        ? { description: createElement(Reference, { traceId }) }
        : {}),
    })
  },

  info(title: string, description?: ReactNode) {
    toast.info(title, { description, duration: 5_000 })
  },

  success(title: string) {
    toast.success(title, { duration: 5_000 })
  },
}
