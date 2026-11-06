import api from './api.js'

export async function getMemberBorrowedBooks(memberId) {
  const response = await api.get(`/members/${memberId}/borrowed-books`)
  return response.data
}

export async function getMemberBorrowHistory(memberId) {
  const response = await api.get(`/members/${memberId}/borrow-history`)
  return response.data
}
