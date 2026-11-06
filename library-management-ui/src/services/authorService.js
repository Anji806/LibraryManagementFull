import api from './api.js'

export async function getAuthors() {
  const response = await api.get('/authors')
  return response.data
}

export async function getAuthorById(id) {
  const response = await api.get(`/authors/${id}`)
  return response.data
}

export async function createAuthor(author) {
  const response = await api.post('/authors', author)
  return response.data
}

export async function updateAuthor(id, author) {
  const response = await api.put(`/authors/${id}`, author)
  return response.data
}

export async function deleteAuthor(id) {
  const response = await api.delete(`/authors/${id}`)
  return response.data
}
