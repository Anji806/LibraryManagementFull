import api from './api.js'

export async function borrowBook(bookId, memberId) {
  const response = await api.post('/borrowtransactions/borrow', null, {
    params: { bookId, memberId },
  })
  return response.data
}

export async function returnBook(transactionId) {
  const response = await api.put(`/borrowtransactions/return/${transactionId}`)
  return response.data
}
