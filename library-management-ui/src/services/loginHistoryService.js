import api from './api.js'

const pendingHistoryRequests = new Map()

export async function getLoginHistory(filters) {
  const key = JSON.stringify(filters)
  if (pendingHistoryRequests.has(key)) return pendingHistoryRequests.get(key)
  const request = api.get('/login-history', { params: filters })
    .then((response) => response.data)
    .finally(() => pendingHistoryRequests.delete(key))
  pendingHistoryRequests.set(key, request)
  return request
}

export async function deleteLoginHistory(loginLogIds) {
  const response = await api.delete('/login-history', { data: { loginLogIds } })
  return response.data
}

export async function getAllLoginHistory(filters) {
  const pageSize = 100
  const firstPage = await getLoginHistory({ ...filters, page: 1, pageSize })
  const pages = Math.ceil(firstPage.totalCount / pageSize)
  const items = [...firstPage.items]
  for (let page = 2; page <= pages; page += 1) {
    const result = await getLoginHistory({ ...filters, page, pageSize })
    items.push(...result.items)
  }
  return items
}
