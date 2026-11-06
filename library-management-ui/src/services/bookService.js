import api from './api.js'

export async function getBooks() {
  const response = await api.get('/books')
  return response.data
}

export async function getBookById(id) {
  const response = await api.get(`/books/${id}`)
  return response.data
}

export async function searchBooks(title) {
  const response = await api.get('/books/search', { params: { title } })
  return response.data
}

export async function getBookBorrowHistory(bookId) {
  const response = await api.get(`/books/${bookId}/borrow-history`)
  return response.data
}

export async function createBook(book) {
  const response = await api.post('/books', book)
  return response.data
}

export async function updateBook(id, book) {
  const response = await api.put(`/books/${id}`, book)
  return response.data
}

export async function deleteBook(id) {
  const response = await api.delete(`/books/${id}`)
  return response.data
}
